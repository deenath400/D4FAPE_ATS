namespace Ats.Db;

using System;
using Ats.Db.Applications;
using Ats.Db.Configurations;
using Ats.Db.Pipeline;
using Ats.Db.Requisitions;
using Ats.Shared.Auth;
using Microsoft.AspNetCore.Identity;
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
    public DbSet<Application> Applications => Set<Application>();
    public DbSet<CvAttachment> CvAttachments => Set<CvAttachment>();
    public DbSet<StageTransition> StageTransitions => Set<StageTransition>();
    public DbSet<ScreeningReport> ScreeningReports => Set<ScreeningReport>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfiguration(new RefreshTokenConfiguration());
        builder.ApplyConfiguration(new RequisitionConfiguration());
        builder.ApplyConfiguration(new StageConfiguration());
        builder.ApplyConfiguration(new ApplicationConfiguration());
        builder.ApplyConfiguration(new CvAttachmentConfiguration());
        builder.ApplyConfiguration(new StageTransitionConfiguration());
        builder.ApplyConfiguration(new ScreeningReportConfiguration());

        SeedRoles(builder);
        SeedUsers(builder);
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

    // Seeded credentials (all 3 emails + the shared password) are documented together in
    // docs/specs/0007-seed-sample-accounts/plan/erd.md §7 and spec.md FR-3/FR-4 — do not require
    // reading this file to find them (FR-8/AC-9).
    private static void SeedUsers(ModelBuilder builder)
    {
        builder.Entity<ApplicationUser>().HasData(
            new ApplicationUser
            {
                Id = AuthConstants.SeedAccounts.CandidateUserId,
                UserName = AuthConstants.SeedAccounts.CandidateEmail,
                NormalizedUserName = AuthConstants.SeedAccounts.CandidateEmail.ToUpperInvariant(),
                Email = AuthConstants.SeedAccounts.CandidateEmail,
                NormalizedEmail = AuthConstants.SeedAccounts.CandidateEmail.ToUpperInvariant(),
                EmailConfirmed = false,
                PasswordHash = AuthConstants.SeedAccounts.SharedPasswordHash,
                SecurityStamp = AuthConstants.SeedAccounts.CandidateSecurityStamp,
                ConcurrencyStamp = AuthConstants.SeedAccounts.CandidateUserId.ToString(),
                PhoneNumberConfirmed = false,
                TwoFactorEnabled = false,
                LockoutEnabled = true,
                AccessFailedCount = 0,
                FirstName = "Sample",
                LastName = "Candidate",
                CreatedAtUtc = AuthConstants.SeedAccounts.SeededAtUtc
            },
            new ApplicationUser
            {
                Id = AuthConstants.SeedAccounts.RecruiterUserId,
                UserName = AuthConstants.SeedAccounts.RecruiterEmail,
                NormalizedUserName = AuthConstants.SeedAccounts.RecruiterEmail.ToUpperInvariant(),
                Email = AuthConstants.SeedAccounts.RecruiterEmail,
                NormalizedEmail = AuthConstants.SeedAccounts.RecruiterEmail.ToUpperInvariant(),
                EmailConfirmed = false,
                PasswordHash = AuthConstants.SeedAccounts.SharedPasswordHash,
                SecurityStamp = AuthConstants.SeedAccounts.RecruiterSecurityStamp,
                ConcurrencyStamp = AuthConstants.SeedAccounts.RecruiterUserId.ToString(),
                PhoneNumberConfirmed = false,
                TwoFactorEnabled = false,
                LockoutEnabled = true,
                AccessFailedCount = 0,
                FirstName = "Sample",
                LastName = "Recruiter",
                CreatedAtUtc = AuthConstants.SeedAccounts.SeededAtUtc
            },
            new ApplicationUser
            {
                Id = AuthConstants.SeedAccounts.HiringManagerUserId,
                UserName = AuthConstants.SeedAccounts.HiringManagerEmail,
                NormalizedUserName = AuthConstants.SeedAccounts.HiringManagerEmail.ToUpperInvariant(),
                Email = AuthConstants.SeedAccounts.HiringManagerEmail,
                NormalizedEmail = AuthConstants.SeedAccounts.HiringManagerEmail.ToUpperInvariant(),
                EmailConfirmed = false,
                PasswordHash = AuthConstants.SeedAccounts.SharedPasswordHash,
                SecurityStamp = AuthConstants.SeedAccounts.HiringManagerSecurityStamp,
                ConcurrencyStamp = AuthConstants.SeedAccounts.HiringManagerUserId.ToString(),
                PhoneNumberConfirmed = false,
                TwoFactorEnabled = false,
                LockoutEnabled = true,
                AccessFailedCount = 0,
                FirstName = "Sample",
                LastName = "Hiring Manager",
                CreatedAtUtc = AuthConstants.SeedAccounts.SeededAtUtc
            });

        builder.Entity<IdentityUserRole<Guid>>().HasData(
            new IdentityUserRole<Guid> { UserId = AuthConstants.SeedAccounts.CandidateUserId, RoleId = AuthConstants.Roles.CandidateRoleId },
            new IdentityUserRole<Guid> { UserId = AuthConstants.SeedAccounts.RecruiterUserId, RoleId = AuthConstants.Roles.RecruiterRoleId },
            new IdentityUserRole<Guid> { UserId = AuthConstants.SeedAccounts.HiringManagerUserId, RoleId = AuthConstants.Roles.HiringManagerRoleId });
    }
}
