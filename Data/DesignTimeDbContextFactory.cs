using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Student_Management_System.Models;

namespace Student_Management_System.Data;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? "Development";
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
        var migrationConnection =
            Environment.GetEnvironmentVariable("EDUFLOW_MIGRATION_CONNECTION");
        var connectionString = string.IsNullOrWhiteSpace(migrationConnection)
            ? configuration.GetConnectionString("DefaultConnection")
            : migrationConnection;
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                "Set EDUFLOW_MIGRATION_CONNECTION or configure a non-empty ConnectionStrings:DefaultConnection before running Entity Framework commands.");
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure())
            .Options;
        return new ApplicationDbContext(options);
    }
}
