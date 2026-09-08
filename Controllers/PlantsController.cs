using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using InternalCarrierApp.API.Data;
using InternalCarrierApp.API.DTOs;
using InternalCarrierApp.API.Models;

namespace InternalCarrierApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PlantsController(ApplicationDbContext db) : ControllerBase
{
    // GET api/plants  — returns only plants the current user can access
    [HttpGet]
    public async Task<ActionResult<IEnumerable<PlantDto>>> GetMyPlants()
    {
        var userId  = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var isAdmin = User.IsInRole("Admin");

        // Order before projecting: EF cannot translate an OrderBy over an already
        // constructed PlantDto, which made this throw for every non-admin user.
        IQueryable<PlantDto> query;

        if (isAdmin)
        {
            query = db.Plants
                .OrderBy(p => p.Code)
                .Select(p => new PlantDto(p.Id, p.Code, p.Name, p.Description));
        }
        else
        {
            query = db.UserPlants
                .Where(up => up.UserId == userId)
                .OrderBy(up => up.Plant.Code)
                .Select(up =>
                    new PlantDto(up.Plant.Id, up.Plant.Code, up.Plant.Name, up.Plant.Description));
        }

        return Ok(await query.ToListAsync());
    }

    // GET api/plants/all  — Admin: full list
    [HttpGet("all"), Authorize(Roles = "Admin")]
    public async Task<ActionResult<IEnumerable<PlantDto>>> GetAll() =>
        Ok(await db.Plants
            .OrderBy(p => p.Code)
            .Select(p => new PlantDto(p.Id, p.Code, p.Name, p.Description))
            .ToListAsync());

    // GET api/plants/detail  — Admin: full list plus the reference counts the UI
    // needs to explain why a plant cannot be deleted.
    [HttpGet("detail"), Authorize(Roles = "Admin")]
    public async Task<ActionResult<IEnumerable<PlantDetailDto>>> GetDetail() =>
        Ok(await db.Plants
            .OrderBy(p => p.Code)
            .Select(p => new PlantDetailDto(
                p.Id,
                p.Code,
                p.Name,
                p.Description,
                p.Servers.Count,
                p.Inventory.Count,
                p.UserPlants.Count))
            .ToListAsync());

    // POST api/plants  — Admin
    [HttpPost, Authorize(Roles = "Admin")]
    public async Task<ActionResult<PlantDto>> Create([FromBody] CreatePlantDto dto)
    {
        var code = dto.Code.Trim();
        var name = dto.Name.Trim();

        if (await db.Plants.AnyAsync(p => p.Code == code))
            return Conflict(new { message = $"Ya existe una planta con el código {code}." });

        var plant = new Plant
        {
            Code        = code,
            Name        = name,
            Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim()
        };

        db.Plants.Add(plant);
        await db.SaveChangesAsync();

        var created = new PlantDto(plant.Id, plant.Code, plant.Name, plant.Description);
        return CreatedAtAction(nameof(GetAll), new { id = plant.Id }, created);
    }

    // PUT api/plants/{id}  — Admin
    [HttpPut("{id:int}"), Authorize(Roles = "Admin")]
    public async Task<ActionResult<PlantDto>> Update(int id, [FromBody] UpdatePlantDto dto)
    {
        var plant = await db.Plants.FirstOrDefaultAsync(p => p.Id == id);
        if (plant is null) return NotFound();

        var code = dto.Code.Trim();

        if (await db.Plants.AnyAsync(p => p.Id != id && p.Code == code))
            return Conflict(new { message = $"Ya existe una planta con el código {code}." });

        plant.Code        = code;
        plant.Name        = dto.Name.Trim();
        plant.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();

        await db.SaveChangesAsync();

        return Ok(new PlantDto(plant.Id, plant.Code, plant.Name, plant.Description));
    }

    // DELETE api/plants/{id}  — Admin
    // Servers and inventory use DeleteBehavior.Restrict, so a blind delete would
    // surface as a raw FK error. Report what is blocking instead.
    [HttpDelete("{id:int}"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var plant = await db.Plants
            .Include(p => p.UserPlants)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (plant is null) return NotFound();

        var serverCount    = await db.Servers.CountAsync(s => s.PlantId == id);
        var inventoryCount = await db.Inventory.CountAsync(item => item.PlantId == id);

        if (serverCount > 0 || inventoryCount > 0)
        {
            var blockers = new List<string>();
            if (serverCount > 0) blockers.Add($"{serverCount} servidor(es)");
            if (inventoryCount > 0) blockers.Add($"{inventoryCount} activo(s) de inventario");

            return Conflict(new
            {
                message = $"No se puede eliminar la planta {plant.Code}: tiene {string.Join(" y ", blockers)} asociados.",
                serverCount,
                inventoryCount
            });
        }

        // User assignments are pure links, so they are removed with the plant.
        db.UserPlants.RemoveRange(plant.UserPlants);
        db.Plants.Remove(plant);
        await db.SaveChangesAsync();

        return NoContent();
    }
}
