using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using InternalCarrierApp.API.Models;

namespace InternalCarrierApp.API.Data;

public static class SeedData
{
    public static readonly string[] Roles =
        ["Admin", "DBAAdmin", "Engineer", "ReadOnly"];

    /// <summary>
    /// A user to create on startup. Bound from configuration so credentials live in
    /// appsettings.Development.json (git-ignored) instead of in tracked source.
    /// </summary>
    private sealed class SeedUser
    {
        public string? Email { get; set; }
        public string? Password { get; set; }
        public string? FullName { get; set; }
        public string? JobTitle { get; set; }
        public string? Role { get; set; }

        /// <summary>"all", "none", or a list of plant codes such as ["A", "B"].</summary>
        public string[]? Plants { get; set; }
    }

    public static async Task InitializeAsync(IServiceProvider services)
    {
        var roleManager   = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager   = services.GetRequiredService<UserManager<ApplicationUser>>();
        var db            = services.GetRequiredService<ApplicationDbContext>();
        var configuration = services.GetRequiredService<IConfiguration>();
        var logger        = services.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(SeedData));

        // Aplica migraciones pendientes (crea la DB si no existe)
        await db.Database.MigrateAsync();

        // ── Roles ───────────────────────────────────────────────────────────────
        foreach (var role in Roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        // ── Users ───────────────────────────────────────────────────────────────
        var seedUsers = configuration.GetSection("SeedUsers").Get<List<SeedUser>>() ?? [];

        // Backwards compatible with the previous single-admin shape.
        var legacyAdmin = configuration.GetSection("SeedAdmin").Get<SeedUser>();
        if (legacyAdmin?.Email is not null)
        {
            legacyAdmin.Role ??= "Admin";
            legacyAdmin.FullName ??= "System Administrator";
            legacyAdmin.Plants ??= ["all"];
            seedUsers.Add(legacyAdmin);
        }

        if (seedUsers.Count == 0) return;

        var plants = await db.Plants.ToListAsync();

        foreach (var seed in seedUsers)
        {
            if (string.IsNullOrWhiteSpace(seed.Email) || string.IsNullOrWhiteSpace(seed.Password))
                throw new InvalidOperationException("Every SeedUsers entry needs both Email and Password.");

            var role = string.IsNullOrWhiteSpace(seed.Role) ? "ReadOnly" : seed.Role;
            if (!Roles.Contains(role))
                throw new InvalidOperationException(
                    $"SeedUsers entry '{seed.Email}' has invalid role '{role}'. Options: {string.Join(", ", Roles)}.");

            // An existing account keeps its password — restarting the API must never
            // reset a credential someone changed later. Role and plants are different:
            // this config declares the intended access, so reconcile them. Otherwise a
            // seed entry silently does nothing when the account predates it.
            var existing = await userManager.FindByEmailAsync(seed.Email);
            if (existing is not null)
            {
                await ReconcileAccessAsync(userManager, db, existing, role, seed.Plants, plants, logger);
                continue;
            }

            var user = new ApplicationUser
            {
                UserName       = seed.Email,
                Email          = seed.Email,
                FullName       = string.IsNullOrWhiteSpace(seed.FullName) ? seed.Email : seed.FullName,
                JobTitle       = seed.JobTitle,
                IsActive       = true,
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(user, seed.Password);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Could not create seed user '{seed.Email}': {string.Join(" ", result.Errors.Select(e => e.Description))}");
            }

            await userManager.AddToRoleAsync(user, role);

            foreach (var plant in ResolvePlants(seed.Plants, plants))
                db.UserPlants.Add(new UserPlant { UserId = user.Id, PlantId = plant.Id });

            await db.SaveChangesAsync();
            logger.LogInformation("Seeded user {Email} with role {Role}.", seed.Email, role);
        }
    }

    /// <summary>
    /// Brings an existing account's role and plant assignments in line with the seed
    /// entry. The password is deliberately left alone.
    /// </summary>
    private static async Task ReconcileAccessAsync(
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext         db,
        ApplicationUser              user,
        string                       role,
        string[]?                    plantSelector,
        List<Plant>                  allPlants,
        ILogger                      logger)
    {
        var currentRoles = await userManager.GetRolesAsync(user);
        if (!currentRoles.Contains(role))
        {
            if (currentRoles.Count > 0)
                await userManager.RemoveFromRolesAsync(user, currentRoles);
            await userManager.AddToRoleAsync(user, role);
            logger.LogInformation(
                "Seed user {Email}: role {Old} -> {New}.",
                user.Email, currentRoles.FirstOrDefault() ?? "(none)", role);
        }

        if (!user.IsActive)
        {
            user.IsActive = true;
            await userManager.UpdateAsync(user);
            logger.LogInformation("Seed user {Email} reactivated.", user.Email);
        }

        var desired  = ResolvePlants(plantSelector, allPlants).Select(p => p.Id).ToHashSet();
        var assigned = await db.UserPlants.Where(up => up.UserId == user.Id).ToListAsync();

        if (assigned.Select(up => up.PlantId).ToHashSet().SetEquals(desired)) return;

        db.UserPlants.RemoveRange(assigned);
        foreach (var plantId in desired)
            db.UserPlants.Add(new UserPlant { UserId = user.Id, PlantId = plantId });

        await db.SaveChangesAsync();
        logger.LogInformation(
            "Seed user {Email}: plant assignments set to {Count}.", user.Email, desired.Count);
    }

    /// <summary>Resolves the configured plant selector against the plants that exist.</summary>
    private static IEnumerable<Plant> ResolvePlants(string[]? selector, List<Plant> allPlants)
    {
        if (selector is null || selector.Length == 0) return [];
        if (selector.Contains("all", StringComparer.OrdinalIgnoreCase)) return allPlants;
        if (selector.Contains("none", StringComparer.OrdinalIgnoreCase)) return [];

        return allPlants.Where(plant =>
            selector.Contains(plant.Code, StringComparer.OrdinalIgnoreCase));
    }
}
