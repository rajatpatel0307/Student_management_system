using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Student_Management_System.Models;

namespace Student_Management_System.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261024000200_StaffComputedMobileMapping")]
public sealed class StaffComputedMobileMapping : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        const string function = """
            CREATE FUNCTION dbo.NormalizeUserMobile(@input nvarchar(max))
            RETURNS nvarchar(16) WITH SCHEMABINDING
            AS
            BEGIN
                DECLARE @v nvarchar(max) = @input;
                SET @v = REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(@v,N' ',N''),N'-',N''),N'(',N''),N')',N''),N'.',N''),NCHAR(9),N''),NCHAR(10),N''),NCHAR(13),N'');
                IF LEFT(@v,2)=N'00' SET @v=N'+'+SUBSTRING(@v,3,LEN(@v));
                IF LEN(@v)=10 AND @v COLLATE Latin1_General_100_BIN2 NOT LIKE N'%[^0-9]%' SET @v=N'+91'+@v;
                ELSE IF LEN(@v)=12 AND LEFT(@v,2)=N'91' AND @v COLLATE Latin1_General_100_BIN2 NOT LIKE N'%[^0-9]%' SET @v=N'+'+@v;
                IF @v IS NULL OR LEN(@v)<9 OR LEN(@v)>16 OR LEFT(@v,1)<>N'+' OR SUBSTRING(@v,2,1)=N'0' OR SUBSTRING(@v,2,16) COLLATE Latin1_General_100_BIN2 LIKE N'%[^0-9]%' RETURN NULL;
                IF LEFT(@v,3)=N'+91' AND (LEN(@v)<>13 OR SUBSTRING(@v,4,1) COLLATE Latin1_General_100_BIN2 NOT LIKE N'[6-9]') RETURN NULL;
                RETURN CONVERT(nvarchar(16),@v);
            END;
            """;
        // Preserve an institution's existing function, computed column and indexes.
        migrationBuilder.Sql("IF OBJECT_ID(N'dbo.NormalizeUserMobile', N'FN') IS NULL EXEC(N'" + function.Replace("'", "''") + "');");
        migrationBuilder.Sql("""
            IF EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AspNetUsers') AND name=N'NormalizedMobileNumber' AND is_computed=0)
            BEGIN
                ALTER TABLE dbo.AspNetUsers DROP COLUMN NormalizedMobileNumber;
                ALTER TABLE dbo.AspNetUsers ADD NormalizedMobileNumber AS ([dbo].[NormalizeUserMobile]([PhoneNumber])) PERSISTED;
            END
            ELSE IF COL_LENGTH(N'dbo.AspNetUsers', N'NormalizedMobileNumber') IS NULL
                ALTER TABLE dbo.AspNetUsers ADD NormalizedMobileNumber AS ([dbo].[NormalizeUserMobile]([PhoneNumber])) PERSISTED;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // The shared normalization function and generated values remain in place.
    }
}
