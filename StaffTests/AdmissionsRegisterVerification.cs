using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Student_Management_System.Controllers;
using Student_Management_System.Models;
using Student_Management_System.Services;

internal static class AdmissionsRegisterVerification
{
    public static async Task RunAsync(ApplicationDbContext db, int campusId, string actorId, Action<bool, string> check)
    {
        var department = new Department { Name = "Admission paging fixture", Code = "PAGE" };
        var year = new AcademicYear { Name = "Paging fixture year", StartsOn = DateTime.Today, EndsOn = DateTime.Today.AddYears(1) };
        var now = DateTime.UtcNow;
        db.AdmissionApplications.AddRange(Enumerable.Range(1, 251).Select(index => new AdmissionApplication
        {
            ReferenceNumber = $"PAGE-{index:000}", ApplicantName = $"Applicant {index:000}", Email = $"paging-{index}@test.invalid",
            DateOfBirth = DateTime.Today.AddYears(-18), Department = department, AcademicYear = year, CampusId = campusId,
            ProgramName = "Paging fixture program", Stage = AdmissionStage.Submitted, UpdatedUtc = now
        }));
        db.AdmissionInquiries.AddRange(new AdmissionInquiry { ProspectName = "Own inquiry", AssignedToUserId = actorId },
            new AdmissionInquiry { ProspectName = "Another user's inquiry", AssignedToUserId = "other-user" });
        await db.SaveChangesAsync();
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, actorId), new Claim(ClaimTypes.Role, "AdmissionOfficer")], "test"));
        var controller = new AdmissionsController(db, null!, null!, null!, null!, new CampusScopeService(db))
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = principal } } };
        var result = (ViewResult)await controller.Index("Applicant", AdmissionStage.Submitted, year.Id, page: 26, pageSize: 10);
        var model = (AdmissionDashboardViewModel)result.Model!;
        check(model.Total == 251 && model.Pagination.TotalPages == 26 && model.Applications.Count == 1,
            "The admissions register must reach records beyond the former 250-row ceiling.");
        check(model.Pagination.Page == 26 && model.Pagination.PageSize == 10 && model.New == 251,
            "Metrics must describe the entire filtered set while paging describes the current page.");
        var inquiries = (IEnumerable<AdmissionInquiry>)controller.ViewBag.Inquiries;
        check(inquiries.Count() == 1 && inquiries.Single().AssignedToUserId == actorId,
            "An admissions officer must not see another user's unscoped inquiry rows.");
        var again = (AdmissionDashboardViewModel)((ViewResult)await controller.Index("Applicant", AdmissionStage.Submitted, year.Id, page: 26, pageSize: 10)).Model!;
        check(again.Applications.Single().Id == model.Applications.Single().Id, "Equal update timestamps must have stable admission pagination ordering.");
        var empty = (AdmissionDashboardViewModel)((ViewResult)await controller.Index("no-match", AdmissionStage.Approved, year.Id, page: 99, pageSize: 10)).Model!;
        check(empty.Total == 0 && empty.Pagination.Page == 1 && empty.Applications.Count == 0, "Empty filtered admissions pages must clamp to page one.");
    }
}
