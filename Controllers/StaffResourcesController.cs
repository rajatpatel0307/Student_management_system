using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Student_Management_System.Models;
using Student_Management_System.Services;

namespace Student_Management_System.Controllers;

[Authorize, Route("api/staff-resources/{workspace}/{resource}")]
[ApiController]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class StaffResourcesController(ApplicationDbContext db, StaffAccessService access, AuditService audit,
    ICampusScopeService campusScope) : Controller
{
    [HttpGet]
    public async Task<IActionResult> List(string workspace, string resource, string? search, int page = 1)
    {
        if (!await access.AllowedAsync(User, workspace)) return Forbid();
        search = search?.Trim() ?? "";
        var campuses = await campusScope.AllowedCampusIdsAsync(User);
        return (workspace, resource) switch
        {
            ("librarian", "books" or "add-book" or "edit-book" or "inventory") => await Page(db.LibraryBooks.Where(b => !b.IsArchived && (search == "" || b.Title.Contains(search) || b.Author.Contains(search))), page,
                b => new { b.Id, b.Title, b.Author, b.Isbn, b.Category, b.Publisher, status = "Active" }),
            ("librarian", "categories") => await Page(db.LibraryCategories.Where(c => search == "" || c.Name.Contains(search)), page, c => new { c.Id, title = c.Name, c.Name, status = "Active" }),
            ("librarian", "authors") => await Page(db.LibraryAuthors.Where(c => search == "" || c.Name.Contains(search)), page, c => new { c.Id, title = c.Name, c.Name, status = "Active" }),
            ("librarian", "publishers") => await Page(db.LibraryPublishers.Where(c => search == "" || c.Name.Contains(search)), page, c => new { c.Id, title = c.Name, c.Name, status = "Active" }),
            ("transport", "vehicles" or "add-vehicle" or "edit-vehicle") => await Page(db.TransportVehicles.Where(v => search == "" || v.RegistrationNumber.Contains(search) || v.Model.Contains(search)), page,
                v => new { v.Id, title = v.RegistrationNumber, v.RegistrationNumber, v.Model, v.Capacity, v.IsActive, v.VehicleNumber, v.VehicleType, v.TransportDriverId, v.TransportRouteId, v.InsuranceExpiry, v.FitnessExpiry, v.PermitExpiry, v.MaintenanceStatus, rowVersion = Convert.ToBase64String(v.RowVersion), status = v.IsActive ? "Active" : "Inactive" }),
            ("transport", "drivers") => await Page(db.TransportDrivers.Where(d => search == "" || d.FullName.Contains(search) || d.LicenseNumber.Contains(search)), page,
                d => new { d.Id, title = d.FullName, d.FullName, d.Phone, d.LicenseNumber, d.LicenseExpiry, d.IsActive, rowVersion = Convert.ToBase64String(d.RowVersion), status = d.IsActive ? "Active" : "Inactive" }),
            ("transport", "routes") => await Page(db.TransportRoutes.Where(r => search == "" || r.Name.Contains(search) || r.Code.Contains(search)), page,
                r => new { r.Id, title = r.Name, r.Name, r.Code, r.Stops, r.IsActive, status = r.IsActive ? "Active" : "Inactive" }),
            ("transport", "stops") => await Page(db.TransportStops.Where(r => search == "" || r.Name.Contains(search)), page,
                r => new { r.Id, title = r.Name, r.Name, r.TransportRouteId, r.Sequence, status = "Active" }),
            ("transport", "trips") => await Page(db.TransportTrips.Where(t => search == "" || t.TransportVehicle.RegistrationNumber.Contains(search) || t.TransportRoute.Name.Contains(search)), page,
                t => new { t.Id, title = t.TransportVehicle.RegistrationNumber, t.TransportVehicleId, t.TransportDriverId, t.TransportRouteId, t.ScheduledOn, t.DepartureTime, t.Status, t.Notes, rowVersion = Convert.ToBase64String(t.RowVersion) }),
            ("transport", "fuel") => await Page(db.VehicleFuelLogs.Where(f => search == "" || f.TransportVehicle.RegistrationNumber.Contains(search) || f.Reference!.Contains(search)), page,
                f => new { f.Id, title = f.TransportVehicle.RegistrationNumber, f.TransportVehicleId, f.FilledOn, f.Litres, f.UnitPrice, totalCost = f.Litres * f.UnitPrice, f.OdometerKm, f.Reference, status = "Recorded", rowVersion = Convert.ToBase64String(f.RowVersion) }),
            ("transport" or "hostel", "attendance") => await Page(db.StaffAttendanceRecords.Where(a => a.Workspace == workspace && campuses.Contains(a.CampusId) && (search == "" || a.Student.FullName.Contains(search))), page,
                a => new { a.Id, title = a.Student.FullName, a.StudentId, a.AttendanceDate, a.Status, a.Notes, rowVersion = Convert.ToBase64String(a.RowVersion) }),
            ("registrar", "certificates" or "transfer-certificates" or "bonafide" or "id-cards") => await Page(db.RegistrarCertificates.Where(c => campuses.Contains(c.CampusId) && (resource == "certificates" || c.CertificateType == (resource == "bonafide" ? "Bonafide" : resource == "id-cards" ? "ID Card" : "Transfer")) && (search == "" || c.Student.FullName.Contains(search) || c.CertificateNumber!.Contains(search))), page,
                c => new { c.Id, title = c.Student.FullName, c.StudentId, c.CertificateType, c.CertificateNumber, c.Purpose, c.EvidenceReference, c.Status, c.ReviewComment, c.IssuedUtc, rowVersion = Convert.ToBase64String(c.RowVersion) }),
            ("hostel", "hostels" or "buildings") => await Page(db.HostelBuildings.Where(r => search == "" || r.Name.Contains(search)), page,
                r => new { r.Id, title = r.Name, r.Name, r.Address, r.IsActive, status = r.IsActive ? "Active" : "Inactive" }),
            ("hostel", "rooms") => await Page(db.HostelRooms.Where(r => search == "" || r.RoomNumber.Contains(search) || r.HostelName.Contains(search)), page,
                r => new { r.Id, title = r.RoomNumber, r.RoomNumber, r.HostelName, r.Capacity, r.IsActive, status = r.IsActive ? "Active" : "Inactive" }),
            ("hostel", "beds") => await Page(db.HostelBeds.Where(r => search == "" || r.BedNumber.Contains(search)), page,
                r => new { r.Id, title = r.BedNumber, r.BedNumber, r.HostelRoomId, r.IsActive, status = r.IsActive ? "Active" : "Inactive" }),
            ("accountant", "expenses") => await Page(db.StaffExpenses.Where(r => r.CampusId.HasValue && campuses.Contains(r.CampusId.Value) && (search == "" || r.Title.Contains(search) || r.Category.Contains(search))), page,
                r => new { r.Id, r.Title, r.Category, r.Vendor, r.Amount, r.ExpenseDate, r.Reference, r.Status, rowVersion = Convert.ToBase64String(r.RowVersion) }),
            ("registrar", "students" or "registration" or "academic-records" or "enrollment") => await Page(db.Students.Where(s => !s.IsArchived && (search == "" || s.FullName.Contains(search)) && s.UserId != null && db.UserCampusMemberships.Any(m => m.UserId == s.UserId && m.IsActive && campuses.Contains(m.CampusId))), page,
                s => new { s.Id, title = s.FullName, s.FullName, s.AdmissionNumber, s.RollNumber, s.ClassId, s.SectionId, status = s.StudentStatus }),
            ("registrar", "courses") => await Page(db.AcademicPrograms.Where(p => (search == "" || p.Name.Contains(search)) && p.CampusId.HasValue && campuses.Contains(p.CampusId.Value)), page,
                p => new { p.Id, title = p.Name, p.Code, p.Name, p.DurationSemesters, p.DepartmentId, p.IsActive, status = p.IsActive ? "Active" : "Inactive" }),
            ("registrar", "departments") => await Page(db.Departments.Where(d => !d.IsArchived && (search == "" || d.Name.Contains(search) || d.Code.Contains(search))), page,
                d => new { d.Id, title = d.Name, d.Name, d.Code, rowVersion = Convert.ToBase64String(d.RowVersion), status = "Active" }),
            _ => NotFound()
        };
    }

    private async Task<IActionResult> Page<T, TDto>(IQueryable<T> query, int page, System.Linq.Expressions.Expression<Func<T, TDto>> select) where T : class
    {
        var total = await query.CountAsync();
        page = Math.Clamp(page, 1, Math.Max(1, (int)Math.Ceiling(total / 10d)));
        return Json(new { total, page, items = await query.AsNoTracking().OrderBy(x => EF.Property<object>(x, "Id")).Skip((page - 1) * 10).Take(10).Select(select).ToListAsync() });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(string workspace, string resource, [FromBody] JsonElement input)
    {
        if (!await access.CanManageAsync(User, workspace)) return Forbid();
        try
        {
            var id = 0;
            if (input.TryGetProperty("id", out var idValue) && (!idValue.TryGetInt32(out id) || id < 0))
                return BadRequest(new { message = "A valid resource ID is required." });
            string Text(string name, int max = 160, bool required = true)
            {
                var value = input.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String ? prop.GetString()?.Trim() ?? "" : "";
                if ((required && value.Length == 0) || value.Length > max) throw new StaffValidationException($"Provide a valid {name} (maximum {max} characters).");
                return value;
            }
            int Int(string name, int max = 1000000)
            {
                var value = input.TryGetProperty(name, out var prop) && prop.TryGetInt32(out var n) ? n : 0;
                if (value < 1 || value > max) throw new StaffValidationException($"Provide a valid {name} between 1 and {max}.");
                return value;
            }
            int? OptionalInt(string name) => input.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.Number ? Int(name) : null;
            bool Active() => !input.TryGetProperty("isActive", out var prop) || prop.ValueKind == JsonValueKind.True;
            DateTime? Date(string name, bool required = false)
            {
                var value = Text(name, 40, required);
                if (value.Length == 0) return null;
                if (!DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) || date.Year < 1900) throw new StaffValidationException($"Provide a valid {name}.");
                return date.Date;
            }
            object entity;
            switch (workspace, resource)
            {
                case ("librarian", "books" or "add-book" or "edit-book" or "inventory"):
                    var book = id == 0 ? new LibraryBook() : await db.LibraryBooks.SingleOrDefaultAsync(b => b.Id == id && !b.IsArchived) ?? throw new KeyNotFoundException();
                    book.Title = Text("title", 200); book.Author = Text("author"); book.Isbn = Text("isbn", 20, false); book.Category = Text("category", 80, false); book.Publisher = Text("publisher", 80, false);
                    if (book.Isbn.Length == 0) book.Isbn = null;
                    if (book.Isbn != null && await db.LibraryBooks.AnyAsync(b => b.Id != id && b.Isbn == book.Isbn)) throw new StaffValidationException("This ISBN already exists.");
                    book.LibraryCategoryId = await db.LibraryCategories.Where(c => c.Name == book.Category && c.IsActive).Select(c => (int?)c.Id).FirstOrDefaultAsync();
                    book.LibraryPublisherId = await db.LibraryPublishers.Where(p => p.Name == book.Publisher).Select(p => (int?)p.Id).FirstOrDefaultAsync();
                    if (id == 0)
                    {
                        db.LibraryBooks.Add(book);
                        var copies = Int("copyCount", 100);
                        for (var i = 0; i < copies; i++) book.Copies.Add(new LibraryBookCopy { Barcode = "BK-" + Guid.NewGuid().ToString("N"), ShelfLocation = Text("shelfLocation", 80, false) });
                    }
                    entity = book; break;
                case ("librarian", "categories"):
                    var category = id == 0 ? new LibraryCategory() : await db.LibraryCategories.FindAsync(id) ?? throw new KeyNotFoundException();
                    category.Name = Text("name", 80); if (id == 0) db.LibraryCategories.Add(category); entity = category; break;
                case ("librarian", "authors"):
                    var author = id == 0 ? new LibraryAuthor() : await db.LibraryAuthors.FindAsync(id) ?? throw new KeyNotFoundException();
                    author.Name = Text("name"); if (id == 0) db.LibraryAuthors.Add(author); entity = author; break;
                case ("librarian", "publishers"):
                    var publisher = id == 0 ? new LibraryPublisher() : await db.LibraryPublishers.FindAsync(id) ?? throw new KeyNotFoundException();
                    publisher.Name = Text("name"); if (id == 0) db.LibraryPublishers.Add(publisher); entity = publisher; break;
                case ("transport", "vehicles" or "add-vehicle" or "edit-vehicle"):
                    var vehicle = id == 0 ? new TransportVehicle() : await db.TransportVehicles.FindAsync(id) ?? throw new KeyNotFoundException();
                    vehicle.RegistrationNumber = Text("registrationNumber", 30).ToUpperInvariant(); vehicle.Model = Text("model", 80); vehicle.Capacity = Int("capacity", 200); vehicle.IsActive = Active();
                    vehicle.VehicleNumber = Text("vehicleNumber", 30, false); vehicle.VehicleType = Text("vehicleType", 40); vehicle.TransportDriverId = OptionalInt("transportDriverId"); vehicle.TransportRouteId = OptionalInt("transportRouteId");
                    if (vehicle.TransportDriverId.HasValue && !await db.TransportDrivers.AnyAsync(d => d.Id == vehicle.TransportDriverId && d.IsActive)) throw new StaffValidationException("Select an active driver.");
                    if (vehicle.TransportRouteId.HasValue && !await db.TransportRoutes.AnyAsync(r => r.Id == vehicle.TransportRouteId && r.IsActive)) throw new StaffValidationException("Select an active route.");
                    vehicle.InsuranceExpiry = Date("insuranceExpiry"); vehicle.FitnessExpiry = Date("fitnessExpiry"); vehicle.PermitExpiry = Date("permitExpiry"); vehicle.MaintenanceStatus = Text("maintenanceStatus", 40);
                    if (vehicle.MaintenanceStatus is not ("Ready" or "Due" or "In maintenance")) throw new StaffValidationException("Select a valid maintenance status.");
                    if (id == 0) db.TransportVehicles.Add(vehicle); entity = vehicle; break;
                case ("transport", "drivers"):
                    var driver = id == 0 ? new TransportDriver() : await db.TransportDrivers.FindAsync(id) ?? throw new KeyNotFoundException();
                    driver.FullName = Text("fullName"); driver.Phone = Text("phone", 30); driver.LicenseNumber = Text("licenseNumber", 80).ToUpperInvariant(); driver.LicenseExpiry = Date("licenseExpiry", true)!.Value; driver.IsActive = Active(); driver.UpdatedUtc = DateTime.UtcNow;
                    if (id == 0) db.TransportDrivers.Add(driver); entity = driver; break;
                case ("transport", "routes"):
                    var route = id == 0 ? new TransportRoute() : await db.TransportRoutes.FindAsync(id) ?? throw new KeyNotFoundException();
                    route.Name = Text("name"); route.Code = Text("code", 30).ToUpperInvariant(); route.Stops = Text("stops", 1000); route.IsActive = Active(); if (id == 0) db.TransportRoutes.Add(route); entity = route; break;
                case ("transport", "stops"):
                    var stop = id == 0 ? new TransportStop() : await db.TransportStops.FindAsync(id) ?? throw new KeyNotFoundException();
                    stop.Name = Text("name", 120); stop.Sequence = Int("sequence", 1000); stop.TransportRouteId = Int("transportRouteId");
                    if (!await db.TransportRoutes.AnyAsync(r => r.Id == stop.TransportRouteId && r.IsActive)) throw new StaffValidationException("Select an active route.");
                    if (id == 0) db.TransportStops.Add(stop); entity = stop; break;
                case ("hostel", "hostels" or "buildings"):
                    var building = id == 0 ? new HostelBuilding() : await db.HostelBuildings.FindAsync(id) ?? throw new KeyNotFoundException();
                    building.Name = Text("name", 120); building.Address = Text("address", 500, false); building.IsActive = Active(); if (id == 0) db.HostelBuildings.Add(building); entity = building; break;
                case ("hostel", "rooms"):
                    var room = id == 0 ? new HostelRoom() : await db.HostelRooms.FindAsync(id) ?? throw new KeyNotFoundException();
                    room.HostelName = Text("hostelName", 80); room.RoomNumber = Text("roomNumber", 30); room.Capacity = Int("capacity", 20); room.IsActive = Active();
                    if (id != 0 && await db.HostelAllocations.CountAsync(a => a.HostelRoomId == id && a.EndsOn == null) > room.Capacity) throw new StaffValidationException("Capacity cannot be lower than current occupancy.");
                    if (id == 0) db.HostelRooms.Add(room); entity = room; break;
                case ("hostel", "beds"):
                    var bed = id == 0 ? new HostelBed() : await db.HostelBeds.FindAsync(id) ?? throw new KeyNotFoundException();
                    bed.BedNumber = Text("bedNumber", 30); bed.HostelRoomId = Int("hostelRoomId"); bed.IsActive = Active();
                    if (!await db.HostelRooms.AnyAsync(r => r.Id == bed.HostelRoomId && r.IsActive)) throw new StaffValidationException("Select an active room.");
                    if (id == 0) db.HostelBeds.Add(bed); entity = bed; break;
                case ("accountant", "expenses"):
                    var campuses = await campusScope.AllowedCampusIdsAsync(User);
                    if (campuses.Length == 0) throw new StaffValidationException("An active campus membership is required to record expenses.");
                    var expense = id == 0 ? new StaffExpense { CreatedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!, CampusId = await campusScope.RequireDefaultCampusIdAsync(User) } : await db.StaffExpenses.SingleOrDefaultAsync(e => e.Id == id && e.CampusId.HasValue && campuses.Contains(e.CampusId.Value)) ?? throw new KeyNotFoundException();
                    if (expense.Status != "Draft") throw new StaffValidationException("Approved expenses cannot be edited.");
                    expense.Title = Text("title"); expense.Category = Text("category", 80); expense.Vendor = Text("vendor", 160, false); expense.Reference = Text("reference", 100, false); expense.ExpenseDate = Date("expenseDate", true)!.Value;
                    if (!input.TryGetProperty("amount", out var amount) || !amount.TryGetDecimal(out var money) || money is <= 0 or > 10000000) throw new StaffValidationException("Amount must be between 0.01 and 10,000,000.");
                    expense.Amount = money; expense.UpdatedUtc = DateTime.UtcNow;
                    if (id == 0) db.StaffExpenses.Add(expense); entity = expense; break;
                case ("registrar", "certificates" or "transfer-certificates" or "bonafide" or "id-cards"):
                    var allowedCampuses = await campusScope.AllowedCampusIdsAsync(User);
                    var studentId = Int("studentId");
                    var studentCampus = await db.UserCampusMemberships.Where(m => m.IsActive && allowedCampuses.Contains(m.CampusId) && db.Students.Any(s => s.Id == studentId && !s.IsArchived && s.UserId == m.UserId)).OrderByDescending(m => m.IsDefault).Select(m => (int?)m.CampusId).FirstOrDefaultAsync();
                    if (!studentCampus.HasValue) throw new StaffValidationException("Select an active student within your campus access.");
                    var certificate = id == 0 ? new RegistrarCertificate { CampusId = studentCampus.Value, CreatedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)! } : await db.RegistrarCertificates.SingleOrDefaultAsync(c => c.Id == id && allowedCampuses.Contains(c.CampusId)) ?? throw new KeyNotFoundException();
                    if (certificate.Status != "Draft") throw new StaffValidationException("Only draft certificates can be edited.");
                    certificate.StudentId = studentId; certificate.CampusId = studentCampus.Value; certificate.CertificateType = Text("certificateType", 40);
                    if (certificate.CertificateType is not ("Bonafide" or "Transfer" or "Enrollment" or "ID Card")) throw new StaffValidationException("Select a supported certificate type.");
                    certificate.Purpose = Text("purpose", 1000); certificate.EvidenceReference = Text("evidenceReference", 160, false); certificate.UpdatedUtc = DateTime.UtcNow;
                    if (id == 0) db.RegistrarCertificates.Add(certificate); entity = certificate; break;
                case ("registrar", "courses"):
                    var courseCampuses = await campusScope.AllowedCampusIdsAsync(User);
                    if (courseCampuses.Length == 0) throw new StaffValidationException("An active campus membership is required.");
                    var course = id == 0 ? new AcademicProgram { CampusId = await campusScope.RequireDefaultCampusIdAsync(User) } : await db.AcademicPrograms.SingleOrDefaultAsync(p => p.Id == id && p.CampusId.HasValue && courseCampuses.Contains(p.CampusId.Value)) ?? throw new KeyNotFoundException();
                    course.Code = Text("code", 30).ToUpperInvariant(); course.Name = Text("name"); course.DurationSemesters = Int("durationSemesters", 12); course.DepartmentId = Int("departmentId"); course.IsActive = Active();
                    if (!await db.Departments.AnyAsync(d => d.Id == course.DepartmentId && !d.IsArchived)) throw new StaffValidationException("Select an active department.");
                    if (id == 0) db.AcademicPrograms.Add(course); entity = course; break;
                case ("registrar", "departments"):
                    var department = id == 0 ? new Department() : await db.Departments.SingleOrDefaultAsync(d => d.Id == id && !d.IsArchived) ?? throw new KeyNotFoundException();
                    department.Name = Text("name", 120); department.Code = Text("code", 20).ToUpperInvariant();
                    if (id == 0) db.Departments.Add(department); entity = department; break;
                case ("transport" or "hostel", "attendance"):
                    var attendanceCampuses = await campusScope.AllowedCampusIdsAsync(User);
                    var attendanceStudentId = Int("studentId");
                    var attendanceCampus = await db.UserCampusMemberships.Where(m => m.IsActive && attendanceCampuses.Contains(m.CampusId) && db.Students.Any(s => s.Id == attendanceStudentId && !s.IsArchived && s.UserId == m.UserId)).OrderByDescending(m => m.IsDefault).Select(m => (int?)m.CampusId).FirstOrDefaultAsync();
                    if (!attendanceCampus.HasValue) throw new StaffValidationException("Select an active student within your campus access.");
                    var attendanceDate = Date("attendanceDate", true)!.Value;
                    if (attendanceDate > DateTime.Today) throw new StaffValidationException("Attendance cannot be recorded in advance.");
                    var allocated = workspace == "hostel" ? await db.HostelAllocations.AnyAsync(a => a.StudentId == attendanceStudentId && a.StartsOn <= attendanceDate && (a.EndsOn == null || a.EndsOn >= attendanceDate))
                        : await db.TransportEnrollments.AnyAsync(a => a.StudentId == attendanceStudentId && a.StartsOn <= attendanceDate && (a.EndsOn == null || a.EndsOn >= attendanceDate));
                    if (!allocated) throw new StaffValidationException("The student must have an active allocation on the attendance date.");
                    var attendance = id == 0 ? new StaffAttendanceRecord { Workspace = workspace } : await db.StaffAttendanceRecords.SingleOrDefaultAsync(a => a.Id == id && a.Workspace == workspace && attendanceCampuses.Contains(a.CampusId)) ?? throw new KeyNotFoundException();
                    attendance.CampusId = attendanceCampus.Value; attendance.StudentId = attendanceStudentId; attendance.AttendanceDate = attendanceDate; attendance.Status = Text("status", 20);
                    if (attendance.Status is not ("Present" or "Absent" or "Late" or "On leave")) throw new StaffValidationException("Select a valid attendance status.");
                    attendance.Notes = Text("notes", 500, false); attendance.RecordedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!; attendance.UpdatedUtc = DateTime.UtcNow;
                    if (id == 0) db.StaffAttendanceRecords.Add(attendance); entity = attendance; break;
                case ("transport", "trips"):
                    var trip = id == 0 ? new TransportTrip { CreatedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)! } : await db.TransportTrips.FindAsync((long)id) ?? throw new KeyNotFoundException();
                    if (trip.Status is "Completed" or "Cancelled") throw new StaffValidationException("Completed or cancelled trips cannot be edited.");
                    var tripStatus = Text("status", 20);
                    if (!(id == 0 && tripStatus == "Planned" || id != 0 && (trip.Status == tripStatus || trip.Status == "Planned" && tripStatus is "In progress" or "Cancelled" || trip.Status == "In progress" && tripStatus is "Completed" or "Cancelled")))
                        throw new StaffValidationException("Plan a trip before starting it, then complete or cancel it.");
                    trip.TransportVehicleId = Int("transportVehicleId"); trip.TransportDriverId = Int("transportDriverId"); trip.TransportRouteId = Int("transportRouteId"); trip.ScheduledOn = Date("scheduledOn", true)!.Value;
                    trip.DepartureTime = Text("departureTime", 5);
                    if (!TimeOnly.TryParseExact(trip.DepartureTime, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)) throw new StaffValidationException("Departure time must use HH:mm format.");
                    var tripVehicle = await db.TransportVehicles.SingleOrDefaultAsync(v => v.Id == trip.TransportVehicleId && v.IsActive);
                    var tripDriver = await db.TransportDrivers.SingleOrDefaultAsync(d => d.Id == trip.TransportDriverId && d.IsActive && d.LicenseExpiry >= trip.ScheduledOn);
                    if (tripVehicle == null || tripDriver == null || !await db.TransportRoutes.AnyAsync(r => r.Id == trip.TransportRouteId && r.IsActive)) throw new StaffValidationException("Select an active vehicle, licensed driver and route.");
                    if (tripStatus == "In progress" && (tripVehicle.MaintenanceStatus != "Ready" || tripVehicle.InsuranceExpiry == null || tripVehicle.InsuranceExpiry < trip.ScheduledOn || tripVehicle.FitnessExpiry == null || tripVehicle.FitnessExpiry < trip.ScheduledOn || tripVehicle.PermitExpiry == null || tripVehicle.PermitExpiry < trip.ScheduledOn))
                        throw new StaffValidationException("A trip can start only with valid insurance, fitness, permit and a maintenance-ready vehicle.");
                    if (await db.TransportTrips.AnyAsync(t => t.Id != id && t.Status != "Cancelled" && t.ScheduledOn == trip.ScheduledOn && t.DepartureTime == trip.DepartureTime && (t.TransportVehicleId == trip.TransportVehicleId || t.TransportDriverId == trip.TransportDriverId))) throw new StaffValidationException("The vehicle or driver is already scheduled at this departure time.");
                    trip.Status = tripStatus; trip.Notes = Text("notes", 1000, false);
                    if (id == 0) db.TransportTrips.Add(trip); entity = trip; break;
                case ("transport", "fuel"):
                    var fuel = id == 0 ? new VehicleFuelLog { RecordedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)! } : await db.VehicleFuelLogs.FindAsync((long)id) ?? throw new KeyNotFoundException();
                    fuel.TransportVehicleId = Int("transportVehicleId");
                    if (!await db.TransportVehicles.AnyAsync(v => v.Id == fuel.TransportVehicleId && v.IsActive)) throw new StaffValidationException("Select an active vehicle.");
                    fuel.FilledOn = Date("filledOn", true)!.Value;
                    if (fuel.FilledOn > DateTime.Today) throw new StaffValidationException("Fuel logs cannot be recorded in advance.");
                    if (!input.TryGetProperty("litres", out var litres) || !litres.TryGetDecimal(out var fuelLitres) || fuelLitres is <= 0 or > 1000 || !input.TryGetProperty("unitPrice", out var price) || !price.TryGetDecimal(out var unitPrice) || unitPrice is <= 0 or > 1000) throw new StaffValidationException("Provide positive litres and unit price, each no greater than 1,000.");
                    if (decimal.Round(fuelLitres, 2) != fuelLitres || decimal.Round(unitPrice, 2) != unitPrice) throw new StaffValidationException("Litres and unit price support up to two decimal places.");
                    fuel.Litres = fuelLitres; fuel.UnitPrice = unitPrice; fuel.OdometerKm = Int("odometerKm", 10000000); fuel.Reference = Text("reference", 100, false);
                    var previousOdometer = await db.VehicleFuelLogs.Where(f => f.Id != id && f.TransportVehicleId == fuel.TransportVehicleId && f.FilledOn < fuel.FilledOn).MaxAsync(f => (int?)f.OdometerKm) ?? 0;
                    var nextOdometer = await db.VehicleFuelLogs.Where(f => f.Id != id && f.TransportVehicleId == fuel.TransportVehicleId && f.FilledOn > fuel.FilledOn).MinAsync(f => (int?)f.OdometerKm);
                    if (fuel.OdometerKm < previousOdometer || nextOdometer.HasValue && fuel.OdometerKm > nextOdometer) throw new StaffValidationException("Odometer must agree with the vehicle's chronological fuel history.");
                    if (id == 0) db.VehicleFuelLogs.Add(fuel); entity = fuel; break;
                default: return NotFound();
            }
            var entry = db.Entry(entity);
            if (id != 0 && entry.Metadata.FindProperty("RowVersion") != null)
            {
                var version = Convert.FromBase64String(Text("rowVersion", 100));
                if (!version.SequenceEqual((byte[])entry.Property("RowVersion").CurrentValue!)) return Conflict(new { message = "This record changed. Refresh before saving." });
                entry.Property("RowVersion").OriginalValue = version;
            }
            var validation = new List<ValidationResult>();
            if (!Validator.TryValidateObject(entity, new ValidationContext(entity), validation, true)) return BadRequest(new { message = string.Join(" ", validation.Select(v => v.ErrorMessage)) });
            audit.Add(id == 0 ? "Created workspace resource" : "Updated workspace resource", entity.GetType().Name, id == 0 ? null : id,
                detailsJson: JsonSerializer.Serialize(new { workspace, resource }));
            await db.SaveChangesAsync();
            return Ok();
        }
        catch (StaffValidationException e) { return BadRequest(new { message = e.Message }); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (FormatException) { return BadRequest(new { message = "The record version is invalid. Refresh and try again." }); }
        catch (InvalidOperationException) { return BadRequest(new { message = "A field has an invalid type." }); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { message = "This record changed. Refresh and try again." }); }
        catch (DbUpdateException) { return Conflict(new { message = "A unique value already exists or a related record changed. Review the form and try again." }); }
    }

    [HttpPost("{id:long}/approve"), ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveExpense(string workspace, string resource, long id, [FromBody] ReviewWorkInput input)
    {
        if (workspace != "accountant" || resource != "expenses" || !await access.CanManageAsync(User, workspace)) return Forbid();
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var campuses = await campusScope.AllowedCampusIdsAsync(User);
        var expense = await db.StaffExpenses.SingleOrDefaultAsync(e => e.Id == id && e.CampusId.HasValue && campuses.Contains(e.CampusId.Value));
        if (expense == null) return NotFound();
        if (expense.Status != "Draft") return Conflict(new { message = "This expense is already approved." });
        try { if (!Convert.FromBase64String(input.RowVersion).SequenceEqual(expense.RowVersion)) return Conflict(); }
        catch (FormatException) { return BadRequest(); }
        if (expense.CreatedByUserId == User.FindFirstValue(ClaimTypes.NameIdentifier)) return BadRequest(new { message = "A different authorized accountant or administrator must approve this expense." });
        expense.Status = "Approved"; expense.ApprovedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier); expense.UpdatedUtc = DateTime.UtcNow;
        audit.Add("Approved expense", nameof(StaffExpense), id, detailsJson: JsonSerializer.Serialize(new { input.Comment }));
        try { await db.SaveChangesAsync(); } catch (DbUpdateConcurrencyException) { return Conflict(); }
        return Ok();
    }
}
