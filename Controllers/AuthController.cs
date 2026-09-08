using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InternalCarrierApp.API.Data;
using InternalCarrierApp.API.DTOs;
using InternalCarrierApp.API.Models;
using InternalCarrierApp.API.Services;

namespace InternalCarrierApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(
    UserManager<ApplicationUser>  userManager,
    ApplicationDbContext          db,
    ITokenService                 tokenService) : ControllerBase
{
    // POST api/auth/login
    [HttpPost("login"), EnableRateLimiting("auth")]
    public async Task<ActionResult<TokenResponseDto>> Login([FromBody] LoginDto dto)
    {
        // One message for every failure mode, so the response never reveals whether
        // the address exists, is inactive, or simply had the wrong password.
        ActionResult<TokenResponseDto> InvalidCredentials() =>
            Unauthorized(new { message = "Credenciales inválidas." });

        var user = await userManager.FindByEmailAsync(dto.Email);
        if (user is null || !user.IsActive)
            return InvalidCredentials();

        if (await userManager.IsLockedOutAsync(user))
            return InvalidCredentials();

        if (!await userManager.CheckPasswordAsync(user, dto.Password))
        {
            // Configuring lockout is not enough: the counter only moves if the
            // failure is recorded here.
            await userManager.AccessFailedAsync(user);
            return InvalidCredentials();
        }

        await userManager.ResetAccessFailedCountAsync(user);

        var roles = await userManager.GetRolesAsync(user);
        var role  = roles.FirstOrDefault() ?? "ReadOnly";

        var plants = await db.UserPlants
            .Where(up => up.UserId == user.Id)
            .Include(up => up.Plant)
            .Select(up => new PlantDto(up.Plant.Id, up.Plant.Code, up.Plant.Name, up.Plant.Description))
            .ToListAsync();

        var (token, expires) = tokenService.GenerateToken(
            user, role, plants.Select(p => p.Code), await userManager.GetSecurityStampAsync(user));

        return Ok(new TokenResponseDto(token, user.Email!, user.FullName, role, plants, expires));
    }

    // POST api/auth/register  [Admin only]
    [HttpPost("register"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)
    {
        if (!SeedData.Roles.Contains(dto.Role))
            return BadRequest(new { message = $"Rol inválido. Opciones: {string.Join(", ", SeedData.Roles)}" });

        var user = new ApplicationUser
        {
            UserName  = dto.Email,
            Email     = dto.Email,
            FullName  = dto.FullName,
            JobTitle  = dto.JobTitle,
            EmailConfirmed = true,
            IsActive  = true
        };

        var result = await userManager.CreateAsync(user, dto.Password);
        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });

        await userManager.AddToRoleAsync(user, dto.Role);

        // Assign plants
        var validPlants = await db.Plants
            .Where(p => dto.PlantIds.Contains(p.Id))
            .ToListAsync();

        foreach (var plant in validPlants)
            db.UserPlants.Add(new UserPlant { UserId = user.Id, PlantId = plant.Id });

        await db.SaveChangesAsync();

        return Ok(new { message = $"Usuario {dto.Email} creado con rol {dto.Role}." });
    }

    // POST api/auth/register-request  [anonymous]
    // Self-service sign-up. The account is created with the lowest role, no plants
    // and IsActive = false, so Login keeps rejecting it until an admin grants access.
    [HttpPost("register-request"), AllowAnonymous, EnableRateLimiting("auth")]
    public async Task<IActionResult> RegisterRequest([FromBody] RegistrationRequestDto dto)
    {
        // Same body and status whatever happens, so this endpoint cannot be used to
        // discover which emails already have an account.
        IActionResult Accepted() => Ok(new
        {
            message = "Solicitud recibida. Un administrador debe activar tu cuenta antes del primer acceso."
        });

        var email = dto.Email.Trim();

        if (await userManager.FindByEmailAsync(email) is not null)
            return Accepted();

        var user = new ApplicationUser
        {
            UserName       = email,
            Email          = email,
            FullName       = dto.FullName.Trim(),
            JobTitle       = string.IsNullOrWhiteSpace(dto.JobTitle) ? null : dto.JobTitle.Trim(),
            EmailConfirmed = true,
            IsActive       = false
        };

        var result = await userManager.CreateAsync(user, dto.Password);
        if (!result.Succeeded)
        {
            // Password-policy failures are about the caller's own input, not about
            // existing accounts, so they are safe (and necessary) to report.
            var passwordErrors = result.Errors
                .Where(e => e.Code.StartsWith("Password", StringComparison.Ordinal))
                .Select(e => e.Description)
                .ToList();

            return passwordErrors.Count > 0
                ? BadRequest(new { errors = passwordErrors })
                : Accepted();
        }

        await userManager.AddToRoleAsync(user, "ReadOnly");
        return Accepted();
    }

    // GET api/auth/me
    [HttpGet("me"), Authorize]
    public async Task<ActionResult<TokenResponseDto>> Me()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var user   = await userManager.FindByIdAsync(userId!);
        if (user is null) return NotFound();

        var roles  = await userManager.GetRolesAsync(user);
        var role   = roles.FirstOrDefault() ?? "ReadOnly";
        var plants = await db.UserPlants
            .Where(up => up.UserId == user.Id)
            .Include(up => up.Plant)
            .Select(up => new PlantDto(up.Plant.Id, up.Plant.Code, up.Plant.Name, up.Plant.Description))
            .ToListAsync();

        var (token, expires) = tokenService.GenerateToken(
            user, role, plants.Select(p => p.Code), await userManager.GetSecurityStampAsync(user));
        return Ok(new TokenResponseDto(token, user.Email!, user.FullName, role, plants, expires));
    }
}
