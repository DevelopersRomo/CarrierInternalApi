using System.Security.Claims;
using InternalCarrierApp.API.Data;
using InternalCarrierApp.API.DTOs;
using InternalCarrierApp.API.Filtering;
using InternalCarrierApp.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InternalCarrierApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ApplicationsController(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager) : ControllerBase
{
    private static readonly IReadOnlyDictionary<string, TableQueryField<ApplicationRecord>> Fields =
        new Dictionary<string, TableQueryField<ApplicationRecord>>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = TableQueryField<ApplicationRecord>.Integer("id", application => application.Id),
            ["name"] = TableQueryField<ApplicationRecord>.String("name", application => application.Name),
            ["ownerName"] = TableQueryField<ApplicationRecord>.String("ownerName", application => application.Owners.Select(owner => owner.User.FullName).FirstOrDefault()),
            ["serverName"] = TableQueryField<ApplicationRecord>.String("serverName", application => application.Server.ServerName),
            ["databaseName"] = TableQueryField<ApplicationRecord>.String("databaseName", application => application.DatabaseName),
            ["url"] = TableQueryField<ApplicationRecord>.String("url", application => application.Url),
            ["environment"] = TableQueryField<ApplicationRecord>.String("environment", application => application.Environment),
            ["notes"] = TableQueryField<ApplicationRecord>.String("notes", application => application.Notes),
            ["createdAt"] = TableQueryField<ApplicationRecord>.DateTime("createdAt", application => application.CreatedAt),
            ["updatedAt"] = TableQueryField<ApplicationRecord>.DateTime("updatedAt", application => application.UpdatedAt),
            ["lastModifiedBy"] = TableQueryField<ApplicationRecord>.String("lastModifiedBy", application => application.LastModifiedBy)
        };

    [HttpGet]
    public async Task<ActionResult<PagedResult<ApplicationDto>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int size = 100,
        [FromQuery(Name = "filter_query")] string? filterQuery = null,
        [FromQuery(Name = "sort_by")] string? sortBy = null,
        CancellationToken cancellationToken = default)
    {
        if (page < 1 || size is < 1 or > 500)
            return BadRequest("page must be at least 1 and size must be between 1 and 500.");

        var query = AuthorizedQuery();
        try
        {
            query = TableQuery.ApplyFilters(query, filterQuery, Fields);
            var total = await query.CountAsync(cancellationToken);
            var applications = await TableQuery.ApplySort(query, sortBy, Fields, "name")
                .ThenBy(application => application.Id)
                .Skip((page - 1) * size)
                .Take(size)
                .ToListAsync(cancellationToken);

            return Ok(new PagedResult<ApplicationDto>(
                applications.Select(ToDto).ToList(),
                total,
                page,
                size,
                (int)Math.Ceiling(total / (double)size)));
        }
        catch (TableQueryValidationException exception)
        {
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { ["query"] = [exception.Message] }));
        }
    }

    [HttpGet("owners")]
    public async Task<ActionResult<IEnumerable<ApplicationOwnerOption>>> GetOwners(CancellationToken cancellationToken)
    {
        var owners = await userManager.Users
            .Where(user => user.IsActive)
            .OrderBy(user => user.FullName)
            .Select(user => new ApplicationOwnerOption(user.Id, user.FullName, user.Email!))
            .ToListAsync(cancellationToken);
        return Ok(owners);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApplicationDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var application = await AuthorizedQuery(false).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        return application is null ? NotFound() : Ok(ToDto(application));
    }

    [HttpPost]
    [Authorize(Roles = "Admin,DBAAdmin,Engineer")]
    public async Task<ActionResult<ApplicationDto>> Create(
        [FromBody] ApplicationWriteDto dto,
        CancellationToken cancellationToken)
    {
        var validation = await ValidateReferences(dto, cancellationToken);
        if (validation is not null) return validation;

        var application = new ApplicationRecord();
        Apply(application, dto);
        db.Applications.Add(application);
        await db.SaveChangesAsync(cancellationToken);
        await db.Entry(application).Collection(item => item.Owners).Query().Include(owner => owner.User).LoadAsync(cancellationToken);
        await db.Entry(application).Reference(item => item.Server).LoadAsync(cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = application.Id }, ToDto(application));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin,DBAAdmin,Engineer")]
    public async Task<ActionResult<ApplicationDto>> Update(
        int id,
        [FromBody] ApplicationWriteDto dto,
        CancellationToken cancellationToken)
    {
        var application = await AuthorizedQuery().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (application is null) return NotFound();

        var validation = await ValidateReferences(dto, cancellationToken);
        if (validation is not null) return validation;

        Apply(application, dto);
        application.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        await db.Entry(application).Collection(item => item.Owners).Query().Include(owner => owner.User).LoadAsync(cancellationToken);
        await db.Entry(application).Reference(item => item.Server).LoadAsync(cancellationToken);
        return Ok(ToDto(application));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var application = await AuthorizedQuery().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (application is null) return NotFound();
        db.Applications.Remove(application);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private IQueryable<ApplicationRecord> AuthorizedQuery(bool asNoTracking = true)
    {
        var query = db.Applications
            .Include(application => application.Owners)
                .ThenInclude(owner => owner.User)
            .Include(application => application.Server);
        return asNoTracking ? query.AsNoTracking() : query;
    }

    private async Task<ActionResult?> ValidateReferences(ApplicationWriteDto dto, CancellationToken cancellationToken)
    {
        var ownerIds = dto.OwnerIds.Distinct(StringComparer.Ordinal).ToList();
        if (ownerIds.Count == 0)
            return BadRequest(new { message = "At least one owner is required." });

        var activeOwnerCount = await userManager.Users
            .CountAsync(user => ownerIds.Contains(user.Id) && user.IsActive, cancellationToken);
        if (activeOwnerCount != ownerIds.Count)
            return BadRequest(new { message = "All owners must be active users." });

        var allowedPlants = await GetUserPlantIdsAsync(cancellationToken);
        var serverExists = await db.Servers.AnyAsync(
            server => server.Id == dto.ServerId && allowedPlants.Contains(server.PlantId),
            cancellationToken);
        return serverExists ? null : NotFound("Server not found.");
    }

    private async Task<List<int>> GetUserPlantIdsAsync(CancellationToken cancellationToken)
    {
        if (User.IsInRole("Admin"))
            return await db.Plants.Select(plant => plant.Id).ToListAsync(cancellationToken);

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return await db.UserPlants
            .Where(userPlant => userPlant.UserId == userId)
            .Select(userPlant => userPlant.PlantId)
            .ToListAsync(cancellationToken);
    }

    private void Apply(ApplicationRecord application, ApplicationWriteDto dto)
    {
        application.Name = dto.Name.Trim();
        application.ServerId = dto.ServerId;
        application.DatabaseName = dto.DatabaseName.Trim();
        application.IsActive = dto.IsActive;
        application.Url = dto.Url.Trim();
        application.Environment = Clean(dto.Environment);
        application.Notes = Clean(dto.Notes);
        application.LastModifiedBy = User.FindFirstValue(ClaimTypes.Email);
        application.Owners.Clear();
        foreach (var ownerId in dto.OwnerIds.Distinct(StringComparer.Ordinal))
            application.Owners.Add(new ApplicationOwner { UserId = ownerId });
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static ApplicationDto ToDto(ApplicationRecord application) => new(
        application.Id,
        application.Name,
        application.Owners.Select(owner => owner.UserId).ToList(),
        application.Owners.Select(owner => owner.User.FullName).ToList(),
        string.Join(", ", application.Owners.Select(owner => owner.User.FullName)),
        application.ServerId,
        application.Server.ServerName,
        application.DatabaseName,
        application.IsActive,
        application.Url,
        application.Environment,
        application.Notes,
        application.CreatedAt,
        application.UpdatedAt,
        application.LastModifiedBy);
}
