namespace Ats.Shared.Auth;

using System;

public static class AuthConstants
{
    public static class Roles
    {
        public const string Candidate = "Candidate";
        public const string Recruiter = "Recruiter";
        public const string HiringManager = "HiringManager";

        public static readonly Guid CandidateRoleId = Guid.Parse("d6b4122d-6228-4e08-bf29-43c3d5e23a01");
        public static readonly Guid RecruiterRoleId = Guid.Parse("d6b4122d-6228-4e08-bf29-43c3d5e23a02");
        public static readonly Guid HiringManagerRoleId = Guid.Parse("d6b4122d-6228-4e08-bf29-43c3d5e23a03");
    }

    public static class Policies
    {
        public const string CandidateOnly = "CandidateOnly";
        public const string StaffOnly = "StaffOnly";
        public const string RecruiterOnly = "RecruiterOnly";
        public const string HiringManagerOnly = "HiringManagerOnly";
    }

    // Seeded credentials (all 3 emails + the shared password) are documented together in
    // docs/specs/0007-seed-sample-accounts/plan/erd.md §7 and spec.md FR-3/FR-4 — do not require
    // reading this file to find them (FR-8/AC-9).
    public static class SeedAccounts
    {
        public const string CandidateEmail = "sample.candidate@d4fape-ats.local";
        public const string RecruiterEmail = "sample.recruiter@d4fape-ats.local";
        public const string HiringManagerEmail = "sample.hiringmanager@d4fape-ats.local";

        // Shared by all three seeded accounts. Documented together with the emails above in
        // plan/erd.md §7 (FR-8/AC-9) — a developer does not need to open this file to find them.
        public const string SharedPassword = "Temp@123";

        // Pinned output of a real `PasswordHasher<ApplicationUser>().HashPassword(dummyUser,
        // SharedPassword)` call, computed once at implementation time (see plan/lld.md §10 step 0)
        // and hardcoded here. `HasData` has no DI/UserManager access at migration-apply time, so
        // this value cannot be computed live the way a real registration does — it is generated
        // once and pinned, the same way the seeded roles' `ConcurrencyStamp` values are pinned
        // strings.
        public const string SharedPasswordHash = "AQAAAAIAAYagAAAAEOtx0o7XBEX91ZfQpzpRzQRdjjNKgXHPuOpNiKax7WTqd/M0OzWUpco0hClbyO1bJQ==";

        public static readonly Guid CandidateUserId = Guid.Parse("d6b4122d-6228-4e08-bf29-43c3d5e23b01");
        public static readonly Guid RecruiterUserId = Guid.Parse("d6b4122d-6228-4e08-bf29-43c3d5e23b02");
        public static readonly Guid HiringManagerUserId = Guid.Parse("d6b4122d-6228-4e08-bf29-43c3d5e23b03");

        public const string CandidateSecurityStamp = "d6b4122d-6228-4e08-bf29-43c3d5e23c01";
        public const string RecruiterSecurityStamp = "d6b4122d-6228-4e08-bf29-43c3d5e23c02";
        public const string HiringManagerSecurityStamp = "d6b4122d-6228-4e08-bf29-43c3d5e23c03";

        // Fixed, not DateTime.UtcNow — HasData values must be literal and stable across every
        // fresh migration apply.
        public static readonly DateTime SeededAtUtc = new(2026, 8, 14, 0, 0, 0, DateTimeKind.Utc);
    }
}
