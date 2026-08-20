using Microsoft.AspNetCore.Authorization;
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
    [HttpPost("login")]
    public async Task<ActionResult<TokenResponseDto>> Login([FromBody] LoginDto dto)
    {
        var user = await userManager.FindByEmailAsync(dto.Email);
        if (user is null || !user.IsActive)
            return Unauthorized(new { message = "Credenciales inválidas." });

        if (!await userManager.CheckPasswordAsync(user, dto.Password))
            return Unauthorized(new { message = "Credenciales inválidas." });

        var roles = await userManager.GetRolesAsync(user);
        var role  = roles.FirstOrDefault() ?? "ReadOnly";

        var plants = await db.UserPlants
            .Where(up => up.UserId == user.Id)
            .Include(up => up.Plant)
            .Select(up => new PlantDto(up.Plant.Id, up.Plant.Code, up.Plant.Name, up.Plant.Description))
            .ToListAsync();

        var (token, expires) = tokenService.GenerateToken(user, role, plants.Select(p => p.Code));

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

        var (token, expires) = tokenService.GenerateToken(user, role, plants.Select(p => p.Code));
        return Ok(new TokenResponseDto(token, user.Email!, user.FullName, role, plants, expires));
    }
}
