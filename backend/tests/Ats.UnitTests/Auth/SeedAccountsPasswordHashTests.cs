namespace Ats.UnitTests.Auth;

using Ats.Shared.Auth;
using Microsoft.AspNetCore.Identity;
using Xunit;

/// <summary>
/// AC-2 (spec 0007): unit test verifying that AuthConstants.SeedAccounts.SharedPasswordHash is
/// not the plaintext string and successfully verifies against the shared password using ASP.NET Core
/// Identity's PasswordHasher algorithm.
/// </summary>
public class SeedAccountsPasswordHashTests
{
    [Fact]
    public void SeededPasswordHash_IsNotPlaintextAndVerifiesAgainstSharedPassword()
    {
        var passwordHash = AuthConstants.SeedAccounts.SharedPasswordHash;
        var rawPassword = AuthConstants.SeedAccounts.SharedPassword;

        Assert.False(string.IsNullOrWhiteSpace(passwordHash));
        Assert.NotEqual(rawPassword, passwordHash);

        var hasher = new PasswordHasher<ApplicationUser>();
        var dummyUser = new ApplicationUser();

        var result = hasher.VerifyHashedPassword(dummyUser, passwordHash, rawPassword);

        Assert.True(
            result == PasswordVerificationResult.Success || result == PasswordVerificationResult.SuccessRehashNeeded,
            $"Expected password verification to succeed, but got {result}");
    }
}
