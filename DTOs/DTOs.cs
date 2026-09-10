using System.ComponentModel.DataAnnotations;

namespace InternalCarrierApp.API.DTOs;

// ── AUTH ──────────────────────────────────────────────────────────────────────

public record LoginDto(
    [Required, EmailAddress] string Email,
    [Required] string Password
);

public record RegisterDto(
    [Required, EmailAddress] string Email,
    [Required, MinLength(8)] string Password,
    [Required, MaxLength(100)] string FullName,
    string? JobTitle,
    [Required] string Role,          // Admin | DBAAdmin | Engineer | ReadOnly
    [Required] List<int> PlantIds
);

public record TokenResponseDto(
    string AccessToken,
    string Email,
    string FullName,
    string Role,
    List<PlantDto> Plants,
    DateTime ExpiresAt
);

// ── PLANT ─────────────────────────────────────────────────────────────────────

public record PlantDto(int Id, string Code, string Name, string? Description);

// ── SERVER ────────────────────────────────────────────────────────────────────

public record ServerDto(
    int Id,
    int PlantId,
    string PlantCode,
    string ServerName,
    string? IpAddress,
    string? ApplicationDescription,
    string? AppName,
    string? Site,
    string? Environment,
    string? InfraType,
    string? ResourceType,
    string? SqlVersion,
    string? OperativeSystem,
    int? RamGb,
    string? CpuQty,
    string? DiskC,
    string? DiskD,
    string? DiskE,
    string? CeNumberServer,
    string? CeNumberSql,
    string? Notes,
    int Priority,
    DateTime UpdatedAt,
    string? LastModifiedBy
);

public sealed class CreateServerDto
{
    [Range(1, int.MaxValue)]
    public int PlantId { get; init; }

    [Required, MaxLength(255)]
    public string ServerName { get; init; } = string.Empty;

    [MaxLength(50)]
    public string? IpAddress { get; init; }

    [MaxLength(200)]
    public string? ApplicationDescription { get; init; }

    [MaxLength(200)]
    public string? AppName { get; init; }

    [MaxLength(30)]
    public string? Site { get; init; }

    [MaxLength(20)]
    public string? Environment { get; init; }

    [MaxLength(20)]
    public string? InfraType { get; init; }

    [MaxLength(30)]
    public string? ResourceType { get; init; }

    [MaxLength(30)]
    public string? SqlVersion { get; init; }

    [MaxLength(80)]
    public string? OperativeSystem { get; init; }

    [Range(0, int.MaxValue)]
    public int? RamGb { get; init; }

    public string? CpuQty { get; init; }
    public string? DiskC { get; init; }
    public string? DiskD { get; init; }
    public string? DiskE { get; init; }

    [MaxLength(20)]
    public string? CeNumberServer { get; init; }

    [MaxLength(20)]
    public string? CeNumberSql { get; init; }

    public string? Notes { get; init; }

    [Range(0, int.MaxValue)]
    public int Priority { get; init; } = 0;
}

public sealed class UpdateServerDto
{
    [Required, MaxLength(255)]
    public string ServerName { get; init; } = string.Empty;

    [MaxLength(50)]
    public string? IpAddress { get; init; }

    [MaxLength(200)]
    public string? ApplicationDescription { get; init; }

    [MaxLength(200)]
    public string? AppName { get; init; }

    [MaxLength(30)]
    public string? Site { get; init; }

    [MaxLength(20)]
    public string? Environment { get; init; }

    [MaxLength(20)]
    public string? InfraType { get; init; }

    [MaxLength(30)]
    public string? ResourceType { get; init; }

    [MaxLength(30)]
    public string? SqlVersion { get; init; }

    [MaxLength(80)]
    public string? OperativeSystem { get; init; }

    [Range(0, int.MaxValue)]
    public int? RamGb { get; init; }

    public string? CpuQty { get; init; }
    public string? DiskC { get; init; }
    public string? DiskD { get; init; }
    public string? DiskE { get; init; }

    [MaxLength(20)]
    public string? CeNumberServer { get; init; }

    [MaxLength(20)]
    public string? CeNumberSql { get; init; }

    public string? Notes { get; init; }

    [Range(0, int.MaxValue)]
    public int Priority { get; init; } = 0;
}

// ── APPLICATION ───────────────────────────────────────────────────────────────

public record ApplicationDto(
    int Id,
    string Name,
    List<string> OwnerIds,
    List<string> OwnerNames,
    string OwnerName,
    int ServerId,
    string ServerName,
    string DatabaseName,
    bool IsActive,
    string Url,
    string? Environment,
    string? Notes,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string? LastModifiedBy
);

public record ApplicationOwnerOption(string Id, string FullName, string Email);

public sealed class ApplicationWriteDto
{
    [Required, MaxLength(150)]
    public string Name { get; init; } = string.Empty;

    [Required, MinLength(1)]
    public List<string> OwnerIds { get; init; } = [];

    [Range(1, int.MaxValue)]
    public int ServerId { get; init; }

    [Required, MaxLength(150)]
    public string DatabaseName { get; init; } = string.Empty;

    public bool IsActive { get; init; } = true;

    [Required, MaxLength(2048), Url]
    public string Url { get; init; } = string.Empty;

    [MaxLength(20)]
    public string? Environment { get; init; }

    [MaxLength(200)]
    public string? Notes { get; init; }
}
