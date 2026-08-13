namespace Ats.IntegrationTests.Auth;

using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Ats.IntegrationTests;
using Ats.Service.Auth.Dtos;
using Ats.Shared.Auth;
using Xunit;

/// <summary>
/// AC-3, AC-4, AC-5, E-1 (spec 0007): integration tests verifying that each of the three seeded
/// accounts (Candidate, Recruiter, Hiring Manager) can log in successfully with their seeded credentials,
/// returning 200 OK with the correct role claim and valid refresh token, and that registering with a
/// seeded account's email returns 409 Conflict duplicate email.
/// </summary>
public class SeedAccountsLoginTests
{
    [Fact]
    public async Task Login_SeededCandidateCredentials_Returns200WithCandidateRoleClaim()
    {
        using var factory = new CustomWebApplicationFactory(applyMigrations: true);
        factory.InitializeDatabase();
        using var client = factory.CreateClient();

        var loginPayload = new LoginRequestDto(
            AuthConstants.SeedAccounts.CandidateEmail,
            AuthConstants.SeedAccounts.SharedPassword);

        var response = await client.PostAsJsonAsync("/api/auth/login", loginPayload);
        var content = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode, $"Login failed with status {response.StatusCode}: {content}");

        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
        Assert.NotNull(authResponse);
        Assert.Equal("Bearer", authResponse.TokenType);
        Assert.False(string.IsNullOrWhiteSpace(authResponse.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(authResponse.RefreshToken));
        Assert.Equal(AuthConstants.SeedAccounts.CandidateEmail, authResponse.User.Email);
        Assert.Contains(AuthConstants.Roles.Candidate, authResponse.User.Roles);
    }

    [Fact]
    public async Task Login_SeededRecruiterCredentials_Returns200WithRecruiterRoleClaim()
    {
        using var factory = new CustomWebApplicationFactory(applyMigrations: true);
        factory.InitializeDatabase();
        using var client = factory.CreateClient();

        var loginPayload = new LoginRequestDto(
            AuthConstants.SeedAccounts.RecruiterEmail,
            AuthConstants.SeedAccounts.SharedPassword);

        var response = await client.PostAsJsonAsync("/api/auth/login", loginPayload);
        var content = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode, $"Login failed with status {response.StatusCode}: {content}");

        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
        Assert.NotNull(authResponse);
        Assert.Equal("Bearer", authResponse.TokenType);
        Assert.False(string.IsNullOrWhiteSpace(authResponse.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(authResponse.RefreshToken));
        Assert.Equal(AuthConstants.SeedAccounts.RecruiterEmail, authResponse.User.Email);
        Assert.Contains(AuthConstants.Roles.Recruiter, authResponse.User.Roles);
    }

    [Fact]
    public async Task Login_SeededHiringManagerCredentials_Returns200WithHiringManagerRoleClaim()
    {
        using var factory = new CustomWebApplicationFactory(applyMigrations: true);
        factory.InitializeDatabase();
        using var client = factory.CreateClient();

        var loginPayload = new LoginRequestDto(
            AuthConstants.SeedAccounts.HiringManagerEmail,
            AuthConstants.SeedAccounts.SharedPassword);

        var response = await client.PostAsJsonAsync("/api/auth/login", loginPayload);
        var content = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode, $"Login failed with status {response.StatusCode}: {content}");

        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
        Assert.NotNull(authResponse);
        Assert.Equal("Bearer", authResponse.TokenType);
        Assert.False(string.IsNullOrWhiteSpace(authResponse.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(authResponse.RefreshToken));
        Assert.Equal(AuthConstants.SeedAccounts.HiringManagerEmail, authResponse.User.Email);
        Assert.Contains(AuthConstants.Roles.HiringManager, authResponse.User.Roles);
    }

    [Fact]
    public async Task Register_WithSeededCandidateEmail_Returns409DuplicateEmail()
    {
        using var factory = new CustomWebApplicationFactory(applyMigrations: true);
        factory.InitializeDatabase();
        using var client = factory.CreateClient();

        var regPayload = new RegisterRequestDto(
            AuthConstants.SeedAccounts.CandidateEmail,
            "Password123!",
            "New",
            "Candidate");

        var response = await client.PostAsJsonAsync("/api/auth/register", regPayload);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("auth.register.duplicate-email", content);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
