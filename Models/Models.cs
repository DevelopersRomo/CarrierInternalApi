using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InternalCarrierApp.API.Models;

// ─────────────────────────────────────────────
// PLANT  (A, B, C, D, E, F, G, MTH)
// ─────────────────────────────────────────────
public class Plant
{
    public int Id { get; set; }

    [Required, MaxLength(10)]
    public string Code { get; set; } = string.Empty;   // "A", "MTH", etc.

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;   // "Planta A – SMT"

    public string? Description { get; set; }

    public ICollection<UserPlant> UserPlants { get; set; } = [];
    public ICollection<ServerRecord> Servers { get; set; } = [];
    public ICollection<HardwareInventory> Inventory { get; set; } = [];
}

// ─────────────────────────────────────────────
// APPLICATION USER  (Identity)
// ─────────────────────────────────────────────
public class ApplicationUser : IdentityUser
{
    [MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    public string? JobTitle { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Many-to-many with Plant
    public ICollection<UserPlant> UserPlants { get; set; } = [];
}

// ─────────────────────────────────────────────
// USER ↔ PLANT  (join table)
// ─────────────────────────────────────────────
public class UserPlant
{
    public string UserId { get; set; } = string.Empty;
    public int PlantId { get; set; }

    public ApplicationUser User { get; set; } = null!;
    public Plant Plant { get; set; } = null!;
}

// ─────────────────────────────────────────────
// SERVER RECORD  (inventory row)
// ─────────────────────────────────────────────
public class ServerRecord
{
    public int Id { get; set; }

    [Required]
    public int PlantId { get; set; }
    public Plant Plant { get; set; } = null!;

    [Required, MaxLength(60)]
    public string ServerName { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? IpAddress { get; set; }

    [MaxLength(200)]
    public string? ApplicationDescription { get; set; }

    [MaxLength(200)]
    public string? AppName { get; set; }

    /// <summary>OnPrem | AWS | CMXF PC</summary>
    [MaxLength(30)]
    public string? Site { get; set; }

    /// <summary>PRD | QA | QA/PRD</summary>
    [MaxLength(20)]
    public string? Environment { get; set; }

    /// <summary>Virtual | Physical | PC</summary>
    [MaxLength(20)]
    public string? InfraType { get; set; }

    /// <summary>Apps | SQL | Apps-SQL | File Share | Infra</summary>
    [MaxLength(30)]
    public string? ResourceType { get; set; }

    [MaxLength(30)]
    public string? SqlVersion { get; set; }

    [MaxLength(80)]
    public string? OperativeSystem { get; set; }

    public int? RamGb { get; set; }
    public string? CpuQty { get; set; }
    public string? DiskC { get; set; }
    public string? DiskD { get; set; }
    public string? DiskE { get; set; }

    [MaxLength(20)]
    public string? CeNumberServer { get; set; }

    [MaxLength(20)]
    public string? CeNumberSql { get; set; }

    public string? Notes { get; set; }

    public int Priority { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(256)]
    public string? LastModifiedBy { get; set; }
}
