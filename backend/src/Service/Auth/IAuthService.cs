namespace Ats.Service.Auth;

using System;
using System.Threading;
using System.Threading.Tasks;
using Ats.Service.Auth.Dtos;
using Ats.Service.Common;

public interface IAuthService
{
    Task<Result<UserDto>> RegisterCandidateAsync(RegisterRequestDto dto, CancellationToken ct = default);
    Task<Result<AuthResponseDto>> AuthenticateAsync(LoginRequestDto dto, CancellationToken ct = default);
    Task<Result<AuthResponseDto>> RefreshTokenAsync(RefreshTokenRequestDto dto, CancellationToken ct = default);
    Task<Result> RevokeTokenAsync(string refreshToken, CancellationToken ct = default);
    Task<Result<UserDto>> GetCurrentUserAsync(Guid userId, CancellationToken ct = default);
}
