using InternalCarrierApp.API.Models;
using Microsoft.EntityFrameworkCore;

namespace InternalCarrierApp.API.Data;

public static class PlantSeedData
{
    public const string UtecCode = "UTEC";

    public static async Task InitializeAsync(
        ApplicationDbContext db,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        if (await db.Plants.AnyAsync(
                plant => plant.Code == UtecCode,
                cancellationToken))
        {
            return;
        }

        db.Plants.Add(new Plant
        {
            Code = UtecCode,
            Name = UtecCode
        });

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seeded plant {PlantCode}.", UtecCode);
    }
}
