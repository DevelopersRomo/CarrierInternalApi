using System.ComponentModel.DataAnnotations;
using InternalCarrierApp.API.DTOs;
using Xunit;

namespace InternalCarrierApp.API.Tests;

public class DtoValidationTests
{
    [Fact]
    public void InventoryWrite_RejectsInvalidRangesEnumsAndWarrantyOrder()
    {
        var dto = new InventoryWriteDto
        {
            PlantId = 1,
            SerialNumber = "SERIAL-1",
            Brand = "Carrier",
            DeviceType = "Phone",
            RamGb = -1,
            Status = "Unknown",
            WarrantyStartDate = new DateOnly(2026, 9, 2),
            WarrantyEndDate = new DateOnly(2026, 9, 1)
        };

        var results = Validate(dto);

        Assert.Contains(results, result => result.MemberNames.Contains(nameof(dto.DeviceType)));
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(dto.RamGb)));
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(dto.Status)));
        var warrantyOnly = new InventoryWriteDto
        {
            PlantId = 1,
            SerialNumber = "SERIAL-2",
            Brand = "Carrier",
            Status = "Active",
            WarrantyStartDate = new DateOnly(2026, 9, 2),
            WarrantyEndDate = new DateOnly(2026, 9, 1)
        };
        Assert.Contains(
            Validate(warrantyOnly),
            result => result.MemberNames.Contains(nameof(dto.WarrantyEndDate)));
    }

    [Fact]
    public void ServerWrite_RejectsNegativeCapacityAndPriority()
    {
        var dto = new CreateServerDto
        {
            PlantId = 1,
            ServerName = "server",
            RamGb = -1,
            Priority = -1
        };

        var results = Validate(dto);

        Assert.Contains(results, result => result.MemberNames.Contains(nameof(dto.RamGb)));
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(dto.Priority)));
    }

    private static List<ValidationResult> Validate(object value)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(value, new ValidationContext(value), results, true);
        return results;
    }
}
