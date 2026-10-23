using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Student_Management_System.Models;

namespace Student_Management_System.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261024000100_StaffIdentityCompatibility")]
public sealed class StaffIdentityCompatibility : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Some deployed databases already enforce these fields through Identity
        // guard triggers. Adopt those columns without changing their data/rules.
        migrationBuilder.Sql("""
            IF COL_LENGTH(N'dbo.AspNetUsers', N'PublicUserId') IS NULL
                ALTER TABLE [dbo].[AspNetUsers] ADD [PublicUserId] nvarchar(40) NULL;
            IF COL_LENGTH(N'dbo.AspNetUsers', N'NormalizedMobileNumber') IS NULL
                ALTER TABLE [dbo].[AspNetUsers] ADD [NormalizedMobileNumber] nvarchar(16) NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Keep identity data and any pre-existing guard triggers intact.
    }
}
