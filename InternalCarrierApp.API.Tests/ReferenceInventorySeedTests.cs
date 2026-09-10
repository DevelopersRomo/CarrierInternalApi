using InternalCarrierApp.API.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace InternalCarrierApp.API.Tests;

public sealed class ReferenceInventorySeedTests
{
    [Fact]
    public async Task PlantSeed_CreatesMinimalUtecPlantOnlyOnce()
    {
        await using var db = CreateDbContext();

        await PlantSeedData.InitializeAsync(db, NullLogger.Instance);
        await PlantSeedData.InitializeAsync(db, NullLogger.Instance);

        var plants = await db.Plants
            .Where(plant => plant.Code == PlantSeedData.UtecCode)
            .ToListAsync();

        var plant = Assert.Single(plants);
        Assert.Equal("UTEC", plant.Name);
        Assert.Null(plant.Description);
    }

    [Fact]
    public async Task ServerSeed_CreatesAllInventoryRowsWithExactPlantMappingAndIsIdempotent()
    {
        await using var db = CreateDbContext();
        await db.Database.EnsureCreatedAsync();
        await PlantSeedData.InitializeAsync(db, NullLogger.Instance);

        await ServerSeedData.InitializeAsync(db, NullLogger.Instance);
        await ServerSeedData.InitializeAsync(db, NullLogger.Instance);

        var servers = await db.Servers
            .Include(server => server.Plant)
            .ToListAsync();

        Assert.Equal(47, ServerSeedData.ServerNames.Count);
        Assert.Equal(ServerSeedData.ServerNames.Count, servers.Count);
        Assert.Equal(
            ServerSeedData.ServerNames.Order(StringComparer.OrdinalIgnoreCase),
            servers.Select(server => server.ServerName).Order(StringComparer.OrdinalIgnoreCase));

        Assert.Collection(
            servers.GroupBy(server => server.Plant.Code)
                .OrderBy(group => group.Key)
                .Select(group => (group.Key, Count: group.Count())),
            group => Assert.Equal(("A", 6), group),
            group => Assert.Equal(("F", 1), group),
            group => Assert.Equal(("G", 4), group),
            group => Assert.Equal(("UTEC", 36), group));

        var rdsServer = Assert.Single(servers, server => server.Priority == 43);
        Assert.Equal("A", rdsServer.Plant.Code);
        Assert.Equal(
            "sc-601242391086-pp-dkvfynpogsu42-mssqldb-uxdowvwq8pcy.cshbmfldesup.us-east-1.rds.amazonaws.com",
            rdsServer.ServerName);

        Assert.Equal(2, servers.Count(server => server.Plant.Code == "G" && server.Priority == 0));
        Assert.Equal(
            "SAP EDICOM Facturación",
            Assert.Single(servers, server => server.Priority == 16).AppName);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }
}
