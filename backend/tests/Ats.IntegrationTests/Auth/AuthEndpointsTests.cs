namespace Ats.IntegrationTests.Auth;

using System;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Ats.IntegrationTests;
using Ats.Service.Auth.Dtos;
using Xunit;

public class AuthEndpointsTests
{
    [Fact]
    public async Task Register_WithValidCandidate_Returns201CreatedAndUserDto()
    {
        using var factory = new CustomWebApplicationFactory(applyMigrations: true);
        factory.InitializeDatabase();
        using var client = factory.CreateClient();

        var regPayload = new RegisterRequestDto("candidate1@example.com", "Password123!", "Ada", "Lovelace");
        var response = await client.PostAsJsonAsync("/api/auth/register", regPayload);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var userDto = await response.Content.ReadFromJsonAsync<UserDto>();
        Assert.NotNull(userDto);
        Assert.Equal("candidate1@example.com", userDto.Email);
        Assert.Contains("Candidate", userDto.Roles);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_Returns409ConflictProblemDetails()
    {
        using var factory = new CustomWebApplicationFactory(applyMigrations: true);
        factory.InitializeDatabase();
        using var client = factory.CreateClient();

        var regPayload = new RegisterRequestDto("dup@example.com", "Password123!", "Ada", "Lovelace");
        await client.PostAsJsonAsync("/api/auth/register", regPayload);

        var duplicateResponse = await client.PostAsJsonAsync("/api/auth/register", regPayload);

        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);
        var content = await duplicateResponse.Content.ReadAsStringAsync();
        Assert.Contains("auth.register.duplicate-email", content);
        Assert.Equal("application/problem+json", duplicateResponse.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Login_WithValidCredentials_Returns200AndTokenPair()
    {
        using var factory = new CustomWebApplicationFactory(applyMigrations: true);
        factory.InitializeDatabase();
        using var client = factory.CreateClient();

        var regPayload = new RegisterRequestDto("loginuser@example.com", "Password123!", "Ada", "Lovelace");
        await client.PostAsJsonAsync("/api/auth/register", regPayload);

        var loginPayload = new LoginRequestDto("loginuser@example.com", "Password123!");
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", loginPayload);
        var loginContent = await loginResponse.Content.ReadAsStringAsync();
        Assert.True(loginResponse.IsSuccessStatusCode, $"Login failed with status {loginResponse.StatusCode}: {loginContent}");

        var authResponse = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>();
        Assert.NotNull(authResponse);
        Assert.False(string.IsNullOrWhiteSpace(authResponse.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(authResponse.RefreshToken));
        Assert.Equal("Bearer", authResponse.TokenType);
        Assert.Equal("loginuser@example.com", authResponse.User.Email);
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_Returns401UnauthorizedProblemDetails()
    {
        using var factory = new CustomWebApplicationFactory(applyMigrations: true);
        factory.InitializeDatabase();
        using var client = factory.CreateClient();

        var loginPayload = new LoginRequestDto("wrong@example.com", "WrongPassword123!");
        var response = await client.PostAsJsonAsync("/api/auth/login", loginPayload);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("auth.login.invalid-credentials", content);
    }

    [Fact]
    public async Task Refresh_WithValidRefreshToken_Returns200AndRotatedTokens()
    {
        using var factory = new CustomWebApplicationFactory(applyMigrations: true);
        factory.InitializeDatabase();
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequestDto("refreshuser@example.com", "Password123!", "Ada", "Lovelace"));
        var loginResp = await client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto("refreshuser@example.com", "Password123!"));
        var authDto = await loginResp.Content.ReadFromJsonAsync<AuthResponseDto>();

        var refreshResp = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequestDto(authDto!.RefreshToken));

        Assert.Equal(HttpStatusCode.OK, refreshResp.StatusCode);
        var newAuthDto = await refreshResp.Content.ReadFromJsonAsync<AuthResponseDto>();
        Assert.NotNull(newAuthDto);
        Assert.NotEqual(authDto.RefreshToken, newAuthDto.RefreshToken);
    }

    [Fact]
    public async Task Refresh_WithRevokedToken_Returns401AndRevokesFamily()
    {
        using var factory = new CustomWebApplicationFactory(applyMigrations: true);
        factory.InitializeDatabase();
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequestDto("replayuser@example.com", "Password123!", "Ada", "Lovelace"));
        var loginResp = await client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto("replayuser@example.com", "Password123!"));
        var authDto = await loginResp.Content.ReadFromJsonAsync<AuthResponseDto>();

        // First refresh (invalidates initial token)
        await client.PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequestDto(authDto!.RefreshToken));

        // Replay attempt with initial token
        var replayResp = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequestDto(authDto.RefreshToken));

        Assert.Equal(HttpStatusCode.Unauthorized, replayResp.StatusCode);
        var content = await replayResp.Content.ReadAsStringAsync();
        Assert.Contains("auth.refresh.token-revoked", content);
    }

    [Fact]
    public async Task GetMe_WithValidBearerToken_Returns200AndUserSummary()
    {
        using var factory = new CustomWebApplicationFactory(applyMigrations: true);
        factory.InitializeDatabase();
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequestDto("meuser@example.com", "Password123!", "Ada", "Lovelace"));
        var loginResp = await client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto("meuser@example.com", "Password123!"));
        var authDto = await loginResp.Content.ReadFromJsonAsync<AuthResponseDto>();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authDto!.AccessToken);
        var meResponse = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        var userDto = await meResponse.Content.ReadFromJsonAsync<UserDto>();
        Assert.NotNull(userDto);
        Assert.Equal("meuser@example.com", userDto.Email);
    }

    [Fact]
    public async Task GetMe_WithoutBearerToken_Returns401Unauthorized()
    {
        using var factory = new CustomWebApplicationFactory(applyMigrations: true);
        factory.InitializeDatabase();
        using var client = factory.CreateClient();

        var meResponse = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, meResponse.StatusCode);
    }

    [Fact]
    public async Task StaffEndpoint_WithCandidateToken_Returns403Forbidden()
    {
        using var factory = new CustomWebApplicationFactory(applyMigrations: true);
        factory.InitializeDatabase();
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequestDto("candidateonly@example.com", "Password123!", "Ada", "Lovelace"));
        var loginResp = await client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto("candidateonly@example.com", "Password123!"));
        var authDto = await loginResp.Content.ReadFromJsonAsync<AuthResponseDto>();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authDto!.AccessToken);
        var staffResponse = await client.GetAsync("/api/auth/staff-test");

        Assert.Equal(HttpStatusCode.Forbidden, staffResponse.StatusCode);
    }
}
