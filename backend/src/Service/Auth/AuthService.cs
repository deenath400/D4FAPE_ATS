namespace Ats.Service.Auth;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ats.Db;
using Ats.Service.Auth.Dtos;
using Ats.Service.Common;
using Ats.Shared.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AppDbContext _dbContext;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IConfiguration _configuration;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        AppDbContext dbContext,
        IJwtTokenGenerator jwtTokenGenerator,
        IConfiguration configuration)
    {
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _jwtTokenGenerator = jwtTokenGenerator ?? throw new ArgumentNullException(nameof(jwtTokenGenerator));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public async Task<Result<UserDto>> RegisterCandidateAsync(RegisterRequestDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var existingUser = await _userManager.FindByEmailAsync(dto.Email);
        if (existingUser != null)
        {
            return Result<UserDto>.Conflict("auth.register.duplicate-email", "An account with this email address already exists.");
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = dto.Email,
            UserName = dto.Email,
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            CreatedAtUtc = DateTime.UtcNow
        };

        var createResult = await _userManager.CreateAsync(user, dto.Password);
        if (!createResult.Succeeded)
        {
            var errors = createResult.Errors
                .GroupBy(e => e.Code)
                .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());

            return Result<UserDto>.Validation(errors, "User registration failed validation.");
        }

        await _userManager.AddToRoleAsync(user, AuthConstants.Roles.Candidate);

        var roles = await _userManager.GetRolesAsync(user);
        var userDto = new UserDto(user.Id, user.Email!, user.FirstName, user.LastName, roles.ToList());

        return Result<UserDto>.Ok(userDto);
    }

    public async Task<Result<AuthResponseDto>> AuthenticateAsync(LoginRequestDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var user = await _userManager.FindByEmailAsync(dto.Email);
        if (user == null)
        {
            return Result<AuthResponseDto>.Unauthorized("auth.login.invalid-credentials", "Invalid email or password.");
        }

        if (await _userManager.IsLockedOutAsync(user))
        {
            return Result<AuthResponseDto>.Unauthorized("auth.login.account-locked", "Account locked due to multiple failed login attempts.");
        }

        var passwordValid = await _userManager.CheckPasswordAsync(user, dto.Password);
        if (!passwordValid)
        {
            await _userManager.AccessFailedAsync(user);
            return Result<AuthResponseDto>.Unauthorized("auth.login.invalid-credentials", "Invalid email or password.");
        }

        await _userManager.ResetAccessFailedCountAsync(user);

        var roles = await _userManager.GetRolesAsync(user);
        var accessToken = _jwtTokenGenerator.GenerateAccessToken(user, roles);

        var (rawRefreshToken, refreshTokenEntity) = IssueRefreshToken(user.Id);
        _dbContext.RefreshTokens.Add(refreshTokenEntity);
        await _dbContext.SaveChangesAsync(ct);

        var userDto = new UserDto(user.Id, user.Email!, user.FirstName, user.LastName, roles.ToList());
        var expMinutes = int.TryParse(_configuration["Jwt:AccessTokenExpirationMinutes"], out var exp) ? exp : 15;

        var authResponse = new AuthResponseDto(
            accessToken,
            rawRefreshToken,
            "Bearer",
            expMinutes * 60,
            userDto);

        return Result<AuthResponseDto>.Ok(authResponse);
    }

    public async Task<Result<AuthResponseDto>> RefreshTokenAsync(RefreshTokenRequestDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (string.IsNullOrWhiteSpace(dto.RefreshToken))
        {
            return Result<AuthResponseDto>.Unauthorized("auth.refresh.invalid-token", "Refresh token is required.");
        }

        var tokenHash = HashToken(dto.RefreshToken);
        var tokenEntity = await _dbContext.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash, ct);

        if (tokenEntity == null || tokenEntity.ExpiresAtUtc <= DateTime.UtcNow)
        {
            return Result<AuthResponseDto>.Unauthorized("auth.refresh.invalid-token", "Invalid or expired refresh token.");
        }

        if (tokenEntity.RevokedAtUtc != null)
        {
            // Replay detection: revoke all active tokens belonging to this user family
            var userActiveTokens = await _dbContext.RefreshTokens
                .Where(rt => rt.UserId == tokenEntity.UserId && rt.RevokedAtUtc == null)
                .ToListAsync(ct);

            foreach (var token in userActiveTokens)
            {
                token.Revoke();
            }

            await _dbContext.SaveChangesAsync(ct);
            return Result<AuthResponseDto>.Unauthorized("auth.refresh.token-revoked", "Refresh token has been revoked.");
        }

        var (rawNewRefreshToken, newRefreshTokenEntity) = IssueRefreshToken(tokenEntity.UserId);
        tokenEntity.Revoke(newRefreshTokenEntity.Id);
        _dbContext.RefreshTokens.Add(newRefreshTokenEntity);

        await _dbContext.SaveChangesAsync(ct);

        var roles = await _userManager.GetRolesAsync(tokenEntity.User);
        var newAccessToken = _jwtTokenGenerator.GenerateAccessToken(tokenEntity.User, roles);
        var userDto = new UserDto(tokenEntity.User.Id, tokenEntity.User.Email!, tokenEntity.User.FirstName, tokenEntity.User.LastName, roles.ToList());
        var expMinutes = int.TryParse(_configuration["Jwt:AccessTokenExpirationMinutes"], out var exp) ? exp : 15;

        var authResponse = new AuthResponseDto(
            newAccessToken,
            rawNewRefreshToken,
            "Bearer",
            expMinutes * 60,
            userDto);

        return Result<AuthResponseDto>.Ok(authResponse);
    }

    public async Task<Result> RevokeTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return Result.Ok();
        }

        var tokenHash = HashToken(refreshToken);
        var tokenEntity = await _dbContext.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash, ct);

        if (tokenEntity != null && tokenEntity.RevokedAtUtc == null)
        {
            tokenEntity.Revoke();
            await _dbContext.SaveChangesAsync(ct);
        }

        return Result.Ok();
    }

    public async Task<Result<UserDto>> GetCurrentUserAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return Result<UserDto>.NotFound("auth.user.not-found", "User not found.");
        }

        var roles = await _userManager.GetRolesAsync(user);
        var userDto = new UserDto(user.Id, user.Email!, user.FirstName, user.LastName, roles.ToList());

        return Result<UserDto>.Ok(userDto);
    }

    private (string RawToken, RefreshToken Entity) IssueRefreshToken(Guid userId)
    {
        var randomBytes = RandomNumberGenerator.GetBytes(32);
        var rawToken = Convert.ToBase64String(randomBytes);
        var tokenHash = HashToken(rawToken);
        var expDays = int.TryParse(_configuration["Jwt:RefreshTokenExpirationDays"], out var days) ? days : 7;
        var tokenEntity = RefreshToken.Create(userId, tokenHash, TimeSpan.FromDays(expDays));

        return (rawToken, tokenEntity);
    }

    public static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }
}
