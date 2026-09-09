using System.Security.Claims;
using ClosedXML.Excel;
using InternalCarrierApp.API.Data;
using InternalCarrierApp.API.DTOs;
using InternalCarrierApp.API.Filtering;
using InternalCarrierApp.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InternalCarrierApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ServersController(ApplicationDbContext db) : ControllerBase
{
    private static readonly IReadOnlyDictionary<string, TableQueryField<ServerRecord>> Fields =
        new Dictionary<string, TableQueryField<ServerRecord>>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = TableQueryField<ServerRecord>.Integer("id", server => server.Id),
            ["plantId"] = TableQueryField<ServerRecord>.Integer("plantId", server => server.PlantId),
            ["plantCode"] = TableQueryField<ServerRecord>.String("plantCode", server => server.Plant.Code),
            ["serverName"] = TableQueryField<ServerRecord>.String("serverName", server => server.ServerName),
            ["ipAddress"] = TableQueryField<ServerRecord>.String("ipAddress", server => server.IpAddress),
            ["applicationDescription"] = TableQueryField<ServerRecord>.String("applicationDescription", server => server.ApplicationDescription),
            ["appName"] = TableQueryField<ServerRecord>.String("appName", server => server.AppName),
            ["site"] = TableQueryField<ServerRecord>.String("site", server => server.Site),
            ["environment"] = TableQueryField<ServerRecord>.String("environment", server => server.Environment),
            ["infraType"] = TableQueryField<ServerRecord>.String("infraType", server => server.InfraType),
            ["resourceType"] = TableQueryField<ServerRecord>.String("resourceType", server => server.ResourceType),
            ["sqlVersion"] = TableQueryField<ServerRecord>.String("sqlVersion", server => server.SqlVersion),
            ["operativeSystem"] = TableQueryField<ServerRecord>.String("operativeSystem", server => server.OperativeSystem),
            ["ramGb"] = TableQueryField<ServerRecord>.NullableInteger("ramGb", server => server.RamGb),
            ["cpuQty"] = TableQueryField<ServerRecord>.String("cpuQty", server => server.CpuQty),
            ["diskC"] = TableQueryField<ServerRecord>.String("diskC", server => server.DiskC),
            ["diskD"] = TableQueryField<ServerRecord>.String("diskD", server => server.DiskD),
            ["diskE"] = TableQueryField<ServerRecord>.String("diskE", server => server.DiskE),
            ["ceNumberServer"] = TableQueryField<ServerRecord>.String("ceNumberServer", server => server.CeNumberServer),
            ["ceNumberSql"] = TableQueryField<ServerRecord>.String("ceNumberSql", server => server.CeNumberSql),
            ["notes"] = TableQueryField<ServerRecord>.String("notes", server => server.Notes),
            ["priority"] = TableQueryField<ServerRecord>.Integer("priority", server => server.Priority),
            ["createdAt"] = TableQueryField<ServerRecord>.DateTime("createdAt", server => server.CreatedAt),
            ["updatedAt"] = TableQueryField<ServerRecord>.DateTime("updatedAt", server => server.UpdatedAt),
            ["lastModifiedBy"] = TableQueryField<ServerRecord>.String("lastModifiedBy", server => server.LastModifiedBy)
        };

    [HttpGet]
    public async Task<ActionResult<PagedResult<ServerDto>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int size = 100,
        [FromQuery(Name = "filter_query")] string? filterQuery = null,
        [FromQuery(Name = "sort_by")] string? sortBy = null,
        [FromQuery] int? plantId = null,
        [FromQuery] string? environment = null,
        [FromQuery] string? resourceType = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        if (page < 1 || size is < 1 or > 500)
            return InvalidPaging();

        var allowedPlants = await GetUserPlantIdsAsync(cancellationToken);
        var query = ApplyCompatibilityFilters(
            AuthorizedQuery(allowedPlants),
            plantId,
            environment,
            resourceType,
            search);

        try
        {
            query = TableQuery.ApplyFilters(query, filterQuery, Fields);
            var total = await query.CountAsync(cancellationToken);
            var servers = await TableQuery.ApplySort(query, sortBy, Fields, "serverName")
                .ThenBy(server => server.Id)
                .Skip((page - 1) * size)
                .Take(size)
                .ToListAsync(cancellationToken);

            return Ok(new PagedResult<ServerDto>(
                servers.Select(ToDto).ToList(),
                total,
                page,
                size,
                (int)Math.Ceiling(total / (double)size)));
        }
        catch (TableQueryValidationException exception)
        {
            return InvalidQuery(exception.Message);
        }
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ServerDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var allowedPlants = await GetUserPlantIdsAsync(cancellationToken);
        var server = await AuthorizedQuery(allowedPlants)
            .FirstOrDefaultAsync(record => record.Id == id, cancellationToken);
        return server is null ? NotFound() : Ok(ToDto(server));
    }

    [HttpPost]
    [Authorize(Roles = "Admin,DBAAdmin,Engineer")]
    public async Task<ActionResult<ServerDto>> Create(
        [FromBody] CreateServerDto dto,
        CancellationToken cancellationToken)
    {
        var allowedPlants = await GetUserPlantIdsAsync(cancellationToken);
        if (!allowedPlants.Contains(dto.PlantId))
            return NotFound();

        var server = new ServerRecord { PlantId = dto.PlantId };
        Apply(server, dto);
        db.Servers.Add(server);
        await db.SaveChangesAsync(cancellationToken);
        await db.Entry(server).Reference(record => record.Plant).LoadAsync(cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = server.Id }, ToDto(server));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin,DBAAdmin,Engineer")]
    public async Task<ActionResult<ServerDto>> Update(
        int id,
        [FromBody] UpdateServerDto dto,
        CancellationToken cancellationToken)
    {
        var allowedPlants = await GetUserPlantIdsAsync(cancellationToken);
        var server = await db.Servers
            .Include(record => record.Plant)
            .FirstOrDefaultAsync(
                record => record.Id == id && allowedPlants.Contains(record.PlantId),
                cancellationToken);
        if (server is null)
            return NotFound();

        Apply(server, dto);
        server.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(ToDto(server));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var allowedPlants = await GetUserPlantIdsAsync(cancellationToken);
        var server = await db.Servers.FirstOrDefaultAsync(
            record => record.Id == id && allowedPlants.Contains(record.PlantId),
            cancellationToken);
        if (server is null)
            return NotFound();

        db.Servers.Remove(server);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export(
        [FromQuery(Name = "filter_query")] string? filterQuery = null,
        [FromQuery(Name = "sort_by")] string? sortBy = null,
        [FromQuery] int? plantId = null,
        [FromQuery] string? environment = null,
        [FromQuery] string? resourceType = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var allowedPlants = await GetUserPlantIdsAsync(cancellationToken);
        var query = ApplyCompatibilityFilters(
            AuthorizedQuery(allowedPlants),
            plantId,
            environment,
            resourceType,
            search);

        List<ServerRecord> servers;
        try
        {
            query = TableQuery.ApplyFilters(query, filterQuery, Fields);
            servers = await TableQuery.ApplySort(query, sortBy, Fields, "serverName")
                .ThenBy(server => server.Id)
                .ToListAsync(cancellationToken);
        }
        catch (TableQueryValidationException exception)
        {
            return InvalidQuery(exception.Message);
        }

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Server Inventory");
        worksheet.Cell(1, 1).Value = "CARRIER MEXICO SERVER INVENTORY";
        worksheet.Range(1, 1, 1, 18).Merge()
            .Style.Font.SetBold(true).Font.SetFontSize(14)
            .Fill.SetBackgroundColor(XLColor.FromHtml("#1B4F8A"))
            .Font.SetFontColor(XLColor.White)
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
        worksheet.Row(1).Height = 30;

        string[] headers =
        [
            "Priority", "Plant", "Server", "IP", "Application", "App Name", "Site", "Environment",
            "Infrastructure", "Resource", "SQL Version", "OS", "RAM (GB)", "CPU", "Disk C", "Disk D",
            "Disk E", "Server CE#"
        ];
        for (var index = 0; index < headers.Length; index++)
        {
            var cell = worksheet.Cell(2, index + 1);
            cell.Value = headers[index];
            cell.Style.Font.SetBold(true)
                .Fill.SetBackgroundColor(XLColor.FromHtml("#2E75B6"))
                .Font.SetFontColor(XLColor.White)
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
        }

        var row = 3;
        foreach (var server in servers)
        {
            var background = server.Environment?.Contains("QA", StringComparison.OrdinalIgnoreCase) == true
                ? XLColor.FromHtml("#FFF2CC")
                : server.ResourceType?.Contains("SQL", StringComparison.OrdinalIgnoreCase) == true
                    ? XLColor.FromHtml("#D6E4F0")
                    : XLColor.White;
            object[] values =
            [
                server.Priority, server.Plant.Code, server.ServerName, server.IpAddress ?? string.Empty,
                server.ApplicationDescription ?? string.Empty, server.AppName ?? string.Empty,
                server.Site ?? string.Empty, server.Environment ?? string.Empty, server.InfraType ?? string.Empty,
                server.ResourceType ?? string.Empty, server.SqlVersion ?? string.Empty,
                server.OperativeSystem ?? string.Empty, server.RamGb ?? (object)string.Empty,
                server.CpuQty ?? string.Empty, server.DiskC ?? string.Empty, server.DiskD ?? string.Empty,
                server.DiskE ?? string.Empty, server.CeNumberServer ?? string.Empty
            ];

            for (var column = 0; column < values.Length; column++)
            {
                var cell = worksheet.Cell(row, column + 1);
                cell.Value = XLCellValue.FromObject(values[column]);
                cell.Style.Fill.SetBackgroundColor(background);
            }
            row++;
        }

        worksheet.Columns().AdjustToContents();
        worksheet.SheetView.FreezeRows(2);
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        return File(
            stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"Server_Inventory_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
    }

    private IQueryable<ServerRecord> AuthorizedQuery(IReadOnlyCollection<int> allowedPlants) => db.Servers
        .AsNoTracking()
        .Include(server => server.Plant)
        .Where(server => allowedPlants.Contains(server.PlantId));

    private static IQueryable<ServerRecord> ApplyCompatibilityFilters(
        IQueryable<ServerRecord> query,
        int? plantId,
        string? environment,
        string? resourceType,
        string? search)
    {
        if (plantId.HasValue)
            query = query.Where(server => server.PlantId == plantId);
        if (!string.IsNullOrWhiteSpace(environment))
            query = query.Where(server => server.Environment == environment.Trim());
        if (!string.IsNullOrWhiteSpace(resourceType))
            query = query.Where(server => server.ResourceType == resourceType.Trim());
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(server =>
                server.ServerName.ToLower().Contains(term) ||
                (server.IpAddress != null && server.IpAddress.ToLower().Contains(term)) ||
                (server.ApplicationDescription != null && server.ApplicationDescription.ToLower().Contains(term)));
        }
        return query;
    }

    private async Task<List<int>> GetUserPlantIdsAsync(CancellationToken cancellationToken)
    {
        if (User.IsInRole("Admin"))
            return await db.Plants.AsNoTracking().Select(plant => plant.Id).ToListAsync(cancellationToken);

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return await db.UserPlants
            .AsNoTracking()
            .Where(userPlant => userPlant.UserId == userId)
            .Select(userPlant => userPlant.PlantId)
            .ToListAsync(cancellationToken);
    }

    private void Apply(ServerRecord server, CreateServerDto dto)
    {
        server.ServerName = dto.ServerName.Trim();
        server.IpAddress = Clean(dto.IpAddress);
        server.ApplicationDescription = Clean(dto.ApplicationDescription);
        server.AppName = Clean(dto.AppName);
        server.Site = Clean(dto.Site);
        server.Environment = Clean(dto.Environment);
        server.InfraType = Clean(dto.InfraType);
        server.ResourceType = Clean(dto.ResourceType);
        server.SqlVersion = Clean(dto.SqlVersion);
        server.OperativeSystem = Clean(dto.OperativeSystem);
        server.RamGb = dto.RamGb;
        server.CpuQty = Clean(dto.CpuQty);
        server.DiskC = Clean(dto.DiskC);
        server.DiskD = Clean(dto.DiskD);
        server.DiskE = Clean(dto.DiskE);
        server.CeNumberServer = Clean(dto.CeNumberServer);
        server.CeNumberSql = Clean(dto.CeNumberSql);
        server.Notes = Clean(dto.Notes);
        server.Priority = dto.Priority;
        server.LastModifiedBy = User.FindFirstValue(ClaimTypes.Email);
    }

    private void Apply(ServerRecord server, UpdateServerDto dto)
    {
        server.ServerName = dto.ServerName.Trim();
        server.IpAddress = Clean(dto.IpAddress);
        server.ApplicationDescription = Clean(dto.ApplicationDescription);
        server.AppName = Clean(dto.AppName);
        server.Site = Clean(dto.Site);
        server.Environment = Clean(dto.Environment);
        server.InfraType = Clean(dto.InfraType);
        server.ResourceType = Clean(dto.ResourceType);
        server.SqlVersion = Clean(dto.SqlVersion);
        server.OperativeSystem = Clean(dto.OperativeSystem);
        server.RamGb = dto.RamGb;
        server.CpuQty = Clean(dto.CpuQty);
        server.DiskC = Clean(dto.DiskC);
        server.DiskD = Clean(dto.DiskD);
        server.DiskE = Clean(dto.DiskE);
        server.CeNumberServer = Clean(dto.CeNumberServer);
        server.CeNumberSql = Clean(dto.CeNumberSql);
        server.Notes = Clean(dto.Notes);
        server.Priority = dto.Priority;
        server.LastModifiedBy = User.FindFirstValue(ClaimTypes.Email);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private BadRequestObjectResult InvalidPaging() => BadRequest(new ValidationProblemDetails(
        new Dictionary<string, string[]>
        {
            ["pagination"] = ["page must be at least 1 and size must be between 1 and 500."]
        })
    {
        Status = StatusCodes.Status400BadRequest,
        Title = "Invalid table pagination"
    });

    private BadRequestObjectResult InvalidQuery(string message) => BadRequest(new ValidationProblemDetails(
        new Dictionary<string, string[]> { ["query"] = [message] })
    {
        Status = StatusCodes.Status400BadRequest,
        Title = "Invalid table query"
    });

    private static ServerDto ToDto(ServerRecord server) => new(
        server.Id,
        server.PlantId,
        server.Plant.Code,
        server.ServerName,
        server.IpAddress,
        server.ApplicationDescription,
        server.AppName,
        server.Site,
        server.Environment,
        server.InfraType,
        server.ResourceType,
        server.SqlVersion,
        server.OperativeSystem,
        server.RamGb,
        server.CpuQty,
        server.DiskC,
        server.DiskD,
        server.DiskE,
        server.CeNumberServer,
        server.CeNumberSql,
        server.Notes,
        server.Priority,
        server.UpdatedAt,
        server.LastModifiedBy);
}
