namespace Ats.UnitTests.Auth;

using System;
using Ats.Shared.Auth;
using Xunit;

public class RefreshTokenTests
{
    [Fact]
    public void RefreshToken_Create_WithValidArgs_SetsProperties()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tokenHash = "sample_sha256_hash_value";
        var lifetime = TimeSpan.FromDays(7);

        // Act
        var token = RefreshToken.Create(userId, tokenHash, lifetime);

        // Assert
        Assert.NotEqual(Guid.Empty, token.Id);
        Assert.Equal(userId, token.UserId);
        Assert.Equal(tokenHash, token.TokenHash);
        Assert.Null(token.RevokedAtUtc);
        Assert.Null(token.ReplacedByTokenId);
        Assert.True(token.IsActive);
        Assert.True(token.ExpiresAtUtc > DateTime.UtcNow);
    }

    [Fact]
    public void RefreshToken_Create_WithEmptyUserId_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentException>(() =>
            RefreshToken.Create(Guid.Empty, "some_hash", TimeSpan.FromDays(7)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void RefreshToken_Create_WithInvalidTokenHash_ThrowsArgumentException(string? invalidHash)
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentException>(() =>
            RefreshToken.Create(Guid.NewGuid(), invalidHash!, TimeSpan.FromDays(7)));
    }

    [Fact]
    public void RefreshToken_Revoke_SetsRevokedAtUtcAndReplacedByTokenId()
    {
        // Arrange
        var token = RefreshToken.Create(Guid.NewGuid(), "hash_value", TimeSpan.FromDays(7));
        var replacedById = Guid.NewGuid();

        // Act
        token.Revoke(replacedById);

        // Assert
        Assert.NotNull(token.RevokedAtUtc);
        Assert.Equal(replacedById, token.ReplacedByTokenId);
        Assert.False(token.IsActive);
    }
}
