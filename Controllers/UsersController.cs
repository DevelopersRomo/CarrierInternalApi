using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using InternalCarrierApp.API.Data;
using InternalCarrierApp.API.DTOs;
using InternalCarrierApp.API.Models;

namespace InternalCarrierApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class UsersController(
    UserManager<ApplicationUser> userManager,
    ApplicationDbContext         db) : ControllerBase
{
    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    // GET api/users
    [HttpGet]
    public async Task<ActionResult<IEnumerable<AdminUserDto>>> GetAll()
    {
        var users = await userManager.Users
            .OrderBy(u => u.FullName)
            .ToListAsync();

        // One query for every assignment beats one per user.
        var plantsByUser = await db.UserPlants
            .Include(up => up.Plant)
            .GroupBy(up => up.UserId)
            .ToDictionaryAsync(
                group => group.Key,
                group => group
                    .Select(up => new PlantDto(up.Plant.Id, up.Plant.Code, up.Plant.Name, up.Plant.Description))
                    .OrderBy(p => p.Code)
                    .ToList());

        var result = new List<AdminUserDto>(users.Count);
        foreach (var user in users)
        {
            var roles = await userManager.GetRolesAsync(user);
            result.Add(new AdminUserDto(
                user.Id,
                user.Email!,
                user.FullName,
                user.JobTitle,
                roles.FirstOrDefault() ?? "ReadOnly",
                user.IsActive,
                user.CreatedAt,
                plantsByUser.TryGetValue(user.Id, out var plants) ? plants : []));
        }

        return Ok(result);
    }

    // PUT api/users/{id}/role
    [HttpPut("{id}/role")]
    public async Task<IActionResult> UpdateRole(string id, [FromBody] UpdateUserRoleDto dto)
    {
        if (!SeedData.Roles.Contains(dto.Role))
            return BadRequest(new { message = $"Rol inválido. Opciones: {string.Join(", ", SeedData.Roles)}" });

        var user = await userManager.FindByIdAsync(id);
        if (user is null) return NotFound();

        // Losing your own Admin role locks you out of this screen mid-session.
        if (id == CurrentUserId && dto.Role != "Admin")
            return BadRequest(new { message = "No puedes quitarte tu propio rol de Admin." });

        if (dto.Role != "Admin" && await IsLastActiveAdmin(user))
            return BadRequest(new { message = "No puedes cambiar el rol del último administrador activo." });

        var currentRoles = await userManager.GetRolesAsync(user);
        if (currentRoles.Count > 0)
        {
            var removed = await userManager.RemoveFromRolesAsync(user, currentRoles);
            if (!removed.Succeeded)
                return BadRequest(new { errors = removed.Errors.Select(e => e.Description) });
        }

        var added = await userManager.AddToRoleAsync(user, dto.Role);
        if (!added.Succeeded)
            return BadRequest(new { errors = added.Errors.Select(e => e.Description) });

        return NoContent();
    }

    // PUT api/users/{id}/plants
    [HttpPut("{id}/plants")]
    public async Task<IActionResult> UpdatePlants(string id, [FromBody] UpdateUserPlantsDto dto)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null) return NotFound();

        var requested = dto.PlantIds.Distinct().ToList();
        var validIds  = await db.Plants
            .Where(p => requested.Contains(p.Id))
            .Select(p => p.Id)
            .ToListAsync();

        var unknown = requested.Except(validIds).ToList();
        if (unknown.Count > 0)
            return BadRequest(new { message = $"Plantas inexistentes: {string.Join(", ", unknown)}." });

        var existing = await db.UserPlants.Where(up => up.UserId == id).ToListAsync();
        db.UserPlants.RemoveRange(existing);

        foreach (var plantId in validIds)
            db.UserPlants.Add(new UserPlant { UserId = id, PlantId = plantId });

        await db.SaveChangesAsync();
        return NoContent();
    }

    // PUT api/users/{id}/status
    [HttpPut("{id}/status")]
    public async Task<IActionResult> UpdateStatus(string id, [FromBody] UpdateUserStatusDto dto)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null) return NotFound();

        if (!dto.IsActive)
        {
            if (id == CurrentUserId)
                return BadRequest(new { message = "No puedes desactivar tu propia cuenta." });

            if (await IsLastActiveAdmin(user))
                return BadRequest(new { message = "No puedes desactivar al último administrador activo." });
        }

        user.IsActive = dto.IsActive;
        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });

        return NoContent();
    }

    /// <summary>
    /// True when <paramref name="user"/> is an active Admin and no other active Admin
    /// remains. Guards against an admin-less system.
    /// </summary>
    private async Task<bool> IsLastActiveAdmin(ApplicationUser user)
    {
        if (!user.IsActive) return false;
        if (!await userManager.IsInRoleAsync(user, "Admin")) return false;

        var admins = await userManager.GetUsersInRoleAsync("Admin");
        return admins.Count(a => a.IsActive) <= 1;
    }
}
