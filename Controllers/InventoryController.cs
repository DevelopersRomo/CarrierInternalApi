using System.Security.Claims;
using InternalCarrierApp.API.Data;
using InternalCarrierApp.API.DTOs;
using InternalCarrierApp.API.Filtering;
using InternalCarrierApp.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace InternalCarrierApp.API.Controllers;

[ApiController]
[Route("api/inventory")]
[Authorize]
public class InventoryController(ApplicationDbContext db) : ControllerBase
{
    private static readonly IReadOnlyDictionary<string, TableQueryField<HardwareInventory>> Fields =
        new Dictionary<string, TableQueryField<HardwareInventory>>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = TableQueryField<HardwareInventory>.Integer("id", item => item.Id),
            ["plantId"] = TableQueryField<HardwareInventory>.Integer("plantId", item => item.PlantId),
            ["plantCode"] = TableQueryField<HardwareInventory>.String("plantCode", item => item.Plant.Code),
            ["serialNumber"] = TableQueryField<HardwareInventory>.String("serialNumber", item => item.SerialNumber),
            ["brand"] = TableQueryField<HardwareInventory>.String("brand", item => item.Brand),
            ["model"] = TableQueryField<HardwareInventory>.String("model", item => item.Model),
            ["deviceType"] = TableQueryField<HardwareInventory>.String("deviceType", item => item.DeviceType),
            ["processor"] = TableQueryField<HardwareInventory>.String("processor", item => item.Processor),
            ["ramGb"] = TableQueryField<HardwareInventory>.NullableInteger("ramGb", item => item.RamGb),
            ["storageType"] = TableQueryField<HardwareInventory>.String("storageType", item => item.StorageType),
            ["storageSizeGb"] = TableQueryField<HardwareInventory>.NullableInteger("storageSizeGb", item => item.StorageSizeGb),
            ["gpu"] = TableQueryField<HardwareInventory>.String("gpu", item => item.Gpu),
            ["operatingSystem"] = TableQueryField<HardwareInventory>.String("operatingSystem", item => item.OperatingSystem),
            ["warrantyStartDate"] = TableQueryField<HardwareInventory>.Date("warrantyStartDate", item => item.WarrantyStartDate),
            ["warrantyEndDate"] = TableQueryField<HardwareInventory>.Date("warrantyEndDate", item => item.WarrantyEndDate),
            ["purchaseDate"] = TableQueryField<HardwareInventory>.Date("purchaseDate", item => item.PurchaseDate),
            ["employeeId"] = TableQueryField<HardwareInventory>.NullableInteger("employeeId", item => item.EmployeeId),
            ["location"] = TableQueryField<HardwareInventory>.String("location", item => item.Location),
            ["status"] = TableQueryField<HardwareInventory>.String("status", item => item.Status),
            ["notes"] = TableQueryField<HardwareInventory>.String("notes", item => item.Notes),
            ["createdAt"] = TableQueryField<HardwareInventory>.DateTime("createdAt", item => item.CreatedAt),
            ["updatedAt"] = TableQueryField<HardwareInventory>.DateTime("updatedAt", item => item.UpdatedAt)
        };

    [HttpGet]
    public async Task<ActionResult<PagedResult<InventoryDto>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int size = 100,
        [FromQuery(Name = "filter_query")] string? filterQuery = null,
        [FromQuery(Name = "sort_by")] string? sortBy = null,
        CancellationToken cancellationToken = default)
    {
        if (page < 1 || size is < 1 or > 500)
            return InvalidPaging();

        var allowedPlants = await GetUserPlantIdsAsync(cancellationToken);
        var query = db.Inventory
            .AsNoTracking()
            .Include(item => item.Plant)
            .Where(item => allowedPlants.Contains(item.PlantId));

        try
        {
            query = TableQuery.ApplyFilters(query, filterQuery, Fields);
            var total = await query.CountAsync(cancellationToken);
            var records = await TableQuery.ApplySort(query, sortBy, Fields, "serialNumber")
                .ThenBy(item => item.Id)
                .Skip((page - 1) * size)
                .Take(size)
                .ToListAsync(cancellationToken);

            return Ok(new PagedResult<InventoryDto>(
                records.Select(ToDto).ToList(),
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
    public async Task<ActionResult<InventoryDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var allowedPlants = await GetUserPlantIdsAsync(cancellationToken);
        var item = await db.Inventory
            .AsNoTracking()
            .Include(record => record.Plant)
            .FirstOrDefaultAsync(
                record => record.Id == id && allowedPlants.Contains(record.PlantId),
                cancellationToken);

        return item is null ? NotFound() : Ok(ToDto(item));
    }

    [HttpPost]
    [Authorize(Roles = "Admin,DBAAdmin,Engineer")]
    public async Task<ActionResult<InventoryDto>> Create(
        [FromBody] InventoryWriteDto dto,
        CancellationToken cancellationToken)
    {
        var allowedPlants = await GetUserPlantIdsAsync(cancellationToken);
        if (!allowedPlants.Contains(dto.PlantId))
            return NotFound();

        var serialNumber = dto.SerialNumber.Trim();
        if (await db.Inventory.AnyAsync(item => item.SerialNumber == serialNumber, cancellationToken))
            return DuplicateSerial(serialNumber);

        var item = new HardwareInventory();
        Apply(item, dto, serialNumber);
        db.Inventory.Add(item);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            return DuplicateSerial(serialNumber);
        }

        await db.Entry(item).Reference(record => record.Plant).LoadAsync(cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = item.Id }, ToDto(item));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin,DBAAdmin,Engineer")]
    public async Task<ActionResult<InventoryDto>> Update(
        int id,
        [FromBody] InventoryWriteDto dto,
        CancellationToken cancellationToken)
    {
        var allowedPlants = await GetUserPlantIdsAsync(cancellationToken);
        var item = await db.Inventory
            .Include(record => record.Plant)
            .FirstOrDefaultAsync(
                record => record.Id == id && allowedPlants.Contains(record.PlantId),
                cancellationToken);

        if (item is null || !allowedPlants.Contains(dto.PlantId))
            return NotFound();

        var serialNumber = dto.SerialNumber.Trim();
        if (await db.Inventory.AnyAsync(
                record => record.Id != id && record.SerialNumber == serialNumber,
                cancellationToken))
            return DuplicateSerial(serialNumber);

        Apply(item, dto, serialNumber);
        item.UpdatedAt = DateTime.UtcNow;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            return DuplicateSerial(serialNumber);
        }

        var plantReference = db.Entry(item).Reference(record => record.Plant);
        if (plantReference.IsLoaded && item.Plant.Id != item.PlantId)
        {
            plantReference.IsLoaded = false;
            await plantReference.LoadAsync(cancellationToken);
        }

        return Ok(ToDto(item));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var allowedPlants = await GetUserPlantIdsAsync(cancellationToken);
        var item = await db.Inventory.FirstOrDefaultAsync(
            record => record.Id == id && allowedPlants.Contains(record.PlantId),
            cancellationToken);
        if (item is null)
            return NotFound();

        db.Inventory.Remove(item);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
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

    private static void Apply(HardwareInventory item, InventoryWriteDto dto, string serialNumber)
    {
        item.PlantId = dto.PlantId;
        item.SerialNumber = serialNumber;
        item.Brand = dto.Brand.Trim();
        item.Model = Clean(dto.Model);
        item.DeviceType = Clean(dto.DeviceType);
        item.Processor = Clean(dto.Processor);
        item.RamGb = dto.RamGb;
        item.StorageType = Clean(dto.StorageType);
        item.StorageSizeGb = dto.StorageSizeGb;
        item.Gpu = Clean(dto.Gpu);
        item.OperatingSystem = Clean(dto.OperatingSystem);
        item.WarrantyStartDate = dto.WarrantyStartDate;
        item.WarrantyEndDate = dto.WarrantyEndDate;
        item.PurchaseDate = dto.PurchaseDate;
        item.EmployeeId = dto.EmployeeId;
        item.Location = Clean(dto.Location);
        item.Status = dto.Status.Trim();
        item.Notes = Clean(dto.Notes);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 };

    private ConflictObjectResult DuplicateSerial(string serialNumber) => Conflict(new ProblemDetails
    {
        Status = StatusCodes.Status409Conflict,
        Title = "Duplicate serial number",
        Detail = $"An inventory record with serial number '{serialNumber}' already exists."
    });

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

    private static InventoryDto ToDto(HardwareInventory item) => new(
        item.Id,
        item.PlantId,
        item.Plant.Code,
        item.SerialNumber,
        item.Brand,
        item.Model,
        item.DeviceType,
        item.Processor,
        item.RamGb,
        item.StorageType,
        item.StorageSizeGb,
        item.Gpu,
        item.OperatingSystem,
        item.WarrantyStartDate,
        item.WarrantyEndDate,
        item.PurchaseDate,
        item.EmployeeId,
        item.Location,
        item.Status,
        item.Notes,
        item.CreatedAt,
        item.UpdatedAt);
}
