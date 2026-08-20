using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using InternalCarrierApp.API.Data;
using InternalCarrierApp.API.DTOs;

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

        IQueryable<PlantDto> query;

        if (isAdmin)
        {
            query = db.Plants.Select(p =>
                new PlantDto(p.Id, p.Code, p.Name, p.Description));
        }
        else
        {
            query = db.UserPlants
                .Where(up => up.UserId == userId)
                .Select(up =>
                    new PlantDto(up.Plant.Id, up.Plant.Code, up.Plant.Name, up.Plant.Description));
        }

        return Ok(await query.OrderBy(p => p.Code).ToListAsync());
    }

    // GET api/plants/all  — Admin: full list
    [HttpGet("all"), Authorize(Roles = "Admin")]
    public async Task<ActionResult<IEnumerable<PlantDto>>> GetAll() =>
        Ok(await db.Plants
            .OrderBy(p => p.Code)
            .Select(p => new PlantDto(p.Id, p.Code, p.Name, p.Description))
            .ToListAsync());
}
