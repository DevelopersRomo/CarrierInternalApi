using System.ComponentModel.DataAnnotations;

namespace InternalCarrierApp.API.DTOs;

public sealed record InventoryDto(
    int Id,
    int PlantId,
    string PlantCode,
    string SerialNumber,
    string Brand,
    string? Model,
    string? DeviceType,
    string? Processor,
    int? RamGb,
    string? StorageType,
    int? StorageSizeGb,
    string? Gpu,
    string? OperatingSystem,
    DateOnly? WarrantyStartDate,
    DateOnly? WarrantyEndDate,
    DateOnly? PurchaseDate,
    int? EmployeeId,
    string? Location,
    string Status,
    string? Notes,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed class InventoryWriteDto : IValidatableObject
{
    [Range(1, int.MaxValue)]
    public int PlantId { get; init; }

    [Required, MaxLength(50)]
    public string SerialNumber { get; init; } = string.Empty;

    [Required, MaxLength(50)]
    public string Brand { get; init; } = string.Empty;

    [MaxLength(100)]
    public string? Model { get; init; }

    [RegularExpression("^(Laptop|Desktop|Workstation|Server)$")]
    public string? DeviceType { get; init; }

    [MaxLength(100)]
    public string? Processor { get; init; }

    [Range(0, int.MaxValue)]
    public int? RamGb { get; init; }

    [RegularExpression("^(HDD|SSD|NVMe|Hybrid)$")]
    public string? StorageType { get; init; }

    [Range(0, int.MaxValue)]
    public int? StorageSizeGb { get; init; }

    [MaxLength(100)]
    public string? Gpu { get; init; }

    [MaxLength(100)]
    public string? OperatingSystem { get; init; }

    public DateOnly? WarrantyStartDate { get; init; }
    public DateOnly? WarrantyEndDate { get; init; }
    public DateOnly? PurchaseDate { get; init; }
    public int? EmployeeId { get; init; }

    [MaxLength(100)]
    public string? Location { get; init; }

    [Required, RegularExpression("^(Active|In Repair|Retired|Spare)$")]
    public string Status { get; init; } = "Active";

    [MaxLength(255)]
    public string? Notes { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (WarrantyStartDate.HasValue && WarrantyEndDate.HasValue && WarrantyEndDate < WarrantyStartDate)
        {
            yield return new ValidationResult(
                "Warranty end date must be on or after the warranty start date.",
                [nameof(WarrantyEndDate)]);
        }
    }
}
