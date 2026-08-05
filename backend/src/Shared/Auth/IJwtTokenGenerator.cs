namespace Ats.Shared.Auth;

using System.Collections.Generic;

public interface IJwtTokenGenerator
{
    string GenerateAccessToken(ApplicationUser user, IEnumerable<string> roles);
}
