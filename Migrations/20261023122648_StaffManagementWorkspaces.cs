using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Student_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class StaffManagementWorkspaces : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FitnessExpiry",
                table: "TransportVehicles",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InsuranceExpiry",
                table: "TransportVehicles",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MaintenanceStatus",
                table: "TransportVehicles",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Ready");

            migrationBuilder.AddColumn<DateTime>(
                name: "PermitExpiry",
                table: "TransportVehicles",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "TransportVehicles",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<int>(
                name: "TransportDriverId",
                table: "TransportVehicles",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TransportRouteId",
                table: "TransportVehicles",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VehicleNumber",
                table: "TransportVehicles",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VehicleType",
                table: "TransportVehicles",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StaffExpenses",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CampusId = table.Column<int>(type: "int", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Vendor = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ExpenseDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ApprovedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffExpenses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StaffExpenses_Campuses_CampusId",
                        column: x => x.CampusId,
                        principalTable: "Campuses",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "StaffMembers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeProfileId = table.Column<int>(type: "int", nullable: false),
                    PrimaryRole = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Gender = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    DateOfBirth = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Address = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    EmergencyContact = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StaffMembers_EmployeeProfiles_EmployeeProfileId",
                        column: x => x.EmployeeProfileId,
                        principalTable: "EmployeeProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StaffMessages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SenderUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    RecipientUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReadUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StaffMessages_AspNetUsers_RecipientUserId",
                        column: x => x.RecipientUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StaffMessages_AspNetUsers_SenderUserId",
                        column: x => x.SenderUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StaffWorkItems",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CampusId = table.Column<int>(type: "int", nullable: true),
                    Workspace = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    StudentId = table.Column<int>(type: "int", nullable: true),
                    Reference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DueOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ReviewComment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ReviewedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffWorkItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StaffWorkItems_Campuses_CampusId",
                        column: x => x.CampusId,
                        principalTable: "Campuses",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StaffWorkItems_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TransportDrivers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FullName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    LicenseNumber = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    LicenseExpiry = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransportDrivers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TransportVehicles_TransportDriverId",
                table: "TransportVehicles",
                column: "TransportDriverId");

            migrationBuilder.CreateIndex(
                name: "IX_TransportVehicles_TransportRouteId",
                table: "TransportVehicles",
                column: "TransportRouteId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffExpenses_CampusId",
                table: "StaffExpenses",
                column: "CampusId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffExpenses_Status_ExpenseDate",
                table: "StaffExpenses",
                columns: new[] { "Status", "ExpenseDate" });

            migrationBuilder.CreateIndex(
                name: "IX_StaffMembers_EmployeeProfileId",
                table: "StaffMembers",
                column: "EmployeeProfileId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StaffMembers_PrimaryRole_IsDeleted",
                table: "StaffMembers",
                columns: new[] { "PrimaryRole", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_StaffMessages_RecipientUserId_ReadUtc_CreatedUtc",
                table: "StaffMessages",
                columns: new[] { "RecipientUserId", "ReadUtc", "CreatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_StaffMessages_SenderUserId",
                table: "StaffMessages",
                column: "SenderUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffWorkItems_CampusId",
                table: "StaffWorkItems",
                column: "CampusId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffWorkItems_StudentId",
                table: "StaffWorkItems",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffWorkItems_Workspace_Category_Status",
                table: "StaffWorkItems",
                columns: new[] { "Workspace", "Category", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TransportDrivers_LicenseNumber",
                table: "TransportDrivers",
                column: "LicenseNumber",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_TransportVehicles_TransportDrivers_TransportDriverId",
                table: "TransportVehicles",
                column: "TransportDriverId",
                principalTable: "TransportDrivers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TransportVehicles_TransportRoutes_TransportRouteId",
                table: "TransportVehicles",
                column: "TransportRouteId",
                principalTable: "TransportRoutes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Existing installations receive the catalog without rerunning the
            // application bootstrap or resetting administrator permission choices.
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM [AspNetRoles] WHERE [NormalizedName] = N'REGISTRAR')
                    INSERT [AspNetRoles] ([Id], [Name], [NormalizedName], [ConcurrencyStamp])
                    VALUES (CONVERT(nvarchar(36), NEWID()), N'Registrar', N'REGISTRAR', CONVERT(nvarchar(36), NEWID()));
                INSERT [Permissions] ([Key], [Description])
                SELECT v.[Key], v.[Description] FROM (VALUES
                    (N'staff.manage', N'Manage operational staff accounts and permissions'),
                    (N'transport.manage', N'Manage transport workspace operations'),
                    (N'hostel.manage', N'Manage hostel workspace operations'),
                    (N'registrar.manage', N'Manage registrar requests and records'),
                    (N'finance.expenses.manage', N'Record and approve institutional expenses'),
                    (N'workspace.librarian', N'Access the library staff workspace'),
                    (N'workspace.transport', N'Access the transport staff workspace'),
                    (N'workspace.hostel', N'Access the hostel staff workspace'),
                    (N'workspace.registrar', N'Access the registrar staff workspace'),
                    (N'workspace.accountant', N'Access the finance staff workspace')
                ) v([Key], [Description]) WHERE NOT EXISTS (SELECT 1 FROM [Permissions] p WHERE p.[Key] = v.[Key]);
                INSERT [RolePermissions] ([RoleName], [PermissionId])
                SELECT N'Registrar', p.[Id] FROM [Permissions] p
                WHERE p.[Key] IN (N'registrar.manage', N'admission.view', N'admission.create', N'admission.review', N'admission.approve', N'documents.generate', N'profile.image.edit-self', N'dashboard.view')
                AND NOT EXISTS (SELECT 1 FROM [RolePermissions] rp WHERE rp.[RoleName] = N'Registrar' AND rp.[PermissionId] = p.[Id]);
                INSERT [RolePermissions] ([RoleName], [PermissionId])
                SELECT v.[RoleName], p.[Id] FROM (VALUES
                    (N'Admin', N'staff.manage'), (N'Admin', N'transport.manage'),
                    (N'Admin', N'hostel.manage'), (N'Admin', N'registrar.manage'),
                    (N'Admin', N'finance.expenses.manage'), (N'Admin', N'library.manage'),
                    (N'TransportManager', N'transport.manage'), (N'HostelManager', N'hostel.manage'),
                    (N'Accountant', N'finance.expenses.manage')
                ) v([RoleName], [Key]) JOIN [Permissions] p ON p.[Key] = v.[Key]
                WHERE NOT EXISTS (SELECT 1 FROM [RolePermissions] rp WHERE rp.[RoleName] = v.[RoleName] AND rp.[PermissionId] = p.[Id]);

                -- Import accounts with exactly one staff role. Mixed/privileged
                -- legacy accounts require explicit administrator reconciliation.
                INSERT [EmployeeProfiles] ([UserId], [EmployeeNumber], [JobTitle], [JoinedOn], [IsActive])
                SELECT u.[Id], N'STF-' + LEFT(CONVERT(varchar(64), HASHBYTES('SHA2_256', u.[Id]), 2), 32), r.[Name], CONVERT(date, SYSUTCDATETIME()), u.[IsActive]
                FROM [AspNetUsers] u JOIN [AspNetUserRoles] ur ON ur.[UserId] = u.[Id]
                JOIN [AspNetRoles] r ON r.[Id] = ur.[RoleId]
                WHERE r.[Name] IN (N'Librarian', N'TransportManager', N'HostelManager', N'Registrar', N'Accountant')
                AND (SELECT COUNT(*) FROM [AspNetUserRoles] otherRole WHERE otherRole.[UserId] = u.[Id]) = 1
                AND NOT EXISTS (SELECT 1 FROM [EmployeeProfiles] e WHERE e.[UserId] = u.[Id]);
                INSERT [StaffMembers] ([EmployeeProfileId], [PrimaryRole], [CreatedUtc], [UpdatedUtc], [CreatedByUserId], [IsDeleted])
                SELECT e.[Id], r.[Name], SYSUTCDATETIME(), SYSUTCDATETIME(), N'staff-migration', 0
                FROM [EmployeeProfiles] e JOIN [AspNetUserRoles] ur ON ur.[UserId] = e.[UserId]
                JOIN [AspNetRoles] r ON r.[Id] = ur.[RoleId]
                WHERE r.[Name] IN (N'Librarian', N'TransportManager', N'HostelManager', N'Registrar', N'Accountant')
                AND (SELECT COUNT(*) FROM [AspNetUserRoles] otherRole WHERE otherRole.[UserId] = e.[UserId]) = 1
                AND NOT EXISTS (SELECT 1 FROM [StaffMembers] s WHERE s.[EmployeeProfileId] = e.[Id]);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TransportVehicles_TransportDrivers_TransportDriverId",
                table: "TransportVehicles");

            migrationBuilder.DropForeignKey(
                name: "FK_TransportVehicles_TransportRoutes_TransportRouteId",
                table: "TransportVehicles");

            migrationBuilder.DropTable(
                name: "StaffExpenses");

            migrationBuilder.DropTable(
                name: "StaffMembers");

            migrationBuilder.DropTable(
                name: "StaffMessages");

            migrationBuilder.DropTable(
                name: "StaffWorkItems");

            migrationBuilder.DropTable(
                name: "TransportDrivers");

            migrationBuilder.DropIndex(
                name: "IX_TransportVehicles_TransportDriverId",
                table: "TransportVehicles");

            migrationBuilder.DropIndex(
                name: "IX_TransportVehicles_TransportRouteId",
                table: "TransportVehicles");

            migrationBuilder.DropColumn(
                name: "FitnessExpiry",
                table: "TransportVehicles");

            migrationBuilder.DropColumn(
                name: "InsuranceExpiry",
                table: "TransportVehicles");

            migrationBuilder.DropColumn(
                name: "MaintenanceStatus",
                table: "TransportVehicles");

            migrationBuilder.DropColumn(
                name: "PermitExpiry",
                table: "TransportVehicles");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "TransportVehicles");

            migrationBuilder.DropColumn(
                name: "TransportDriverId",
                table: "TransportVehicles");

            migrationBuilder.DropColumn(
                name: "TransportRouteId",
                table: "TransportVehicles");

            migrationBuilder.DropColumn(
                name: "VehicleNumber",
                table: "TransportVehicles");

            migrationBuilder.DropColumn(
                name: "VehicleType",
                table: "TransportVehicles");
        }
    }
}
