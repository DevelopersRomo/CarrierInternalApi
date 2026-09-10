using System.ComponentModel.DataAnnotations;
using InternalCarrierApp.API.DTOs;
using Xunit;

namespace InternalCarrierApp.API.Tests;

public class ApplicationDtoValidationTests
{
    [Fact]
    public void ApplicationWrite_RequiresCoreFieldsAndValidUrl()
    {
        var dto = new ApplicationWriteDto
        {
            Name = "",
            OwnerIds = [],
            ServerId = 0,
            DatabaseName = "",
            Url = "not-a-url"
        };

        var results = Validate(dto);

        Assert.Contains(results, result => result.MemberNames.Contains(nameof(dto.Name)));
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(dto.OwnerIds)));
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(dto.ServerId)));
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(dto.DatabaseName)));
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(dto.Url)));
    }

    [Fact]
    public void ApplicationWrite_RejectsNotesLongerThan200Characters()
    {
        var dto = new ApplicationWriteDto
        {
            Name = "Carrier app",
            OwnerIds = ["user-1"],
            ServerId = 1,
            DatabaseName = "CarrierDb",
            Url = "https://carrier.example.com",
            Notes = new string('x', 201)
        };

        Assert.Contains(
            Validate(dto),
            result => result.MemberNames.Contains(nameof(dto.Notes)));
    }

    private static List<ValidationResult> Validate(object value)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(value, new ValidationContext(value), results, true);
        return results;
    }
}
