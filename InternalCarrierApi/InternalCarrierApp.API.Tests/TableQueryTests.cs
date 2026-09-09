using InternalCarrierApp.API.Filtering;
using Xunit;

namespace InternalCarrierApp.API.Tests;

public class TableQueryTests
{
    private static readonly IReadOnlyDictionary<string, TableQueryField<TestRecord>> Fields =
        new Dictionary<string, TableQueryField<TestRecord>>(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = TableQueryField<TestRecord>.String("name", record => record.Name),
            ["status"] = TableQueryField<TestRecord>.String("status", record => record.Status),
            ["amount"] = TableQueryField<TestRecord>.NullableInteger("amount", record => record.Amount),
            ["createdOn"] = TableQueryField<TestRecord>.Date("createdOn", record => record.CreatedOn),
            ["id"] = TableQueryField<TestRecord>.Integer("id", record => record.Id)
        };

    [Fact]
    public void ParseFilters_HandlesEscapedQuotesAndAndInsideValues()
    {
        var result = TableQuery.ParseFilters(
            "global_search eq 'O''Brien AND Sons' AND status in ['Active','Spare']",
            Fields);

        Assert.Equal(2, result.Count);
        Assert.Equal("O'Brien AND Sons", result[0].Value);
        Assert.Equal(["Active", "Spare"], Assert.IsType<object[]>(result[1].Value));
    }

    [Fact]
    public void ParseFilters_ConvertsComparableValuesAndSupportsRanges()
    {
        var result = TableQuery.ParseFilters(
            "amount ge 16 AND createdOn between ['2026-01-01','2026-12-31']",
            Fields);

        Assert.Equal(16, result[0].Value);
        Assert.Equal(
            [new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)],
            Assert.IsType<object[]>(result[1].Value));
    }

    [Fact]
    public void ParseFilters_SupportsNotILike()
    {
        var result = TableQuery.ParseFilters("name not_ilike 'retired'", Fields);

        Assert.Equal("not_ilike", result.Single().Operator);
    }

    [Fact]
    public void ApplyFilters_ComposesGlobalStringAndTypedRangeConditions()
    {
        var records = new[]
        {
            new TestRecord { Id = 1, Name = "Dell Laptop", Status = "Active", Amount = 16 },
            new TestRecord { Id = 2, Name = "Dell Desktop", Status = "Retired", Amount = 8 },
            new TestRecord { Id = 3, Name = "Lenovo Laptop", Status = "Active", Amount = 32 }
        }.AsQueryable();

        var result = TableQuery.ApplyFilters(
            records,
            "global_search eq 'dell' AND status not_ilike 'retired' AND amount ge 16",
            Fields).ToList();

        Assert.Equal(1, Assert.Single(result).Id);
    }

    [Fact]
    public void ApplySort_UsesTheAllowlistedTypedSelector()
    {
        var records = new[]
        {
            new TestRecord { Id = 1, Name = "Beta" },
            new TestRecord { Id = 2, Name = "Alpha" }
        }.AsQueryable();

        var result = TableQuery.ApplySort(records, "name asc", Fields, "id").ToList();

        Assert.Equal([2, 1], result.Select(record => record.Id));
    }

    [Theory]
    [InlineData("missing eq 'value'")]
    [InlineData("amount ilike '1'")]
    [InlineData("id is_null")]
    [InlineData("status in 'Active'")]
    [InlineData("name eq 'unterminated")]
    public void ParseFilters_RejectsUnsafeOrMalformedConditions(string query)
    {
        Assert.Throws<TableQueryValidationException>(() => TableQuery.ParseFilters(query, Fields));
    }

    [Theory]
    [InlineData("missing asc")]
    [InlineData("name sideways")]
    [InlineData("name asc extra")]
    public void ParseSort_RejectsUnknownOrMalformedSorts(string sort)
    {
        Assert.Throws<TableQueryValidationException>(() =>
            TableQuery.ParseSort(sort, Fields, "name"));
    }

    private sealed class TestRecord
    {
        public int Id { get; init; }
        public string? Name { get; init; }
        public string? Status { get; init; }
        public int? Amount { get; init; }
        public DateOnly? CreatedOn { get; init; }
    }
}
