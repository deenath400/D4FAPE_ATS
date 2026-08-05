namespace Ats.IntegrationTests;

using System;
using System.Collections.Generic;
using System.IO;
using Ats.Db;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbFilePath;
    private readonly bool _applyMigrations;

    public CustomWebApplicationFactory(bool applyMigrations = true)
    {
        _dbFilePath = Path.Combine(Path.GetTempPath(), $"ats_test_{Guid.NewGuid():N}.db");
        _applyMigrations = applyMigrations;
    }

    public string DbFilePath => _dbFilePath;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = $"Data Source={_dbFilePath}",
                ["Jwt:SigningKey"] = "DevelopmentSuperSecretKeyWithAtLeast32BytesLengthForHmacSha256!",
                ["Jwt:Issuer"] = "D4FAPE-ATS",
                ["Jwt:Audience"] = "D4FAPE-ATS-App"
            });
        });
    }

    public void InitializeDatabase()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (_applyMigrations)
        {
            db.Database.Migrate();
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            try
            {
                if (File.Exists(_dbFilePath)) File.Delete(_dbFilePath);
                var wal = _dbFilePath + "-wal";
                if (File.Exists(wal)) File.Delete(wal);
                var shm = _dbFilePath + "-shm";
                if (File.Exists(shm)) File.Delete(shm);
            }
            catch
            {
                // Best effort cleanup
            }
        }
    }
}
