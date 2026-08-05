namespace Ats.UnitTests.Auth;

using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using Ats.Shared.Auth;
using Microsoft.Extensions.Configuration;
using Xunit;

public class JwtTokenGeneratorTests
{
    [Fact]
    public void GenerateAccessToken_WithValidUserAndRoles_ReturnsSignedToken()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?>
        {
            {"Jwt:SigningKey", "SuperSecretKeyWithAtLeast32BytesLengthForHmacSha256!"},
            {"Jwt:Issuer", "TestIssuer"},
            {"Jwt:Audience", "TestAudience"},
            {"Jwt:AccessTokenExpirationMinutes", "15"}
        };
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var generator = new JwtTokenGenerator(config);
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            FirstName = "Jane",
            LastName = "Doe"
        };
        var roles = new[] { AuthConstants.Roles.Candidate };

        // Act
        var tokenString = generator.GenerateAccessToken(user, roles);

        // Assert
        Assert.NotNull(tokenString);
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(tokenString);

        Assert.Equal("TestIssuer", jwtToken.Issuer);
        Assert.Contains("TestAudience", jwtToken.Audiences);
        Assert.Equal(user.Id.ToString(), jwtToken.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
        Assert.Equal("test@example.com", jwtToken.Claims.First(c => c.Type == JwtRegisteredClaimNames.Email).Value);
        Assert.Equal(AuthConstants.Roles.Candidate, jwtToken.Claims.First(c => c.Type == ClaimTypes.Role).Value);
    }

    [Fact]
    public void GenerateAccessToken_WithShortKey_ThrowsInvalidOperationException()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?>
        {
            {"Jwt:SigningKey", "short_key"}
        };
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var generator = new JwtTokenGenerator(config);
        var user = new ApplicationUser { Id = Guid.NewGuid(), Email = "test@example.com" };

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            generator.GenerateAccessToken(user, new[] { "Candidate" }));
    }
}
