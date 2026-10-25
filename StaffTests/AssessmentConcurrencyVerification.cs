using Microsoft.EntityFrameworkCore;
using Student_Management_System.Models;

internal static class AssessmentConcurrencyVerification
{
    public static async Task RunAsync(string connection, int studentId, int classId, Action<bool, string> check)
    {
        ApplicationDbContext Open() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(connection).Options);
        await using var seed = Open();
        var subject = new Subject { Name = "Concurrency assessment", ClassId = classId };
        var exam = new Exam { Name = "Concurrency exam", ExamDate = DateTime.Today, Status = ExamLifecycle.OpenForEntry };
        var mark = new Mark { StudentId = studentId, Subject = subject, Exam = exam, Score = 45, Grade = "F" };
        seed.Marks.Add(mark);
        await seed.SaveChangesAsync();

        async Task Conflict(ApplicationDbContext context, string message)
        {
            try { await context.SaveChangesAsync(); check(false, message); }
            catch (DbUpdateConcurrencyException) { check(true, message); }
        }

        await using (var approver = Open())
        await using (var editor = Open())
        {
            var approved = await approver.Marks.SingleAsync(m => m.Id == mark.Id);
            var edited = await editor.Marks.SingleAsync(m => m.Id == mark.Id);
            approved.IsApproved = true; approved.UpdatedUtc = DateTime.UtcNow;
            await approver.SaveChangesAsync();
            edited.Score = 99; edited.UpdatedUtc = DateTime.UtcNow;
            await Conflict(editor, "An edit loaded before approval must fail after approval commits.");
            var stored = await seed.Marks.AsNoTracking().SingleAsync(m => m.Id == mark.Id);
            check(stored.IsApproved && stored.Score == 45, "Concurrent editing must preserve the approved score in SQL.");
        }
        await using (var reset = Open())
        {
            var current = await reset.Marks.SingleAsync(m => m.Id == mark.Id);
            current.IsApproved = false; current.UpdatedUtc = DateTime.UtcNow;
            await reset.SaveChangesAsync();
        }
        await using (var editor = Open())
        await using (var approver = Open())
        {
            var edited = await editor.Marks.SingleAsync(m => m.Id == mark.Id);
            var approved = await approver.Marks.SingleAsync(m => m.Id == mark.Id);
            edited.Score = 62; edited.UpdatedUtc = DateTime.UtcNow;
            await editor.SaveChangesAsync();
            approved.IsApproved = true; approved.UpdatedUtc = DateTime.UtcNow;
            await Conflict(approver, "Approval must not approve a score changed after the reviewer loaded it.");
        }
        await using (var first = Open())
        await using (var second = Open())
        {
            var one = await first.Marks.SingleAsync(m => m.Id == mark.Id);
            var two = await second.Marks.SingleAsync(m => m.Id == mark.Id);
            one.Remarks = "First edit"; one.UpdatedUtc = DateTime.UtcNow;
            await first.SaveChangesAsync();
            two.Remarks = "Stale edit"; two.UpdatedUtc = DateTime.UtcNow;
            await Conflict(second, "Concurrent remarks-only edits must not silently overwrite one another.");
        }
        var correction = new ResultCorrectionRequest { MarkId = mark.Id, PreviousScore = 62, RequestedScore = 70, Reason = "Fixture", RequestedByUserId = "test-reviewer" };
        seed.ResultCorrectionRequests.Add(correction);
        await seed.SaveChangesAsync();
        await using (var first = Open())
        await using (var second = Open())
        {
            var one = await first.ResultCorrectionRequests.SingleAsync(r => r.Id == correction.Id);
            var two = await second.ResultCorrectionRequests.SingleAsync(r => r.Id == correction.Id);
            one.Status = ApprovalStatus.Rejected;
            await first.SaveChangesAsync();
            two.Status = ApprovalStatus.Approved;
            await Conflict(second, "Two correction reviewers must not overwrite the first completed decision.");
        }
    }
}
