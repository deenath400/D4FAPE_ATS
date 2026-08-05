namespace Ats.UnitTests.Auth;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ats.Db;
using Ats.Service.Auth;
using Ats.Service.Auth.Dtos;
using Ats.Service.Common;
using Ats.Shared.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

public class AuthServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly Mock<SignInManager<ApplicationUser>> _signInManagerMock;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IConfiguration _configuration;
    private readonly AuthService _authService;

    public AuthServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new AppDbContext(options);
        _dbContext.Database.EnsureCreated();

        var userStore = new Microsoft.AspNetCore.Identity.EntityFrameworkCore.UserStore<ApplicationUser, ApplicationRole, AppDbContext, Guid>(_dbContext);
        var optionsAccessor = Options.Create(new IdentityOptions());
        var passwordHasher = new PasswordHasher<ApplicationUser>();
        var userValidators = new List<IUserValidator<ApplicationUser>> { new UserValidator<ApplicationUser>() };
        var passwordValidators = new List<IPasswordValidator<ApplicationUser>> { new PasswordValidator<ApplicationUser>() };
        var keyNormalizer = new UpperInvariantLookupNormalizer();
        var errors = new IdentityErrorDescriber();
        var userLogger = new Mock<ILogger<UserManager<ApplicationUser>>>().Object;

        _userManager = new UserManager<ApplicationUser>(
            userStore,
            optionsAccessor,
            passwordHasher,
            userValidators,
            passwordValidators,
            keyNormalizer,
            errors,
            services: Mock.Of<IServiceProvider>(),
            logger: userLogger);

        _signInManagerMock = new Mock<SignInManager<ApplicationUser>>(
            _userManager,
            Mock.Of<IHttpContextAccessor>(),
            Mock.Of<IUserClaimsPrincipalFactory<ApplicationUser>>(),
            optionsAccessor,
            Mock.Of<ILogger<SignInManager<ApplicationUser>>>(),
            Mock.Of<IAuthenticationSchemeProvider>(),
            Mock.Of<IUserConfirmation<ApplicationUser>>());

        _signInManagerMock.Setup(s => s.CheckPasswordSignInAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync((ApplicationUser user, string password, bool lockoutOnFailure) =>
            {
                var hasher = new PasswordHasher<ApplicationUser>();
                var verificationResult = hasher.VerifyHashedPassword(user, user.PasswordHash!, password);
                return verificationResult == PasswordVerificationResult.Success
                    ? SignInResult.Success
                    : SignInResult.Failed;
            });

        var inMemorySettings = new Dictionary<string, string?>
        {
            {"Jwt:SigningKey", "SuperSecretKeyWithAtLeast32BytesLengthForHmacSha256!"},
            {"Jwt:Issuer", "TestIssuer"},
            {"Jwt:Audience", "TestAudience"},
            {"Jwt:AccessTokenExpirationMinutes", "15"},
            {"Jwt:RefreshTokenExpirationDays", "7"}
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        _jwtTokenGenerator = new JwtTokenGenerator(_configuration);

        _authService = new AuthService(
            _userManager,
            _dbContext,
            _jwtTokenGenerator,
            _configuration);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task RegisterCandidate_WithValidPayload_CreatesUserAndAssignsCandidateRole()
    {
        // Arrange
        var dto = new RegisterRequestDto("newcandidate@example.com", "SecurePassword123!", "John", "Doe");

        // Act
        var result = await _authService.RegisterCandidateAsync(dto);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("newcandidate@example.com", result.Value.Email);
        Assert.Contains(AuthConstants.Roles.Candidate, result.Value.Roles);
    }

    [Fact]
    public async Task RegisterCandidate_WithDuplicateEmail_ReturnsConflict()
    {
        // Arrange
        var dto = new RegisterRequestDto("duplicate@example.com", "SecurePassword123!", "John", "Doe");
        await _authService.RegisterCandidateAsync(dto);

        // Act
        var duplicateResult = await _authService.RegisterCandidateAsync(dto);

        // Assert
        Assert.False(duplicateResult.IsSuccess);
        Assert.Equal(ResultStatus.Conflict, duplicateResult.Status);
        Assert.Equal("auth.register.duplicate-email", duplicateResult.ErrorCode);
    }

    [Fact]
    public async Task Authenticate_WithValidCredentials_ReturnsTokens()
    {
        // Arrange
        var regDto = new RegisterRequestDto("login@example.com", "SecurePassword123!", "Jane", "Doe");
        await _authService.RegisterCandidateAsync(regDto);

        var loginDto = new LoginRequestDto("login@example.com", "SecurePassword123!");

        // Act
        var result = await _authService.AuthenticateAsync(loginDto);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.NotNull(result.Value.AccessToken);
        Assert.NotNull(result.Value.RefreshToken);
        Assert.Equal("Bearer", result.Value.TokenType);
    }

    [Fact]
    public async Task Authenticate_WithInvalidCredentials_ReturnsUnauthorized()
    {
        // Arrange
        var loginDto = new LoginRequestDto("nonexistent@example.com", "WrongPassword123!");

        // Act
        var result = await _authService.AuthenticateAsync(loginDto);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ResultStatus.Unauthorized, result.Status);
        Assert.Equal("auth.login.invalid-credentials", result.ErrorCode);
    }

    [Fact]
    public async Task RefreshToken_WithValidActiveToken_RotatesTokenPair()
    {
        // Arrange
        var regDto = new RegisterRequestDto("refresh@example.com", "SecurePassword123!", "Jane", "Doe");
        await _authService.RegisterCandidateAsync(regDto);
        var loginResult = await _authService.AuthenticateAsync(new LoginRequestDto("refresh@example.com", "SecurePassword123!"));
        var initialRefreshToken = loginResult.Value!.RefreshToken;

        // Act
        var refreshResult = await _authService.RefreshTokenAsync(new RefreshTokenRequestDto(initialRefreshToken));

        // Assert
        Assert.True(refreshResult.IsSuccess);
        Assert.NotNull(refreshResult.Value);
        Assert.NotEqual(initialRefreshToken, refreshResult.Value.RefreshToken);
    }

    [Fact]
    public async Task RefreshToken_WithRevokedToken_TriggersReplayRevocation()
    {
        // Arrange
        var regDto = new RegisterRequestDto("replay@example.com", "SecurePassword123!", "Jane", "Doe");
        await _authService.RegisterCandidateAsync(regDto);
        var loginResult = await _authService.AuthenticateAsync(new LoginRequestDto("replay@example.com", "SecurePassword123!"));
        var initialRefreshToken = loginResult.Value!.RefreshToken;

        // Rotate once (initialRefreshToken is now revoked)
        await _authService.RefreshTokenAsync(new RefreshTokenRequestDto(initialRefreshToken));

        // Act: Attempt to reuse initialRefreshToken
        var replayResult = await _authService.RefreshTokenAsync(new RefreshTokenRequestDto(initialRefreshToken));

        // Assert
        Assert.False(replayResult.IsSuccess);
        Assert.Equal(ResultStatus.Unauthorized, replayResult.Status);
        Assert.Equal("auth.refresh.token-revoked", replayResult.ErrorCode);
    }

    [Fact]
    public async Task RevokeToken_WithActiveToken_RevokesToken()
    {
        // Arrange
        var regDto = new RegisterRequestDto("revoke@example.com", "SecurePassword123!", "Jane", "Doe");
        await _authService.RegisterCandidateAsync(regDto);
        var loginResult = await _authService.AuthenticateAsync(new LoginRequestDto("revoke@example.com", "SecurePassword123!"));

        // Act
        var revokeResult = await _authService.RevokeTokenAsync(loginResult.Value!.RefreshToken);

        // Assert
        Assert.True(revokeResult.IsSuccess);
    }
}
