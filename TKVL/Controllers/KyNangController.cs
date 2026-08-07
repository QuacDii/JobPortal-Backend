using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;
using TKVL.Models;

[Route("api/[controller]")]
[ApiController]
public class KyNangController : ControllerBase
{
    private readonly JobPortalDbContext _context;
    public KyNangController(JobPortalDbContext context) { _context = context; }

    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(await _context.KyNangs.ToListAsync());

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] KyNang request)
    {
        _context.KyNangs.Add(request);
        await _context.SaveChangesAsync();
        return Ok(request);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] KyNang request)
    {
        var item = await _context.KyNangs.FindAsync(id);
        if (item == null) return NotFound();
        item.TenKyNang = request.TenKyNang;
        await _context.SaveChangesAsync();
        return Ok(item);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await _context.KyNangs.FindAsync(id);
        if (item == null) return NotFound();
        _context.KyNangs.Remove(item);
        await _context.SaveChangesAsync();
        return Ok(new { success = true });
    }
}