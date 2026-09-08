using System.ComponentModel.DataAnnotations;

namespace InternalCarrierApp.API.Models;

public class HardwareInventory
{
    public int Id { get; set; }

    public int PlantId { get; set; }
    public Plant Plant { get; set; } = null!;

    [Required, MaxLength(50)]
    public string SerialNumber { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string Brand { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Model { get; set; }

    [MaxLength(20)]
    public string? DeviceType { get; set; }

    [MaxLength(100)]
    public string? Processor { get; set; }

    public int? RamGb { get; set; }

    [MaxLength(20)]
    public string? StorageType { get; set; }

    public int? StorageSizeGb { get; set; }

    [MaxLength(100)]
    public string? Gpu { get; set; }

    [MaxLength(100)]
    public string? OperatingSystem { get; set; }

    public DateOnly? WarrantyStartDate { get; set; }
    public DateOnly? WarrantyEndDate { get; set; }
    public DateOnly? PurchaseDate { get; set; }
    public int? EmployeeId { get; set; }

    [MaxLength(100)]
    public string? Location { get; set; }

    [Required, MaxLength(20)]
    public string Status { get; set; } = "Active";

    [MaxLength(255)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
