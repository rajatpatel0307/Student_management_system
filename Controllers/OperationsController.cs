using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Student_Management_System.Models;
using Student_Management_System.Services;
using System.Security.Claims;

namespace Student_Management_System.Controllers;

[Authorize]
public sealed class OperationsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly AuditService _audit;
    private readonly IStudentAccountService _accounts;
    public OperationsController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> users,
        AuditService audit,
        IStudentAccountService accounts)
    {
        _db = db;
        _users = users;
        _audit = audit;
        _accounts = accounts;
    }

    public async Task<IActionResult> Index()
    {
        var privileged = User.IsInRole("SuperAdmin") || User.IsInRole("Admin");
        var transportManager = privileged || User.IsInRole("TransportManager");
        var hostelManager = privileged || User.IsInRole("HostelManager");
        var placementManager = privileged || User.IsInRole("PlacementOfficer");
        if (!privileged && !User.IsInRole("TransportManager") &&
            !User.IsInRole("HostelManager") && !User.IsInRole("PlacementOfficer") &&
            !User.IsInRole("Employee") && !User.IsInRole("Student")) return Forbid();
        ViewBag.Programs = privileged
            ? await _db.AcademicPrograms.AsNoTracking().Include(p => p.Department).OrderBy(p => p.Name).ToListAsync()
            : new List<AcademicProgram>();
        ViewBag.TransportRoutes = privileged || User.IsInRole("TransportManager")
            ? await _db.TransportRoutes.AsNoTracking().OrderBy(r => r.Name).ToListAsync()
            : new List<TransportRoute>();
        ViewBag.HostelRooms = privileged || User.IsInRole("HostelManager")
            ? await _db.HostelRooms.AsNoTracking().OrderBy(r => r.HostelName).ThenBy(r => r.RoomNumber).ToListAsync()
            : new List<HostelRoom>();
        var drives = _db.PlacementDrives.AsNoTracking().AsQueryable();
        ViewBag.PlacementDrives = placementManager || User.IsInRole("Student")
            ? await drives.OrderByDescending(d => d.ApplicationDeadlineUtc).Take(100).ToListAsync()
            : new List<PlacementDrive>();
        ViewBag.Employees = privileged
            ? await _db.EmployeeProfiles.AsNoTracking().Include(e => e.User).Include(e => e.Department)
                .OrderBy(e => e.EmployeeNumber).ToListAsync()
            : new List<EmployeeProfile>();
        ViewBag.Departments = privileged
            ? await _db.Departments.AsNoTracking().Where(d => !d.IsArchived).OrderBy(d => d.Name).ToListAsync()
            : new List<Department>();
        ViewBag.AcademicYears = privileged
            ? await _db.AcademicYears.AsNoTracking().Where(y => !y.IsArchived)
                .OrderByDescending(y => y.StartsOn).ToListAsync()
            : new List<AcademicYear>();
        ViewBag.Students = privileged || User.IsInRole("TransportManager") ||
                           User.IsInRole("HostelManager")
            ? await _db.Students.AsNoTracking().Where(s => !s.IsArchived)
                .OrderBy(s => s.FullName).Take(1000).ToListAsync()
            : new List<Student>();
        ViewBag.TransportEnrollments = privileged || User.IsInRole("TransportManager")
            ? await _db.TransportEnrollments.AsNoTracking()
                .Include(e => e.Student).Include(e => e.TransportRoute)
                .Where(e => e.EndsOn == null).OrderBy(e => e.Student.FullName).ToListAsync()
            : new List<TransportEnrollment>();
        ViewBag.HostelAllocations = privileged || User.IsInRole("HostelManager")
            ? await _db.HostelAllocations.AsNoTracking()
                .Include(a => a.Student).Include(a => a.HostelRoom)
                .Where(a => a.EndsOn == null).OrderBy(a => a.Student.FullName).ToListAsync()
            : new List<HostelAllocation>();
        ViewBag.Intakes = privileged
            ? await _db.AcademicIntakes.AsNoTracking()
                .Include(i => i.AcademicProgram).Include(i => i.AcademicYear)
                .OrderByDescending(i => i.OpensUtc).Take(250).ToListAsync()
            : new List<AcademicIntake>();
        ViewBag.PlacementApplications = privileged || User.IsInRole("PlacementOfficer")
            ? await _db.PlacementApplications.AsNoTracking()
                .Include(a => a.Student).Include(a => a.PlacementDrive)
                .OrderByDescending(a => a.AppliedUtc).Take(250).ToListAsync()
            : new List<PlacementApplication>();
        ViewBag.ServiceTickets = await _db.CampusServiceTickets.AsNoTracking()
            .Where(t => t.ReportedByUserId == User.FindFirstValue(ClaimTypes.NameIdentifier) ||
                        privileged || t.AssignedToUserId == User.FindFirstValue(ClaimTypes.NameIdentifier))
            .OrderByDescending(t => t.CreatedUtc).Take(100).ToListAsync();
        ViewBag.TransportVehicles = transportManager
            ? await _db.TransportVehicles.AsNoTracking()
                .OrderBy(v => v.RegistrationNumber).ToListAsync()
            : new List<TransportVehicle>();
        ViewBag.HostelBuildings = hostelManager
            ? await _db.HostelBuildings.AsNoTracking()
                .OrderBy(b => b.Name).ToListAsync()
            : new List<HostelBuilding>();
        ViewBag.PlacementInterviews = placementManager
            ? await _db.PlacementInterviews.AsNoTracking()
                .Include(i => i.PlacementApplication).ThenInclude(a => a.Student)
                .OrderByDescending(i => i.ScheduledUtc).Take(100).ToListAsync()
            : new List<PlacementInterview>();
        ViewBag.EmployeeContracts = privileged
            ? await _db.EmployeeContracts.AsNoTracking()
                .Include(c => c.EmployeeProfile).ThenInclude(e => e.User)
                .OrderByDescending(c => c.StartsOn).Take(100).ToListAsync()
            : new List<EmployeeContract>();
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "SuperAdmin,Admin")]
    public async Task<IActionResult> CreateProgram(AcademicProgram input)
    {
        ModelState.Remove(nameof(AcademicProgram.Department));
        ModelState.Remove(nameof(AcademicProgram.Intakes));
        if (!await _db.Departments.AnyAsync(d => d.Id == input.DepartmentId && !d.IsArchived))
            ModelState.AddModelError(nameof(input.DepartmentId), "Select an active department.");
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        input.Code = input.Code.Trim().ToUpperInvariant();
        input.Name = input.Name.Trim();
        _db.AcademicPrograms.Add(input);
        await _db.SaveChangesAsync();
        await _audit.RecordAsync("Created academic program", nameof(AcademicProgram), input.Id);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "SuperAdmin,Admin")]
    public async Task<IActionResult> CreateIntake(AcademicIntake input)
    {
        ModelState.Remove(nameof(AcademicIntake.AcademicProgram));
        ModelState.Remove(nameof(AcademicIntake.AcademicYear));
        if (input.OpensUtc >= input.ClosesUtc)
            ModelState.AddModelError(nameof(input.ClosesUtc), "Closing date must be after opening date.");
        if (!await _db.AcademicPrograms.AnyAsync(p => p.Id == input.AcademicProgramId && p.IsActive))
            ModelState.AddModelError(nameof(input.AcademicProgramId), "Select an active program.");
        if (!await _db.AcademicYears.AnyAsync(y => y.Id == input.AcademicYearId && !y.IsArchived))
            ModelState.AddModelError(nameof(input.AcademicYearId), "Select an active academic year.");
        if (await _db.AcademicIntakes.AnyAsync(i =>
                i.AcademicProgramId == input.AcademicProgramId &&
                i.AcademicYearId == input.AcademicYearId))
            ModelState.AddModelError("", "An intake already exists for this program and academic year.");
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        _db.AcademicIntakes.Add(input);
        await _db.SaveChangesAsync();
        await _audit.RecordAsync("Created academic intake", nameof(AcademicIntake), input.Id);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "SuperAdmin,Admin,TransportManager")]
    public async Task<IActionResult> CreateTransportRoute(TransportRoute input)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        input.Code = input.Code.Trim().ToUpperInvariant();
        input.Name = input.Name.Trim();
        input.Stops = input.Stops.Trim();
        _db.TransportRoutes.Add(input);
        await _db.SaveChangesAsync();
        await _audit.RecordAsync("Created transport route", nameof(TransportRoute), input.Id);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "SuperAdmin,Admin,HostelManager")]
    public async Task<IActionResult> CreateHostelRoom(HostelRoom input)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        input.HostelName = input.HostelName.Trim();
        input.RoomNumber = input.RoomNumber.Trim();
        _db.HostelRooms.Add(input);
        await _db.SaveChangesAsync();
        await _audit.RecordAsync("Created hostel room", nameof(HostelRoom), input.Id);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "SuperAdmin,Admin,TransportManager")]
    public async Task<IActionResult> EnrollTransport(
        int routeId, int studentId, string boardingStop, DateTime startsOn)
    {
        if (string.IsNullOrWhiteSpace(boardingStop)) return BadRequest("Boarding stop is required.");
        if (!await _db.TransportRoutes.AnyAsync(r => r.Id == routeId && r.IsActive) ||
            !await _db.Students.AnyAsync(s => s.Id == studentId && !s.IsArchived))
            return NotFound();
        if (await _db.TransportEnrollments.AnyAsync(e =>
                e.StudentId == studentId && e.EndsOn == null))
            return Conflict("The student already has an active transport enrollment.");
        var enrollment = new TransportEnrollment
        {
            TransportRouteId = routeId,
            StudentId = studentId,
            BoardingStop = boardingStop.Trim(),
            StartsOn = startsOn.Date
        };
        _db.TransportEnrollments.Add(enrollment);
        await _db.SaveChangesAsync();
        await _audit.RecordAsync("Enrolled student in transport", nameof(TransportEnrollment), enrollment.Id);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "SuperAdmin,Admin,HostelManager")]
    public async Task<IActionResult> AllocateHostel(
        int roomId, int studentId, DateTime startsOn)
    {
        var strategy = _db.Database.CreateExecutionStrategy();
        try
        {
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _db.Database.BeginTransactionAsync(
                    System.Data.IsolationLevel.Serializable);
                var room = await _db.HostelRooms
                    .SingleOrDefaultAsync(r => r.Id == roomId && r.IsActive);
                if (room == null ||
                    !await _db.Students.AnyAsync(s => s.Id == studentId && !s.IsArchived))
                    throw new KeyNotFoundException();
                if (await _db.HostelAllocations.AnyAsync(a =>
                        a.StudentId == studentId && a.EndsOn == null))
                    throw new InvalidOperationException(
                        "The student already has an active hostel allocation.");
                var occupied = await _db.HostelAllocations.CountAsync(a =>
                    a.HostelRoomId == roomId && a.EndsOn == null);
                if (occupied >= room.Capacity)
                    throw new InvalidOperationException("This room is at capacity.");
                var allocation = new HostelAllocation
                {
                    HostelRoomId = roomId,
                    StudentId = studentId,
                    StartsOn = startsOn.Date
                };
                _db.HostelAllocations.Add(allocation);
                _db.AuditLogs.Add(new AuditLog
                {
                    UserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
                    Action = "Allocated hostel room",
                    EntityName = nameof(HostelAllocation),
                    EntityId = $"{roomId}:{studentId}"
                });
                await _db.SaveChangesAsync();
                await transaction.CommitAsync();
            });
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException exception) { return Conflict(exception.Message); }
        catch (DbUpdateException)
        {
            return Conflict("The room or student allocation changed. Refresh and try again.");
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "SuperAdmin,Admin,TransportManager")]
    public async Task<IActionResult> EndTransport(long id, DateTime endsOn)
    {
        var enrollment = await _db.TransportEnrollments
            .SingleOrDefaultAsync(e => e.Id == id);
        if (enrollment == null) return NotFound();
        if (enrollment.EndsOn.HasValue) return Conflict("This enrollment is already closed.");
        if (endsOn.Date < enrollment.StartsOn.Date)
            return BadRequest("End date cannot precede the start date.");
        enrollment.EndsOn = endsOn.Date;
        await _db.SaveChangesAsync();
        await _audit.RecordAsync("Closed transport enrollment", nameof(TransportEnrollment), id);
        TempData["Success"] = "Transport enrollment closed.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "SuperAdmin,Admin,HostelManager")]
    public async Task<IActionResult> EndHostel(long id, DateTime endsOn)
    {
        var allocation = await _db.HostelAllocations
            .SingleOrDefaultAsync(a => a.Id == id);
        if (allocation == null) return NotFound();
        if (allocation.EndsOn.HasValue) return Conflict("This allocation is already closed.");
        if (endsOn.Date < allocation.StartsOn.Date)
            return BadRequest("End date cannot precede the start date.");
        allocation.EndsOn = endsOn.Date;
        await _db.SaveChangesAsync();
        await _audit.RecordAsync("Closed hostel allocation", nameof(HostelAllocation), id);
        TempData["Success"] = "Hostel allocation closed.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "SuperAdmin,Admin,PlacementOfficer")]
    public async Task<IActionResult> CreatePlacementDrive(PlacementDrive input)
    {
        if (input.ApplicationDeadlineUtc <= DateTime.UtcNow)
            ModelState.AddModelError(nameof(input.ApplicationDeadlineUtc), "Deadline must be in the future.");
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        input.Employer = input.Employer.Trim();
        input.RoleTitle = input.RoleTitle.Trim();
        _db.PlacementDrives.Add(input);
        await _db.SaveChangesAsync();
        await _audit.RecordAsync("Created placement drive", nameof(PlacementDrive), input.Id);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Student")]
    public async Task<IActionResult> ApplyForPlacement(long driveId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var studentId = await _db.Students.Where(s => s.UserId == userId && !s.IsArchived)
            .Select(s => (int?)s.Id).SingleOrDefaultAsync();
        if (!studentId.HasValue) return NotFound();
        if (!await _db.PlacementDrives.AnyAsync(d =>
                d.Id == driveId && d.ApplicationDeadlineUtc > DateTime.UtcNow))
            return Conflict("This placement drive is closed.");
        if (await _db.PlacementApplications.AnyAsync(a =>
                a.PlacementDriveId == driveId && a.StudentId == studentId.Value))
            return Conflict("You already applied.");
        _db.PlacementApplications.Add(new PlacementApplication
        {
            PlacementDriveId = driveId,
            StudentId = studentId.Value
        });
        await _db.SaveChangesAsync();
        await _audit.RecordAsync("Applied for placement drive", nameof(PlacementDrive), driveId);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "SuperAdmin,Admin,PlacementOfficer")]
    public async Task<IActionResult> ReviewPlacement(long id, string status)
    {
        var allowed = new[] { "Applied", "Shortlisted", "Interview", "Selected", "Rejected", "Withdrawn" };
        if (!allowed.Contains(status, StringComparer.OrdinalIgnoreCase)) return BadRequest();
        var application = await _db.PlacementApplications.SingleOrDefaultAsync(a => a.Id == id);
        if (application == null) return NotFound();
        application.Status = allowed.Single(s => s.Equals(status, StringComparison.OrdinalIgnoreCase));
        await _db.SaveChangesAsync();
        await _audit.RecordAsync($"Updated placement status to {application.Status}",
            nameof(PlacementApplication), id);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "SuperAdmin,Admin")]
    public async Task<IActionResult> CreateEmployee(
        string fullName, string email, string employeeNumber,
        string jobTitle, int? departmentId, DateTime joinedOn)
    {
        if (string.IsNullOrWhiteSpace(fullName) ||
            string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(employeeNumber) ||
            string.IsNullOrWhiteSpace(jobTitle))
            return BadRequest("Name, email, employee number, and job title are required.");
        if (joinedOn.Date > DateTime.Today)
            return BadRequest("Joining date cannot be in the future.");
        if (departmentId.HasValue &&
            !await _db.Departments.AnyAsync(d => d.Id == departmentId && !d.IsArchived))
            return BadRequest("Select an active department.");
        if (await _db.EmployeeProfiles.AnyAsync(e =>
                e.EmployeeNumber == employeeNumber.Trim().ToUpperInvariant()))
            return Conflict("Employee number already exists.");
        if (await _users.FindByEmailAsync(email) != null) return Conflict("Email already exists.");
        var user = new ApplicationUser
        {
            FullName = fullName.Trim(),
            Email = email.Trim().ToLowerInvariant(),
            UserName = email.Trim().ToLowerInvariant(),
            EmailConfirmed = false
        };
        var result = await _users.CreateAsync(user);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(nameof(email), error.Description);
            return ValidationProblem(ModelState);
        }
        try
        {
            var roleResult = await _users.AddToRoleAsync(user, "Employee");
            if (!roleResult.Succeeded)
                throw new InvalidOperationException(string.Join(
                    "; ", roleResult.Errors.Select(e => e.Description)));
            _db.EmployeeProfiles.Add(new EmployeeProfile
            {
                UserId = user.Id,
                EmployeeNumber = employeeNumber.Trim().ToUpperInvariant(),
                JobTitle = jobTitle.Trim(),
                DepartmentId = departmentId,
                JoinedOn = joinedOn.Date
            });
            await _db.SaveChangesAsync();
        }
        catch
        {
            var compensation = await _users.DeleteAsync(user);
            if (!compensation.Succeeded)
                throw new InvalidOperationException(
                    "Employee onboarding failed and automatic account compensation also failed.");
            throw;
        }
        var invitation = await _accounts.CreateActivationAsync(user);
        var activationUrl = Url.Action(
            "Activate",
            "Account",
            new { email = user.Email, token = invitation.RawToken },
            Request.Scheme);
        var delivered = !string.IsNullOrWhiteSpace(activationUrl) &&
                        await _accounts.SendActivationAsync(user, activationUrl);
        await _audit.RecordAsync("Created employee", nameof(EmployeeProfile), user.Id);
        TempData[delivered ? "Success" : "Warning"] = delivered
            ? "Employee created and activation sent."
            : "Employee created; activation delivery is pending and can be resent.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "SuperAdmin,Admin")]
    public async Task<IActionResult> SetEmployeeActive(int id, bool active)
    {
        var employee = await _db.EmployeeProfiles.Include(e => e.User)
            .SingleOrDefaultAsync(e => e.Id == id);
        if (employee == null) return NotFound();
        employee.IsActive = active;
        employee.User.IsActive = active;
        await _users.UpdateSecurityStampAsync(employee.User);
        await _db.SaveChangesAsync();
        await _audit.RecordAsync(active ? "Activated employee" : "Deactivated employee",
            nameof(EmployeeProfile), id);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateServiceTicket(
        string module, string title, string description, string priority)
    {
        if (string.IsNullOrWhiteSpace(module) || string.IsNullOrWhiteSpace(title) ||
            string.IsNullOrWhiteSpace(description))
            return BadRequest("Module, title, and description are required.");
        var ticket = new CampusServiceTicket
        {
            Module = module.Trim(),
            Title = title.Trim(),
            Description = description.Trim(),
            Priority = string.IsNullOrWhiteSpace(priority) ? "Normal" : priority.Trim(),
            ReportedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? ""
        };
        _db.CampusServiceTickets.Add(ticket);
        await _db.SaveChangesAsync();
        await _audit.RecordAsync("Created campus service ticket",
            nameof(CampusServiceTicket), ticket.Id);
        TempData["Success"] = "Service ticket submitted.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "SuperAdmin,Admin,TransportManager")]
    public async Task<IActionResult> CreateVehicle(
        string registrationNumber, string model, int capacity)
    {
        registrationNumber = registrationNumber.Trim().ToUpperInvariant();
        if (capacity < 1 || await _db.TransportVehicles.AnyAsync(v =>
                v.RegistrationNumber == registrationNumber))
            return Conflict("Enter a unique registration and positive capacity.");
        var vehicle = new TransportVehicle
        {
            RegistrationNumber = registrationNumber,
            Model = model.Trim(),
            Capacity = capacity
        };
        _db.TransportVehicles.Add(vehicle);
        await _db.SaveChangesAsync();
        await _audit.RecordAsync("Created transport vehicle",
            nameof(TransportVehicle), vehicle.Id);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "SuperAdmin,Admin,HostelManager")]
    public async Task<IActionResult> CreateHostelBuilding(string name, string? address)
    {
        name = name.Trim();
        if (string.IsNullOrWhiteSpace(name) ||
            await _db.HostelBuildings.AnyAsync(b => b.Name == name))
            return Conflict("Enter a unique building name.");
        var building = new HostelBuilding { Name = name, Address = address?.Trim() };
        _db.HostelBuildings.Add(building);
        await _db.SaveChangesAsync();
        await _audit.RecordAsync("Created hostel building",
            nameof(HostelBuilding), building.Id);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "SuperAdmin,Admin,PlacementOfficer")]
    public async Task<IActionResult> SchedulePlacementInterview(
        long placementApplicationId, string stage, DateTime scheduledUtc,
        string? location)
    {
        if (scheduledUtc <= DateTime.UtcNow ||
            !await _db.PlacementApplications.AnyAsync(a => a.Id == placementApplicationId))
            return BadRequest("Select an application and a future interview time.");
        var interview = new PlacementInterview
        {
            PlacementApplicationId = placementApplicationId,
            Stage = stage.Trim(),
            ScheduledUtc = scheduledUtc,
            Location = location?.Trim()
        };
        _db.PlacementInterviews.Add(interview);
        await _db.SaveChangesAsync();
        await _audit.RecordAsync("Scheduled placement interview",
            nameof(PlacementInterview), interview.Id);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "SuperAdmin,Admin")]
    public async Task<IActionResult> CreateEmployeeContract(
        int employeeProfileId, string contractType, DateTime startsOn,
        DateTime? endsOn, decimal baseSalary)
    {
        if (baseSalary < 0 || endsOn < startsOn ||
            !await _db.EmployeeProfiles.AnyAsync(e => e.Id == employeeProfileId))
            return BadRequest("Contract data is invalid.");
        var contract = new EmployeeContract
        {
            EmployeeProfileId = employeeProfileId,
            ContractType = contractType.Trim(),
            StartsOn = startsOn.Date,
            EndsOn = endsOn?.Date,
            BaseSalary = baseSalary
        };
        _db.EmployeeContracts.Add(contract);
        await _db.SaveChangesAsync();
        await _audit.RecordAsync("Created employee contract",
            nameof(EmployeeContract), contract.Id);
        return RedirectToAction(nameof(Index));
    }
}
