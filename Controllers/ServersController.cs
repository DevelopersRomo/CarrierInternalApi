using ClosedXML.Excel;
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
public class ServersController(ApplicationDbContext db) : ControllerBase
{
    // ── GET MY PLANTS FILTER ───────────────────────────────────────────────────
    private async Task<List<int>> GetUserPlantIdsAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var isAdmin = User.IsInRole("Admin");

        if (isAdmin)
            return await db.Plants.Select(p => p.Id).ToListAsync();

        return await db.UserPlants
            .Where(up => up.UserId == userId)
            .Select(up => up.PlantId)
            .ToListAsync();
    }

    // ── GET api/servers ────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ServerDto>>> GetAll(
        [FromQuery] int?    plantId,
        [FromQuery] string? environment,
        [FromQuery] string? resourceType,
        [FromQuery] string? search)
    {
        var allowedPlants = await GetUserPlantIdsAsync();

        var query = db.Servers
            .Include(s => s.Plant)
            .Where(s => allowedPlants.Contains(s.PlantId))
            .AsQueryable();

        if (plantId.HasValue)        query = query.Where(s => s.PlantId == plantId);
        if (!string.IsNullOrEmpty(environment))   query = query.Where(s => s.Environment == environment);
        if (!string.IsNullOrEmpty(resourceType))  query = query.Where(s => s.ResourceType == resourceType);
        if (!string.IsNullOrEmpty(search))
            query = query.Where(s =>
                s.ServerName.Contains(search) ||
                (s.IpAddress != null && s.IpAddress.Contains(search)) ||
                (s.ApplicationDescription != null && s.ApplicationDescription.Contains(search)));

        var result = await query
            .OrderBy(s => s.Plant.Code)
            .ThenBy(s => s.Priority)
            .Select(s => ToDto(s))
            .ToListAsync();

        return Ok(result);
    }

    // ── GET api/servers/{id} ───────────────────────────────────────────────────
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ServerDto>> GetById(int id)
    {
        var allowed = await GetUserPlantIdsAsync();
        var server  = await db.Servers.Include(s => s.Plant).FirstOrDefaultAsync(s => s.Id == id);

        if (server is null || !allowed.Contains(server.PlantId))
            return NotFound();

        return Ok(ToDto(server));
    }

    // ── POST api/servers ───────────────────────────────────────────────────────
    [HttpPost, Authorize(Roles = "Admin,DBAAdmin,Engineer")]
    public async Task<ActionResult<ServerDto>> Create([FromBody] CreateServerDto dto)
    {
        var allowed = await GetUserPlantIdsAsync();
        if (!allowed.Contains(dto.PlantId))
            return Forbid();

        var server = new ServerRecord
        {
            PlantId                = dto.PlantId,
            ServerName             = dto.ServerName,
            IpAddress              = dto.IpAddress,
            ApplicationDescription = dto.ApplicationDescription,
            AppName                = dto.AppName,
            Site                   = dto.Site,
            Environment            = dto.Environment,
            InfraType              = dto.InfraType,
            ResourceType           = dto.ResourceType,
            SqlVersion             = dto.SqlVersion,
            OperativeSystem        = dto.OperativeSystem,
            RamGb                  = dto.RamGb,
            CpuQty                 = dto.CpuQty,
            DiskC                  = dto.DiskC,
            DiskD                  = dto.DiskD,
            DiskE                  = dto.DiskE,
            CeNumberServer         = dto.CeNumberServer,
            CeNumberSql            = dto.CeNumberSql,
            Notes                  = dto.Notes,
            Priority               = dto.Priority,
            LastModifiedBy         = User.FindFirstValue(ClaimTypes.Email)
        };

        db.Servers.Add(server);
        await db.SaveChangesAsync();
        await db.Entry(server).Reference(s => s.Plant).LoadAsync();

        return CreatedAtAction(nameof(GetById), new { id = server.Id }, ToDto(server));
    }

    // ── PUT api/servers/{id} ───────────────────────────────────────────────────
    [HttpPut("{id:int}"), Authorize(Roles = "Admin,DBAAdmin,Engineer")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateServerDto dto)
    {
        var allowed = await GetUserPlantIdsAsync();
        var server  = await db.Servers.FindAsync(id);

        if (server is null || !allowed.Contains(server.PlantId))
            return NotFound();

        server.ServerName             = dto.ServerName;
        server.IpAddress              = dto.IpAddress;
        server.ApplicationDescription = dto.ApplicationDescription;
        server.AppName                = dto.AppName;
        server.Site                   = dto.Site;
        server.Environment            = dto.Environment;
        server.InfraType              = dto.InfraType;
        server.ResourceType           = dto.ResourceType;
        server.SqlVersion             = dto.SqlVersion;
        server.OperativeSystem        = dto.OperativeSystem;
        server.RamGb                  = dto.RamGb;
        server.CpuQty                 = dto.CpuQty;
        server.DiskC                  = dto.DiskC;
        server.DiskD                  = dto.DiskD;
        server.DiskE                  = dto.DiskE;
        server.CeNumberServer         = dto.CeNumberServer;
        server.CeNumberSql            = dto.CeNumberSql;
        server.Notes                  = dto.Notes;
        server.Priority               = dto.Priority;
        server.UpdatedAt              = DateTime.UtcNow;
        server.LastModifiedBy         = User.FindFirstValue(ClaimTypes.Email);

        await db.SaveChangesAsync();
        return NoContent();
    }

    // ── DELETE api/servers/{id} ────────────────────────────────────────────────
    [HttpDelete("{id:int}"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var server = await db.Servers.FindAsync(id);
        if (server is null) return NotFound();

        db.Servers.Remove(server);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ── GET api/servers/export  (Excel) ───────────────────────────────────────
    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] int? plantId)
    {
        var allowed = await GetUserPlantIdsAsync();
        var query   = db.Servers.Include(s => s.Plant)
            .Where(s => allowed.Contains(s.PlantId));

        if (plantId.HasValue) query = query.Where(s => s.PlantId == plantId);

        var servers = await query.OrderBy(s => s.Plant.Code).ThenBy(s => s.Priority).ToListAsync();

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Inventario Servidores");

        // Header
        ws.Cell(1, 1).Value = "INVENTARIO DE SERVIDORES — CARRIER MÉXICO";
        ws.Range(1, 1, 1, 18).Merge()
            .Style.Font.SetBold(true).Font.SetFontSize(14)
            .Fill.SetBackgroundColor(XLColor.FromHtml("#1B4F8A"))
            .Font.SetFontColor(XLColor.White)
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
        ws.Row(1).Height = 30;

        // Columns
        string[] headers = [
            "PRIOR","Planta","Servidor","IP","Aplicación","App Name",
            "Site","Ambiente","Infra","Recurso","SQL Vrs","SO","RAM (GB)",
            "CPU","Disco C","Disco D","Disco E","CE# Servidor"
        ];
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(2, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.SetBold(true)
                .Fill.SetBackgroundColor(XLColor.FromHtml("#2E75B6"))
                .Font.SetFontColor(XLColor.White)
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
        }
        ws.Row(2).Height = 22;

        // Data
        int row = 3;
        foreach (var s in servers)
        {
            var bgColor = (s.Environment?.Contains("QA") ?? false)
                ? XLColor.FromHtml("#FFF2CC")
                : (s.ResourceType?.Contains("SQL") ?? false)
                    ? XLColor.FromHtml("#D6E4F0")
                    : XLColor.White;

            object[] vals = [
                s.Priority, s.Plant.Code, s.ServerName, s.IpAddress ?? "",
                s.ApplicationDescription ?? "", s.AppName ?? "",
                s.Site ?? "", s.Environment ?? "", s.InfraType ?? "",
                s.ResourceType ?? "", s.SqlVersion ?? "", s.OperativeSystem ?? "",
                s.RamGb.HasValue ? (object)s.RamGb.Value : "",
                s.CpuQty ?? "", s.DiskC ?? "", s.DiskD ?? "",
                s.DiskE ?? "", s.CeNumberServer ?? ""
            ];

            for (int col = 0; col < vals.Length; col++)
            {
                var cell = ws.Cell(row, col + 1);
                cell.Value  = XLCellValue.FromObject(vals[col]);
                cell.Style.Fill.SetBackgroundColor(bgColor);
            }
            row++;
        }

        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(2);

        using var stream = new MemoryStream();
        wb.SaveAs(stream);
        stream.Seek(0, SeekOrigin.Begin);

        return File(stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"Inventario_Servidores_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
    }

    // ── HELPER ─────────────────────────────────────────────────────────────────
    private static ServerDto ToDto(ServerRecord s) => new(
        s.Id, s.PlantId, s.Plant.Code, s.ServerName, s.IpAddress,
        s.ApplicationDescription, s.AppName, s.Site, s.Environment,
        s.InfraType, s.ResourceType, s.SqlVersion, s.OperativeSystem,
        s.RamGb, s.CpuQty, s.DiskC, s.DiskD, s.DiskE,
        s.CeNumberServer, s.CeNumberSql, s.Notes,
        s.Priority, s.UpdatedAt, s.LastModifiedBy);
}
