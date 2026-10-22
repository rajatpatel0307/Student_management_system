using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Student_Management_System.Models;

#nullable disable

namespace Student_Management_System.Migrations;

/// <summary>
/// Reconciles the legacy integer Marks.Score column with the decimal domain
/// model. Converting int to decimal(18,2) is lossless and preserves all marks.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260804000700_RepairMarkScorePrecision")]
public sealed class RepairMarkScorePrecision : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF OBJECT_ID(N'[dbo].[Marks]', N'U') IS NOT NULL
               AND COL_LENGTH(N'dbo.Marks', N'Score') IS NOT NULL
               AND EXISTS
               (
                   SELECT 1
                   FROM sys.columns columnInfo
                   INNER JOIN sys.types typeInfo
                       ON typeInfo.user_type_id = columnInfo.user_type_id
                   WHERE columnInfo.object_id = OBJECT_ID(N'[dbo].[Marks]')
                     AND columnInfo.name = N'Score'
                     AND
                     (
                         typeInfo.name <> N'decimal'
                         OR columnInfo.precision <> 18
                         OR columnInfo.scale <> 2
                     )
               )
                ALTER TABLE [dbo].[Marks]
                    ALTER COLUMN [Score] decimal(18,2) NOT NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Converting decimal values back to int could destroy fractional marks,
        // so rollback is intentionally non-destructive.
    }
}
