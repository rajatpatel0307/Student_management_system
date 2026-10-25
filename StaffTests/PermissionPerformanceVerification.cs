using System.Data.Common;
using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Student_Management_System.Services;
using Student_Management_System.Models;

internal static class PermissionPerformanceVerification
{
    public static readonly string[] Keys = ["admission.view", "fees.view", "marks.view", "attendance.view", "ManageDepartments", "ManageTeachers", "ManageStudents", "ManageAcademics", "reports.view", "academic.structure.manage", "student.lifecycle.manage", "communications.manage", "approvals.manage"];
    public static async Task RunAsync(string connection, string actorId, string label, Action<bool, string> check)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, actorId), new Claim(ClaimTypes.Role, "Admin")], "benchmark"));
        var meter = new QueryMeter();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(connection).AddInterceptors(meter).Options;
        async Task RenderPermissions()
        {
            await using var db = new ApplicationDbContext(options);
            var result = await new PermissionService(db).ReadForDisplayAsync(principal, Keys);
            check(result.Count == Keys.Length, "The navigation benchmark must resolve every requested permission.");
        }
        for (var i = 0; i < 3; i++) await RenderPermissions();
        meter.Count = 0; meter.Milliseconds = 0;
        using var process = Process.GetCurrentProcess();
        var cpuStart = process.TotalProcessorTime;
        var elapsed = new List<double>();
        for (var i = 0; i < 20; i++)
        {
            var watch = Stopwatch.StartNew(); await RenderPermissions(); elapsed.Add(watch.Elapsed.TotalMilliseconds);
        }
        var cpu = (process.TotalProcessorTime - cpuStart).TotalMilliseconds;
        process.Refresh();
        var sorted = elapsed.OrderBy(value => value).ToArray();
        var summary = new { label, workload = "20 sequential fresh DbContexts, 13 navigation decisions; 3 warm-ups excluded", requests = 20,
            sqlCommands = meter.Count, sqlDurationMs = meter.Milliseconds, medianMs = sorted[9], p95Ms = sorted[18], totalMs = elapsed.Sum(), cpuMs = cpu,
            processWorkingSetBytes = process.WorkingSet64, errors = 0, machine = Environment.MachineName, framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription };
        Directory.CreateDirectory(".build");
        await File.WriteAllTextAsync($".build/performance-phase4-{label}.json", JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"BENCHMARK {label}: {meter.Count} SQL commands, median {sorted[9]:F2}ms, p95 {sorted[18]:F2}ms.");
    }
    private sealed class QueryMeter : DbCommandInterceptor
    {
        public int Count; public double Milliseconds;
        public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
        { Count++; Milliseconds += eventData.Duration.TotalMilliseconds; return ValueTask.FromResult(result); }
    }
}
