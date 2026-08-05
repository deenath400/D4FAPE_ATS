namespace Ats.Db;

using System;
using Ats.Db.Configurations;
using Ats.Db.Requisitions;
using Ats.Shared.Auth;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

public class AppDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Requisition> Requisitions => Set<Requisition>();
    public DbSet<Stage> Stages => Set<Stage>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfiguration(new RefreshTokenConfiguration());
        builder.ApplyConfiguration(new RequisitionConfiguration());
        builder.ApplyConfiguration(new StageConfiguration());

        SeedRoles(builder);
    }

    private static void SeedRoles(ModelBuilder builder)
    {
        builder.Entity<ApplicationRole>().HasData(
            new ApplicationRole
            {
                Id = AuthConstants.Roles.CandidateRoleId,
                Name = AuthConstants.Roles.Candidate,
                NormalizedName = AuthConstants.Roles.Candidate.ToUpperInvariant(),
                ConcurrencyStamp = "d6b4122d-6228-4e08-bf29-43c3d5e23a01"
            },
            new ApplicationRole
            {
                Id = AuthConstants.Roles.RecruiterRoleId,
                Name = AuthConstants.Roles.Recruiter,
                NormalizedName = AuthConstants.Roles.Recruiter.ToUpperInvariant(),
                ConcurrencyStamp = "d6b4122d-6228-4e08-bf29-43c3d5e23a02"
            },
            new ApplicationRole
            {
                Id = AuthConstants.Roles.HiringManagerRoleId,
                Name = AuthConstants.Roles.HiringManager,
                NormalizedName = AuthConstants.Roles.HiringManager.ToUpperInvariant(),
                ConcurrencyStamp = "d6b4122d-6228-4e08-bf29-43c3d5e23a03"
            }
        );
    }
}
