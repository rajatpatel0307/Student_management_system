using Microsoft.EntityFrameworkCore;
using Student_Management_System.Models;

namespace Student_Management_System.Services;

public static class StaffModuleSetup
{
    public static readonly string[] Migrations = ["20261023122648_StaffManagementWorkspaces", "20261023124049_StaffOperationalRegisters", "20261024000100_StaffIdentityCompatibility", "20261024000200_StaffComputedMobileMapping"];

    // Called only on explicitly configured Development startup. Never apply
    // unrelated migrations or create/reset user credentials in this repair.
    public static async Task ApplyPendingDevelopmentMigrationsAsync(ApplicationDbContext db)
    {
        var pending = (await db.Database.GetPendingMigrationsAsync()).ToArray();
        if (pending.Length > 0 && pending.All(Migrations.Contains)) await db.Database.MigrateAsync();
    }
}
