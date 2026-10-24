using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Student_Management_System.Models;
using Student_Management_System.Services;
using System.Security.Claims;

namespace Student_Management_System.Controllers;

[Authorize(Roles = "SuperAdmin,Admin,AdmissionOfficer,Registrar")]
[PermissionAuthorize("admission.view")]
public sealed class AdmissionsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly AuditService _audit;
    private readonly IStudentAccountService _studentAccounts;
    private readonly IPrivateFileStorage _privateFiles;
    private readonly ICampusScopeService _campusScope;

    public AdmissionsController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> users,
        AuditService audit,
        IStudentAccountService studentAccounts,
        IPrivateFileStorage privateFiles,
        ICampusScopeService campusScope)
    {
        _db = db;
        _users = users;
        _audit = audit;
        _studentAccounts = studentAccounts;
        _privateFiles = privateFiles;
        _campusScope = campusScope;
    }

    public async Task<IActionResult> Index(string? search, AdmissionStage? stage, int? academicYearId)
    {
        var campusIds = await _campusScope.AllowedCampusIdsAsync(User);
        var query = _db.AdmissionApplications.AsNoTracking()
            .Include(a => a.Department).Include(a => a.AcademicYear)
            .Where(a => !a.IsArchived && campusIds.Contains(a.CampusId ?? 0)).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(a => a.ReferenceNumber.Contains(term) ||
                                     a.ApplicantName.Contains(term) ||
                                     a.Email.Contains(term));
        }
        if (stage.HasValue) query = query.Where(a => a.Stage == stage);
        if (academicYearId.HasValue) query = query.Where(a => a.AcademicYearId == academicYearId);

        var records = await query.OrderByDescending(a => a.UpdatedUtc).Take(250).ToListAsync();
        ViewBag.AcademicYears = await _db.AcademicYears.AsNoTracking()
            .OrderByDescending(a => a.StartsOn).ToListAsync();
        ViewBag.Search = search;
        ViewBag.Stage = stage;
        ViewBag.AcademicYearId = academicYearId;
        ViewBag.Inquiries = await _db.AdmissionInquiries.AsNoTracking()
            .Include(i => i.AcademicProgram)
            .Where(i => i.Status != "Closed")
            .OrderByDescending(i => i.CreatedUtc).Take(25).ToListAsync();
        ViewBag.Programs = await _db.AcademicPrograms.AsNoTracking()
            .Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync();
        return View(new AdmissionDashboardViewModel
        {
            Applications = records,
            Total = await query.CountAsync(),
            New = await query.CountAsync(a => a.Stage == AdmissionStage.Submitted),
            UnderReview = await query.CountAsync(a => a.Stage == AdmissionStage.UnderReview),
            DocumentsPending = await query.CountAsync(a => a.Stage == AdmissionStage.DocumentsPending),
            Approved = await query.CountAsync(a => a.Stage == AdmissionStage.Approved || a.Stage == AdmissionStage.OfferSent),
            Enrolled = await query.CountAsync(a => a.Stage == AdmissionStage.Enrolled)
        });
    }

    public async Task<IActionResult> Details(long id)
    {
        var application = await _db.AdmissionApplications.AsNoTracking()
            .Include(a => a.Department).Include(a => a.AcademicYear).Include(a => a.Class)
            .Include(a => a.EnrolledStudent)
            .Include(a => a.Documents)
            .Include(a => a.AcademicProgram)
            .Include(a => a.AcademicIntake)
            .Include(a => a.StageHistory.OrderByDescending(h => h.ChangedUtc))
            .SingleOrDefaultAsync(a => a.Id == id);
        if (application == null) return NotFound();
        if (!await _campusScope.CanAccessAsync(User, application.CampusId)) return Forbid();
        ViewBag.Sections = application.ClassId.HasValue
            ? await _db.Sections.AsNoTracking().Where(s => s.ClassId == application.ClassId.Value)
                .OrderBy(s => s.Name).ToListAsync()
            : new List<Section>();
        var nextStage = GetNextStage(application.Stage);
        ViewBag.NextStage = nextStage;
        ViewBag.NextStageLabel = nextStage.HasValue ? GetStageLabel(nextStage.Value) : null;
        ViewBag.AllowedStages = GetAllowedStages(application.Stage);
        ViewBag.Appointments = await _db.AdmissionAppointments.AsNoTracking()
            .Where(a => a.AdmissionApplicationId == id)
            .OrderByDescending(a => a.StartsUtc).ToListAsync();
        if (application.EnrolledStudent?.UserId is string enrolledUserId)
            ViewBag.Activation = await _db.AccountActivations.AsNoTracking()
                .Where(a => a.UserId == enrolledUserId)
                .OrderByDescending(a => a.Id)
                .FirstOrDefaultAsync();
        return View(application);
    }

    [PermissionAuthorize("admission.create")]
    public async Task<IActionResult> Create()
    {
        await LoadOptionsAsync();
        return View(new AdmissionApplicationInput
        {
            DateOfBirth = DateTime.Today.AddYears(-18),
            Source = "Direct"
        });
    }

    [PermissionAuthorize("admission.create")]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AdmissionApplicationInput input)
    {
        await ValidateRelationshipsAsync(input);
        if (!ModelState.IsValid)
        {
            await LoadOptionsAsync();
            return View(input);
        }

        var application = new AdmissionApplication
        {
            CampusId = await _campusScope.RequireDefaultCampusIdAsync(User),
            ReferenceNumber = await NextReferenceAsync(),
            ApplicantName = input.ApplicantName.Trim(),
            Email = input.Email.Trim().ToLowerInvariant(),
            Phone = input.Phone?.Trim(),
            DateOfBirth = input.DateOfBirth,
            Address = input.Address?.Trim(),
            AcademicYearId = input.AcademicYearId,
            DepartmentId = input.DepartmentId,
            ClassId = input.ClassId,
            ProgramName = input.ProgramName.Trim(),
            Source = string.IsNullOrWhiteSpace(input.Source) ? "Direct" : input.Source.Trim(),
            InternalNotes = input.InternalNotes?.Trim(),
            ApplicantNotes = input.ApplicantNotes?.Trim(),
            Stage = AdmissionStage.Draft,
            CreatedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        };
        _db.Add(application);
        await _db.SaveChangesAsync();
        await _audit.RecordAsync("Created admission application", nameof(AdmissionApplication), application.Id);
        TempData["Success"] = $"Application {application.ReferenceNumber} created.";
        return RedirectToAction(nameof(Details), new { id = application.Id });
    }

    [PermissionAuthorize("admission.review")]
    public async Task<IActionResult> Edit(long id)
    {
        var application = await _db.AdmissionApplications.AsNoTracking()
            .SingleOrDefaultAsync(a => a.Id == id);
        if (application == null) return NotFound();
        if (!await _campusScope.CanAccessAsync(User, application.CampusId)) return Forbid();
        if (application.Stage == AdmissionStage.Enrolled) return Conflict("Enrolled applications are read-only.");
        await LoadOptionsAsync();
        ViewBag.ReferenceNumber = application.ReferenceNumber;
        return View(AdmissionApplicationInput.FromEntity(application));
    }

    [PermissionAuthorize("admission.review")]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(long id, AdmissionApplicationInput input)
    {
        if (id != input.Id) return NotFound();
        var application = await _db.AdmissionApplications.SingleOrDefaultAsync(a => a.Id == id);
        if (application == null) return NotFound();
        if (!await _campusScope.CanAccessAsync(User, application.CampusId)) return Forbid();
        ConcurrencyToken.Apply(_db, application, input.RowVersion);
        if (application.Stage == AdmissionStage.Enrolled) return Conflict("Enrolled applications are read-only.");
        await ValidateRelationshipsAsync(input);
        if (!ModelState.IsValid)
        {
            ViewBag.ReferenceNumber = application.ReferenceNumber;
            await LoadOptionsAsync();
            return View(input);
        }

        application.ApplicantName = input.ApplicantName.Trim();
        application.Email = input.Email.Trim().ToLowerInvariant();
        application.Phone = input.Phone?.Trim();
        application.DateOfBirth = input.DateOfBirth;
        application.Address = input.Address?.Trim();
        application.AcademicYearId = input.AcademicYearId;
        application.DepartmentId = input.DepartmentId;
        application.ClassId = input.ClassId;
        application.ProgramName = input.ProgramName.Trim();
        application.Source = string.IsNullOrWhiteSpace(input.Source) ? "Direct" : input.Source.Trim();
        application.InternalNotes = input.InternalNotes?.Trim();
        application.ApplicantNotes = input.ApplicantNotes?.Trim();
        application.UpdatedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await _audit.RecordAsync("Updated admission application", nameof(AdmissionApplication), id);
        TempData["Success"] = "Application updated successfully.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [PermissionAuthorize("admission.review")]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Transition(long id, string? comment)
    {
        var application = await _db.AdmissionApplications.SingleOrDefaultAsync(a => a.Id == id);
        if (application == null) return NotFound();
        if (!await _campusScope.CanAccessAsync(User, application.CampusId)) return Forbid();
        var nextStage = GetNextStage(application.Stage);
        if (!nextStage.HasValue)
            return UnprocessableEntity($"The {GetStageLabel(application.Stage)} stage has no automatic next step.");

        var previous = application.Stage;
        application.Stage = nextStage.Value;
        application.UpdatedUtc = DateTime.UtcNow;
        _db.AdmissionStageHistories.Add(new AdmissionStageHistory
        {
            AdmissionApplicationId = id,
            FromStage = previous,
            ToStage = nextStage.Value,
            Comment = comment?.Trim(),
            ChangedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? ""
        });
        await _db.SaveChangesAsync();
        await _audit.RecordAsync($"Changed admission stage to {nextStage.Value}", nameof(AdmissionApplication), id);
        TempData["Success"] = $"Application moved to {GetStageLabel(nextStage.Value)}.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [PermissionAuthorize("admission.approve")]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Enroll(long id, int sectionId)
    {
        var application = await _db.AdmissionApplications
            .SingleOrDefaultAsync(a => a.Id == id);
        if (application == null) return NotFound();
        if (!await _campusScope.CanAccessAsync(User, application.CampusId)) return Forbid();
        if (application.Stage is not (AdmissionStage.Approved or AdmissionStage.OfferSent))
            return UnprocessableEntity("Only approved applications can be enrolled.");
        if (!application.ClassId.HasValue) return UnprocessableEntity("Assign a class before enrollment.");

        var placement = await _db.Classes.AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == application.ClassId);
        var section = await _db.Sections.AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == sectionId && s.ClassId == application.ClassId);
        var academicYear = await _db.AcademicYears.FindAsync(application.AcademicYearId);
        var semester = placement?.SemesterId is int semesterId
            ? await _db.Semesters.AsNoTracking().SingleOrDefaultAsync(s => s.Id == semesterId) : null;
        var enrollmentInput = new Student
        {
            ClassId = application.ClassId.Value, SectionId = sectionId,
            DepartmentId = application.DepartmentId, SemesterId = placement?.SemesterId
        };
        var placementErrors = StudentPlacementRules.Validate(enrollmentInput, placement, section, semester);
        if (placementErrors.Count != 0 || academicYear == null || academicYear.IsArchived)
            return BadRequest("Select an active class, matching section, department, semester and academic year before enrollment.");
        if (await _users.FindByEmailAsync(application.Email) != null)
            return Conflict("An account already uses this email address.");

        var strategy = _db.Database.CreateExecutionStrategy();
        var studentId = await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync();
            var user = new ApplicationUser
            {
                UserName = application.Email,
                Email = application.Email,
                FullName = application.ApplicantName,
                EmailConfirmed = false
            };
            var identityResult = await _users.CreateAsync(user);
            if (!identityResult.Succeeded)
            {
                await transaction.RollbackAsync();
                foreach (var error in identityResult.Errors)
                    ModelState.AddModelError("", error.Description);
                return (int?)null;
            }

            var roleResult = await _users.AddToRoleAsync(user, "Student");
            if (!roleResult.Succeeded)
            {
                await transaction.RollbackAsync();
                foreach (var error in roleResult.Errors)
                    ModelState.AddModelError("", error.Description);
                return (int?)null;
            }

            var student = new Student
            {
                FullName = application.ApplicantName,
                Email = application.Email,
                Phone = application.Phone,
                DOB = application.DateOfBirth,
                Address = application.Address,
                UserId = user.Id,
                DepartmentId = application.DepartmentId,
                ClassId = application.ClassId.Value,
                SectionId = section!.Id,
                SemesterId = placement!.SemesterId,
                AcademicProgramId = application.AcademicProgramId
            };
            _db.UserCampusMemberships.Add(new UserCampusMembership
            {
                UserId = user.Id, CampusId = application.CampusId!.Value, IsDefault = true,
                GrantedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            });
            _db.Students.Add(student);
            await _db.SaveChangesAsync();
            _db.StudentClassEnrollments.Add(new StudentClassEnrollment
            {
                StudentId = student.Id,
                ClassId = application.ClassId.Value,
                SectionId = section.Id,
                AcademicYearId = academicYear.Id,
                IsActive = true
            });
            var subjectIds = await _db.Subjects.AsNoTracking()
                .Where(s => s.ClassId == application.ClassId)
                .Select(s => s.Id).ToListAsync();
            _db.StudentSubjectEnrollments.AddRange(subjectIds.Select(subjectId =>
                new StudentSubjectEnrollment { StudentId = student.Id, SubjectId = subjectId }));
            var previousStage = application.Stage;
            application.EnrolledStudentId = student.Id;
            application.Stage = AdmissionStage.Enrolled;
            application.UpdatedUtc = DateTime.UtcNow;
            _db.AdmissionStageHistories.Add(new AdmissionStageHistory
            {
                AdmissionApplicationId = id,
                FromStage = previousStage,
                ToStage = AdmissionStage.Enrolled,
                Comment = "Student account and enrollment created.",
                ChangedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? ""
            });
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            return student.Id;
        });

        if (!studentId.HasValue) return ValidationProblem(ModelState);
        await _audit.RecordAsync("Enrolled admission applicant", nameof(Student), studentId.Value);
        var enrolledUser = await _db.Students.Where(s => s.Id == studentId.Value)
            .Select(s => s.User).SingleOrDefaultAsync();
        if (enrolledUser == null)
            return Problem(
                "The student was enrolled, but the linked account could not be loaded. An administrator must repair the account link.",
                statusCode: StatusCodes.Status500InternalServerError);
        var invitation = await _studentAccounts.CreateActivationAsync(enrolledUser);
        var activationUrl = Url.Action(
            "Activate", "Account",
            new { email = enrolledUser.Email, token = invitation.RawToken },
            Request.Scheme);
        var delivered = !string.IsNullOrWhiteSpace(activationUrl) &&
                        await _studentAccounts.SendActivationAsync(enrolledUser, activationUrl);
        TempData[delivered ? "Success" : "Warning"] = delivered
            ? "Student enrolled and the activation invitation was sent."
            : "Student enrolled, but activation email delivery is pending. Use Resend activation after SMTP is restored.";
        return RedirectToAction(nameof(Details), new { id = application.Id });
    }

    [PermissionAuthorize("admission.approve")]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendActivation(long id, CancellationToken cancellationToken)
    {
        var applicationCampusId = await _db.AdmissionApplications.Where(a => a.Id == id)
            .Select(a => a.CampusId).SingleOrDefaultAsync(cancellationToken);
        if (!await _campusScope.CanAccessAsync(User, applicationCampusId, cancellationToken))
            return Forbid();
        var user = await _db.AdmissionApplications
            .Where(a => a.Id == id && a.EnrolledStudentId != null)
            .Select(a => a.EnrolledStudent!.User)
            .SingleOrDefaultAsync(cancellationToken);
        // Return the same result for missing or already-active accounts.
        if (user != null && !user.EmailConfirmed && !await _users.HasPasswordAsync(user))
        {
            try
            {
                var invitation = await _studentAccounts.CreateActivationAsync(user, cancellationToken);
                var url = Url.Action("Activate", "Account",
                    new { email = user.Email, token = invitation.RawToken }, Request.Scheme);
                if (!string.IsNullOrWhiteSpace(url))
                    await _studentAccounts.SendActivationAsync(user, url, cancellationToken);
                await _audit.RecordAsync("Resent student activation invitation", nameof(ApplicationUser), user.Id);
            }
            catch (InvalidOperationException)
            {
                // Deliberately use the generic response below.
            }
        }
        TempData["Success"] = "If activation is still required, a new invitation has been queued.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [PermissionAuthorize("admission.review")]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> TransitionTo(long id, AdmissionStage stage, string? reason)
    {
        var application = await _db.AdmissionApplications.SingleOrDefaultAsync(a => a.Id == id);
        if (application == null) return NotFound();
        if (!await _campusScope.CanAccessAsync(User, application.CampusId)) return Forbid();
        if (application.Stage == AdmissionStage.Enrolled)
            return Conflict("An enrolled application cannot be closed.");
        if (!GetAllowedStages(application.Stage).Contains(stage))
            return UnprocessableEntity("That admission stage transition is not allowed.");
        if ((stage is AdmissionStage.Rejected or AdmissionStage.Waitlisted or
                      AdmissionStage.Withdrawn or AdmissionStage.Cancelled) &&
            string.IsNullOrWhiteSpace(reason))
            return BadRequest("A reason is required for this outcome.");
        var previous = application.Stage;
        application.Stage = stage;
        application.UpdatedUtc = DateTime.UtcNow;
        _db.AdmissionStageHistories.Add(new AdmissionStageHistory
        {
            AdmissionApplicationId = id,
            FromStage = previous,
            ToStage = stage,
            Comment = reason?.Trim(),
            ChangedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? ""
        });
        await _db.SaveChangesAsync();
        await _audit.RecordAsync($"Changed admission stage to {stage}", nameof(AdmissionApplication), id);
        TempData["Success"] = $"Application moved to {GetStageLabel(stage)}.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [PermissionAuthorize("admission.create")]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateInquiry(
        string prospectName,
        string? email,
        string? phone,
        int? academicProgramId,
        string source,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(prospectName) ||
            (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(phone)))
            return BadRequest("A prospect name and at least one contact method are required.");
        if (academicProgramId.HasValue &&
            !await _db.AcademicPrograms.AnyAsync(p =>
                p.Id == academicProgramId && p.IsActive, cancellationToken))
            return BadRequest("Select an active program.");
        var inquiry = new AdmissionInquiry
        {
            ProspectName = prospectName.Trim(),
            Email = email?.Trim().ToLowerInvariant(),
            Phone = phone?.Trim(),
            AcademicProgramId = academicProgramId,
            Source = string.IsNullOrWhiteSpace(source) ? "Direct" : source.Trim(),
            AssignedToUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)
        };
        _db.AdmissionInquiries.Add(inquiry);
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.RecordAsync("Created admission inquiry",
            nameof(AdmissionInquiry), inquiry.Id);
        TempData["Success"] = "Admission inquiry recorded.";
        return RedirectToAction(nameof(Index));
    }

    [PermissionAuthorize("admission.review")]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ScheduleAppointment(
        long id,
        string appointmentType,
        DateTime startsUtc,
        string? location,
        string? reviewerUserId,
        CancellationToken cancellationToken)
    {
        var appointmentCampusId = await _db.AdmissionApplications.Where(a => a.Id == id)
            .Select(a => a.CampusId).SingleOrDefaultAsync(cancellationToken);
        if (!appointmentCampusId.HasValue) return NotFound();
        if (!await _campusScope.CanAccessAsync(User, appointmentCampusId, cancellationToken))
            return Forbid();
        if (startsUtc <= DateTime.UtcNow)
            return BadRequest("Appointment time must be in the future.");
        var appointment = new AdmissionAppointment
        {
            AdmissionApplicationId = id,
            AppointmentType = string.IsNullOrWhiteSpace(appointmentType)
                ? "Interview"
                : appointmentType.Trim(),
            StartsUtc = startsUtc,
            Location = location?.Trim(),
            ReviewerUserId = reviewerUserId
        };
        _db.AdmissionAppointments.Add(appointment);
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.RecordAsync("Scheduled admission appointment",
            nameof(AdmissionAppointment), appointment.Id);
        TempData["Success"] = "Admission appointment scheduled.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [PermissionAuthorize("admission.review")]
    [HttpPost, ValidateAntiForgeryToken]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> UploadDocument(
        long id, string documentType, IFormFile file, CancellationToken cancellationToken)
    {
        var documentCampusId = await _db.AdmissionApplications.Where(a => a.Id == id)
            .Select(a => a.CampusId).SingleOrDefaultAsync(cancellationToken);
        if (!documentCampusId.HasValue) return NotFound();
        if (!await _campusScope.CanAccessAsync(User, documentCampusId, cancellationToken))
            return Forbid();
        if (string.IsNullOrWhiteSpace(documentType)) return BadRequest("Document type is required.");
        StoredPrivateFile stored;
        try
        {
            stored = await _privateFiles.SaveAdmissionDocumentAsync(file, cancellationToken);
        }
        catch (PrivateFileValidationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Details), new { id });
        }
        try
        {
            var safeOriginalName = Path.GetFileName(file.FileName);
            if (safeOriginalName.Length > 255) safeOriginalName = safeOriginalName[..255];
            var document = new AdmissionDocument
            {
                AdmissionApplicationId = id,
                DocumentType = documentType.Trim(),
                StorageKey = stored.StorageKey,
                OriginalFileName = safeOriginalName,
                ContentType = stored.ContentType,
                SizeBytes = stored.Length
            };
            _db.AdmissionDocuments.Add(document);
            await _db.SaveChangesAsync(cancellationToken);
            await _audit.RecordAsync("Uploaded admission document", nameof(AdmissionDocument), document.Id);
        }
        catch
        {
            await _privateFiles.DeleteAsync(
                stored.StorageKey, PrivateFileKind.AdmissionDocument, cancellationToken);
            throw;
        }
        TempData["Success"] = "Document uploaded for verification.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [PermissionAuthorize("admission.view")]
    [HttpGet]
    public async Task<IActionResult> DownloadDocument(long id, CancellationToken cancellationToken)
    {
        var document = await _db.AdmissionDocuments.AsNoTracking()
            .Include(d => d.AdmissionApplication)
            .SingleOrDefaultAsync(d => d.Id == id, cancellationToken);
        if (document == null) return NotFound();
        if (!await _campusScope.CanAccessAsync(
                User, document.AdmissionApplication.CampusId, cancellationToken))
            return NotFound();
        var opened = await _privateFiles.OpenReadAsync(
            document.StorageKey, PrivateFileKind.AdmissionDocument, cancellationToken);
        if (opened == null) return NotFound();
        Response.Headers.CacheControl = "no-store, no-cache, max-age=0";
        Response.Headers.Pragma = "no-cache";
        return File(opened.Stream, opened.ContentType, document.OriginalFileName, enableRangeProcessing: false);
    }

    [PermissionAuthorize("admission.review")]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyDocument(
        long id,
        bool verified,
        string? comment,
        byte[] rowVersion,
        CancellationToken cancellationToken)
    {
        var document = await _db.AdmissionDocuments
            .Include(d => d.AdmissionApplication)
            .SingleOrDefaultAsync(d => d.Id == id, cancellationToken);
        if (document == null) return NotFound();
        if (!await _campusScope.CanAccessAsync(
                User, document.AdmissionApplication.CampusId, cancellationToken))
            return NotFound();
        if (comment?.Length > 1000)
            return UnprocessableEntity("Verification comments cannot exceed 1,000 characters.");
        ConcurrencyToken.Apply(_db, document.AdmissionApplication, rowVersion);
        document.AdmissionApplication.UpdatedUtc = DateTime.UtcNow;
        var previousStatus = document.Status;
        document.Status = verified
            ? DocumentVerificationStatus.Verified
            : DocumentVerificationStatus.Rejected;
        document.VerifiedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        document.VerifiedUtc = DateTime.UtcNow;
        document.VerificationComment = comment?.Trim();
        _audit.Add(
            verified ? "Verified admission document" : "Rejected admission document",
            nameof(AdmissionDocument),
            id,
            detailsJson: System.Text.Json.JsonSerializer.Serialize(new
            {
                From = previousStatus.ToString(),
                To = document.Status.ToString(),
                document.AdmissionApplicationId
            }));
        // The status mutation and its audit entry are committed atomically.
        await _db.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(Details), new { id = document.AdmissionApplicationId });
    }

    private async Task LoadOptionsAsync()
    {
        ViewBag.AcademicYears = await _db.AcademicYears.AsNoTracking().OrderByDescending(a => a.StartsOn).ToListAsync();
        ViewBag.Departments = await _db.Departments.AsNoTracking().OrderBy(d => d.Name).ToListAsync();
        ViewBag.Classes = await _db.Classes.AsNoTracking().OrderBy(c => c.Name).ToListAsync();
        ViewBag.Sections = await _db.Sections.AsNoTracking().OrderBy(s => s.Name).ToListAsync();
    }

    private void RemoveNavigationValidation()
    {
        ModelState.Remove(nameof(AdmissionApplication.ReferenceNumber));
        ModelState.Remove(nameof(AdmissionApplication.AcademicYear));
        ModelState.Remove(nameof(AdmissionApplication.Department));
        ModelState.Remove(nameof(AdmissionApplication.Class));
        ModelState.Remove(nameof(AdmissionApplication.EnrolledStudent));
        ModelState.Remove(nameof(AdmissionApplication.StageHistory));
    }

    private async Task ValidateRelationshipsAsync(AdmissionApplicationInput input)
    {
        if (input.DateOfBirth >= DateTime.Today)
            ModelState.AddModelError(nameof(input.DateOfBirth), "Date of birth must be in the past.");
        if (!await _db.AcademicYears.AnyAsync(a => a.Id == input.AcademicYearId))
            ModelState.AddModelError(nameof(input.AcademicYearId), "Select a valid academic year.");
        if (!await _db.Departments.AnyAsync(d => d.Id == input.DepartmentId))
            ModelState.AddModelError(nameof(input.DepartmentId), "Select a valid department.");
        if (input.ClassId.HasValue &&
            !await _db.Classes.AnyAsync(c => c.Id == input.ClassId && c.DepartmentId == input.DepartmentId))
            ModelState.AddModelError(nameof(input.ClassId), "The class must belong to the selected department.");
    }

    private async Task<string> NextReferenceAsync()
    {
        string reference;
        do reference = $"ADM-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..21].ToUpperInvariant();
        while (await _db.AdmissionApplications.AnyAsync(a => a.ReferenceNumber == reference));
        return reference;
    }

    private static AdmissionStage? GetNextStage(AdmissionStage current)
    {
        return current switch
        {
            AdmissionStage.Draft => AdmissionStage.Submitted,
            AdmissionStage.Submitted => AdmissionStage.UnderReview,
            AdmissionStage.UnderReview => AdmissionStage.DocumentsPending,
            AdmissionStage.DocumentsPending => AdmissionStage.Verified,
            AdmissionStage.Verified => AdmissionStage.EntranceTest,
            AdmissionStage.EntranceTest => AdmissionStage.Interview,
            AdmissionStage.Interview => AdmissionStage.Approved,
            AdmissionStage.Approved => AdmissionStage.OfferSent,
            _ => null
        };
    }

    private static IReadOnlyList<AdmissionStage> GetAllowedStages(
        AdmissionStage current)
    {
        var exceptional = new[]
        {
            AdmissionStage.Waitlisted, AdmissionStage.Rejected,
            AdmissionStage.Withdrawn, AdmissionStage.Cancelled
        };
        var normal = GetNextStage(current);
        if (current is AdmissionStage.Enrolled or AdmissionStage.Rejected or
                       AdmissionStage.Withdrawn or AdmissionStage.Cancelled)
            return [];
        return normal.HasValue ? [normal.Value, .. exceptional] : exceptional;
    }

    private static string GetStageLabel(AdmissionStage stage)
    {
        return stage switch
        {
            AdmissionStage.UnderReview => "Under Review",
            AdmissionStage.DocumentsPending => "Documents Pending",
            AdmissionStage.EntranceTest => "Entrance Test",
            AdmissionStage.OfferSent => "Offer Sent",
            _ => stage.ToString()
        };
    }
}
