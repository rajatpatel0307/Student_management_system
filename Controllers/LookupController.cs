using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Student_Management_System.Models;

namespace Student_Management_System.Controllers;

[Authorize]
[Route("api/lookups")]
public sealed class LookupController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    public LookupController(ApplicationDbContext db) => _db = db;

    [HttpGet("classes")]
    public async Task<IActionResult> Classes(int departmentId, CancellationToken cancellationToken)
        => Ok(await _db.Classes.AsNoTracking()
            .Where(c => c.DepartmentId == departmentId)
            .OrderBy(c => c.Name)
            .Select(c => new { value = c.Id, text = c.Name })
            .ToListAsync(cancellationToken));

    [HttpGet("sections")]
    public async Task<IActionResult> Sections(int classId, CancellationToken cancellationToken)
        => Ok(await _db.Sections.AsNoTracking()
            .Where(s => s.ClassId == classId)
            .OrderBy(s => s.Name)
            .Select(s => new { value = s.Id, text = s.Name })
            .ToListAsync(cancellationToken));

    [HttpGet("semesters")]
    public async Task<IActionResult> Semesters(int departmentId, CancellationToken cancellationToken)
        => Ok(await _db.Semesters.AsNoTracking()
            .Where(s => s.DepartmentId == departmentId)
            .OrderBy(s => s.Number)
            .Select(s => new { value = s.Id, text = s.Name })
            .ToListAsync(cancellationToken));

    [HttpGet("subjects")]
    public async Task<IActionResult> Subjects(int classId, int? semesterId, CancellationToken cancellationToken)
        => Ok(await _db.Subjects.AsNoTracking()
            .Where(s => s.ClassId == classId && (!semesterId.HasValue || s.SemesterId == semesterId))
            .OrderBy(s => s.Name)
            .Select(s => new { value = s.Id, text = s.Name })
            .ToListAsync(cancellationToken));
}
