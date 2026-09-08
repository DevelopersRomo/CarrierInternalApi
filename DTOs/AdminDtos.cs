using System.ComponentModel.DataAnnotations;

namespace InternalCarrierApp.API.DTOs;

// ── SELF-SERVICE REGISTRATION ─────────────────────────────────────────────────

/// <summary>
/// Anonymous sign-up request. The caller never chooses a role or plants: the
/// account is created inactive with the lowest role and an admin grants access.
/// </summary>
public record RegistrationRequestDto(
    [Required, EmailAddress, MaxLength(256)] string Email,
    [Required, MinLength(8)] string Password,
    [Required, MaxLength(100)] string FullName,
    [MaxLength(100)] string? JobTitle
);

// ── USER ADMINISTRATION ───────────────────────────────────────────────────────

public record AdminUserDto(
    string Id,
    string Email,
    string FullName,
    string? JobTitle,
    string Role,
    bool IsActive,
    DateTime CreatedAt,
    List<PlantDto> Plants
);

public record UpdateUserRoleDto(
    [Required] string Role
);

public record UpdateUserPlantsDto(
    [Required] List<int> PlantIds
);

public record UpdateUserStatusDto(
    [Required] bool IsActive
);

// ── PLANT ADMINISTRATION ──────────────────────────────────────────────────────

public record CreatePlantDto(
    [property: Required, MaxLength(10)] string Code,
    [property: Required, MaxLength(100)] string Name,
    [property: MaxLength(255)] string? Description
);

public record UpdatePlantDto(
    [property: Required, MaxLength(10)] string Code,
    [property: Required, MaxLength(100)] string Name,
    [property: MaxLength(255)] string? Description
);

/// <summary>Plant plus the reference counts that decide whether it can be deleted.</summary>
public record PlantDetailDto(
    int Id,
    string Code,
    string Name,
    string? Description,
    int ServerCount,
    int InventoryCount,
    int UserCount
);
