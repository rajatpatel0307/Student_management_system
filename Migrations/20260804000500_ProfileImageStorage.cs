using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Student_Management_System.Models;

#nullable disable

namespace Student_Management_System.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260804000500_ProfileImageStorage")]
public sealed class ProfileImageStorage : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF COL_LENGTH(N'dbo.AspNetUsers', N'ProfileImageStorageKey') IS NULL
                ALTER TABLE [dbo].[AspNetUsers]
                    ADD [ProfileImageStorageKey] nvarchar(500) NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Non-destructive rollback: retained keys preserve access to private files.
    }
}
