using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using InternalCarrierApp.API.Models;

namespace InternalCarrierApp.API.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Plant> Plants { get; set; }
    public DbSet<UserPlant> UserPlants { get; set; }
    public DbSet<ServerRecord> Servers { get; set; }
    public DbSet<HardwareInventory> Inventory { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // UserPlant composite PK
        builder.Entity<UserPlant>()
            .HasKey(up => new { up.UserId, up.PlantId });

        builder.Entity<UserPlant>()
            .HasOne(up => up.User)
            .WithMany(u => u.UserPlants)
            .HasForeignKey(up => up.UserId);

        builder.Entity<UserPlant>()
            .HasOne(up => up.Plant)
            .WithMany(p => p.UserPlants)
            .HasForeignKey(up => up.PlantId);

        // Plant unique code
        builder.Entity<Plant>()
            .HasIndex(p => p.Code)
            .IsUnique();

        // Server → Plant
        builder.Entity<ServerRecord>()
            .HasOne(s => s.Plant)
            .WithMany(p => p.Servers)
            .HasForeignKey(s => s.PlantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<HardwareInventory>()
            .HasOne(item => item.Plant)
            .WithMany(plant => plant.Inventory)
            .HasForeignKey(item => item.PlantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<HardwareInventory>()
            .HasIndex(item => item.SerialNumber)
            .IsUnique();

        builder.Entity<HardwareInventory>()
            .ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_Inventory_DeviceType",
                    "[DeviceType] IS NULL OR [DeviceType] IN ('Laptop','Desktop','Workstation','Server')");
                table.HasCheckConstraint(
                    "CK_Inventory_StorageType",
                    "[StorageType] IS NULL OR [StorageType] IN ('HDD','SSD','NVMe','Hybrid')");
                table.HasCheckConstraint(
                    "CK_Inventory_Status",
                    "[Status] IN ('Active','In Repair','Retired','Spare')");
                table.HasCheckConstraint("CK_Inventory_RamGb", "[RamGb] IS NULL OR [RamGb] >= 0");
                table.HasCheckConstraint(
                    "CK_Inventory_StorageSizeGb",
                    "[StorageSizeGb] IS NULL OR [StorageSizeGb] >= 0");
                table.HasCheckConstraint(
                    "CK_Inventory_WarrantyDates",
                    "[WarrantyStartDate] IS NULL OR [WarrantyEndDate] IS NULL OR [WarrantyEndDate] >= [WarrantyStartDate]");
            });

        // Seed plants
        builder.Entity<Plant>().HasData(
            new Plant { Id = 1, Code = "A", Name = "Planta A", Description = "Planta de producción A" },
            new Plant { Id = 2, Code = "B", Name = "Planta B", Description = "Planta de producción B" },
            new Plant { Id = 3, Code = "C", Name = "Planta C", Description = "Planta de producción C" },
            new Plant { Id = 4, Code = "D", Name = "Planta D", Description = "Planta de producción D" },
            new Plant { Id = 5, Code = "E", Name = "Planta E", Description = "Planta de producción E" },
            new Plant { Id = 6, Code = "F", Name = "Planta F", Description = "Planta de producción F" },
            new Plant { Id = 7, Code = "G", Name = "Planta G", Description = "Planta de producción G" },
            new Plant { Id = 8, Code = "MTH", Name = "Planta MTH", Description = "Monterrey Hub" }
        );
    }
}
