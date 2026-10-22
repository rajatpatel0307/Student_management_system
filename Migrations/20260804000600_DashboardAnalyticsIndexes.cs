using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Student_Management_System.Models;

#nullable disable

namespace Student_Management_System.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260804000600_DashboardAnalyticsIndexes")]
public sealed class DashboardAnalyticsIndexes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Legacy databases stored Status as nvarchar(max), which cannot be an
        // index key. Validate before narrowing so no existing value is truncated.
        migrationBuilder.Sql("""
            IF EXISTS (SELECT 1 FROM [dbo].[Attendances] WHERE DATALENGTH([Status]) > 40)
                THROW 50001, 'Attendance status exceeds 20 characters. Correct the value before migrating.', 1;
            ALTER TABLE [dbo].[Attendances] ALTER COLUMN [Status] nvarchar(20) NOT NULL;
            """);
        migrationBuilder.CreateIndex(name: "IX_Attendances_Date_Status_StudentId", table: "Attendances", columns: new[] { "Date", "Status", "StudentId" });
        migrationBuilder.CreateIndex(name: "IX_AdmissionApplications_CampusId_CreatedUtc_Stage", table: "AdmissionApplications", columns: new[] { "CampusId", "CreatedUtc", "Stage" });
        migrationBuilder.CreateIndex(name: "IX_FinancialTransactions_CreatedUtc_Status_Type", table: "FinancialTransactions", columns: new[] { "CreatedUtc", "Status", "Type" });
        migrationBuilder.CreateIndex(name: "IX_LibraryLoans_IssuedUtc_ReturnedUtc_DueUtc", table: "LibraryLoans", columns: new[] { "IssuedUtc", "ReturnedUtc", "DueUtc" });
        migrationBuilder.CreateIndex(name: "IX_Assignments_Status_DueUtc_SubjectId", table: "Assignments", columns: new[] { "Status", "DueUtc", "SubjectId" });
        migrationBuilder.CreateIndex(name: "IX_LoginHistories_CreatedUtc_Succeeded", table: "LoginHistories", columns: new[] { "CreatedUtc", "Succeeded" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_Attendances_Date_Status_StudentId", table: "Attendances");
        migrationBuilder.DropIndex(name: "IX_AdmissionApplications_CampusId_CreatedUtc_Stage", table: "AdmissionApplications");
        migrationBuilder.DropIndex(name: "IX_FinancialTransactions_CreatedUtc_Status_Type", table: "FinancialTransactions");
        migrationBuilder.DropIndex(name: "IX_LibraryLoans_IssuedUtc_ReturnedUtc_DueUtc", table: "LibraryLoans");
        migrationBuilder.DropIndex(name: "IX_Assignments_Status_DueUtc_SubjectId", table: "Assignments");
        migrationBuilder.DropIndex(name: "IX_LoginHistories_CreatedUtc_Succeeded", table: "LoginHistories");
    }
}
