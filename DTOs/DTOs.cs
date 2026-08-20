using System.ComponentModel.DataAnnotations;

namespace InternalCarrierApp.API.DTOs;

// ── AUTH ──────────────────────────────────────────────────────────────────────

public record LoginDto(
    [Required, EmailAddress] string Email,
    [Required]               string Password
);

public record RegisterDto(
    [Required, EmailAddress]         string Email,
    [Required, MinLength(8)]         string Password,
    [Required, MaxLength(100)]       string FullName,
    string?                                 JobTitle,
    [Required]                       string Role,          // Admin | DBAAdmin | Engineer | ReadOnly
    [Required] List<int>                    PlantIds
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
    int      Id,
    int      PlantId,
    string   PlantCode,
    string   ServerName,
    string?  IpAddress,
    string?  ApplicationDescription,
    string?  AppName,
    string?  Site,
    string?  Environment,
    string?  InfraType,
    string?  ResourceType,
    string?  SqlVersion,
    string?  OperativeSystem,
    int?     RamGb,
    string?  CpuQty,
    string?  DiskC,
    string?  DiskD,
    string?  DiskE,
    string?  CeNumberServer,
    string?  CeNumberSql,
    string?  Notes,
    int      Priority,
    DateTime UpdatedAt,
    string?  LastModifiedBy
);

public record CreateServerDto(
    [Required] int    PlantId,
    [Required, MaxLength(60)] string ServerName,
    string?  IpAddress,
    string?  ApplicationDescription,
    string?  AppName,
    string?  Site,
    string?  Environment,
    string?  InfraType,
    string?  ResourceType,
    string?  SqlVersion,
    string?  OperativeSystem,
    int?     RamGb,
    string?  CpuQty,
    string?  DiskC,
    string?  DiskD,
    string?  DiskE,
    string?  CeNumberServer,
    string?  CeNumberSql,
    string?  Notes,
    int      Priority = 0
);

public record UpdateServerDto(
    [Required, MaxLength(60)] string ServerName,
    string?  IpAddress,
    string?  ApplicationDescription,
    string?  AppName,
    string?  Site,
    string?  Environment,
    string?  InfraType,
    string?  ResourceType,
    string?  SqlVersion,
    string?  OperativeSystem,
    int?     RamGb,
    string?  CpuQty,
    string?  DiskC,
    string?  DiskD,
    string?  DiskE,
    string?  CeNumberServer,
    string?  CeNumberSql,
    string?  Notes,
    int      Priority = 0
);
