using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;
using TKVL.Models;

[Route("api/[controller]")]
[ApiController]
public class KhuVucController : ControllerBase
{
    private readonly JobPortalDbContext _context;
    public KhuVucController(JobPortalDbContext context) { _context = context; }

    // ==========================================
    // QUẢN LÝ THÀNH PHỐ
    // ==========================================
    [HttpGet("ThanhPho")]
    public async Task<IActionResult> GetThanhPho() => Ok(await _context.ThanhPhos.ToListAsync());

    [HttpPost("ThanhPho")]
    public async Task<IActionResult> CreateThanhPho([FromBody] ThanhPho request)
    {
        _context.ThanhPhos.Add(request);
        await _context.SaveChangesAsync();
        return Ok(request);
    }

    [HttpPut("ThanhPho/{id}")]
    public async Task<IActionResult> UpdateThanhPho(int id, [FromBody] ThanhPho request)
    {
        var item = await _context.ThanhPhos.FindAsync(id);
        if (item == null) return NotFound();
        item.TenTp = request.TenTp;
        await _context.SaveChangesAsync();
        return Ok(item);
    }

    [HttpDelete("ThanhPho/{id}")]
    public async Task<IActionResult> DeleteThanhPho(int id)
    {
        var item = await _context.ThanhPhos.FindAsync(id);
        if (item == null) return NotFound();
        _context.ThanhPhos.Remove(item);
        await _context.SaveChangesAsync();
        return Ok(new { success = true });
    }

    // ==========================================
    // QUẢN LÝ PHƯỜNG XÃ
    // ==========================================
    [HttpGet("PhuongXa")]
    public async Task<IActionResult> GetPhuongXa() => Ok(await _context.PhuongXas.ToListAsync());

    [HttpPost("PhuongXa")]
    public async Task<IActionResult> CreatePhuongXa([FromBody] PhuongXa request)
    {
        if (request.MaTp == 0) request.MaTp = 1;
        _context.PhuongXas.Add(request);
        await _context.SaveChangesAsync();
        return Ok(request);
    }

    [HttpPut("PhuongXa/{id}")]
    public async Task<IActionResult> UpdatePhuongXa(int id, [FromBody] PhuongXa request)
    {
        var item = await _context.PhuongXas.FindAsync(id);
        if (item == null) return NotFound();
        item.TenPhuong = request.TenPhuong;
        await _context.SaveChangesAsync();
        return Ok(item);
    }

    [HttpDelete("PhuongXa/{id}")]
    public async Task<IActionResult> DeletePhuongXa(int id)
    {
        var item = await _context.PhuongXas.FindAsync(id);
        if (item == null) return NotFound();
        _context.PhuongXas.Remove(item);
        await _context.SaveChangesAsync();
        return Ok(new { success = true });
    }
}