using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Student_Management_System.Models;
using Student_Management_System.Modules.Registrar;
using Student_Management_System.Services;

namespace Student_Management_System.Controllers;

[Authorize, Route("api/staff-certificates")]
[ApiController]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class StaffCertificatesController(ApplicationDbContext db, StaffAccessService access,
    ICampusScopeService campusScope, CertificateService certificates) : Controller
{
    [HttpPost("{id:long}/review"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Review(long id, [FromBody] ReviewWorkInput input)
    {
        if (!await access.CanManageAsync(User, "registrar")) return Forbid();
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var campuses = await campusScope.AllowedCampusIdsAsync(User);
        var certificate = await db.RegistrarCertificates.SingleOrDefaultAsync(c => c.Id == id && campuses.Contains(c.CampusId));
        if (certificate == null) return NotFound();
        try { await certificates.ReviewAsync(certificate, input, User.FindFirstValue(ClaimTypes.NameIdentifier)!); return Ok(); }
        catch (StaffValidationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (FormatException) { return BadRequest(); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { message = "The certificate changed. Refresh and try again." }); }
    }
}
