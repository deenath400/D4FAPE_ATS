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
}
