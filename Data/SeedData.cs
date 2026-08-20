using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using InternalCarrierApp.API.Models;

namespace InternalCarrierApp.API.Data;

public static class SeedData
{
    public static readonly string[] Roles =
        ["Admin", "DBAAdmin", "Engineer", "ReadOnly"];

    public static async Task InitializeAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var db = services.GetRequiredService<ApplicationDbContext>();

        // Aplica migraciones pendientes (crea la DB si no existe)
        await db.Database.MigrateAsync();

        // ── Roles ───────────────────────────────────────────────────────────────
        foreach (var role in Roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        // ── Admin user ──────────────────────────────────────────────────────────
        const string adminEmail = "eli.romo@carrier.com";   // ← ajusta a tu correo real
        if (await userManager.FindByEmailAsync(adminEmail) is null)
        {
            var admin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                FullName = "Eli Romo",
                JobTitle = "Tech Lead — Departamento TI",
                IsActive = true,
                EmailConfirmed = true
            };
            var result = await userManager.CreateAsync(admin, "Carrier@2025!");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(admin, "Admin");

                // Assign all plants to admin
                var plants = await db.Plants.ToListAsync();
                foreach (var plant in plants)
                    db.UserPlants.Add(new UserPlant { UserId = admin.Id, PlantId = plant.Id });
                await db.SaveChangesAsync();
            }
        }
    }
}