namespace Ats.UnitTests.Storage;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Ats.Shared.Storage;
using Microsoft.Extensions.Configuration;
using Xunit;

public class LocalDiskFileStorageTests : IDisposable
{
    private readonly string _basePath;
    private readonly LocalDiskFileStorage _storage;

    public LocalDiskFileStorageTests()
    {
        _basePath = Path.Combine(Path.GetTempPath(), "ats-cv-tests-" + Guid.NewGuid().ToString("N"));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:CvBasePath"] = _basePath
            })
            .Build();

        _storage = new LocalDiskFileStorage(configuration);
    }

    public void Dispose()
    {
        if (Directory.Exists(_basePath))
        {
            Directory.Delete(_basePath, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task SaveAsync_ThenOpenReadAsync_RoundTripsBytes()
    {
        // Arrange
        var storageKey = $"{Guid.NewGuid():N}.pdf";
        var originalBytes = Encoding.UTF8.GetBytes("%PDF-1.4 fake content");
        using var contentStream = new MemoryStream(originalBytes);

        // Act
        await _storage.SaveAsync(storageKey, contentStream);
        await using var readStream = await _storage.OpenReadAsync(storageKey);
        using var resultStream = new MemoryStream();
        await readStream.CopyToAsync(resultStream);

        // Assert
        Assert.Equal(originalBytes, resultStream.ToArray());
    }

    [Fact]
    public async Task DeleteAsync_RemovesFile()
    {
        // Arrange
        var storageKey = $"{Guid.NewGuid():N}.pdf";
        using var contentStream = new MemoryStream(Encoding.UTF8.GetBytes("%PDF-1.4"));
        await _storage.SaveAsync(storageKey, contentStream);

        // Act
        await _storage.DeleteAsync(storageKey);

        // Assert
        await Assert.ThrowsAsync<FileNotFoundException>(() => _storage.OpenReadAsync(storageKey));
    }

    [Fact]
    public async Task DeleteAsync_WhenFileDoesNotExist_DoesNotThrow()
    {
        // Arrange
        var storageKey = $"{Guid.NewGuid():N}.pdf";

        // Act & Assert
        await _storage.DeleteAsync(storageKey);
    }

    [Theory]
    [InlineData("../secrets.txt")]
    [InlineData("..\\secrets.txt")]
    [InlineData("subdir/file.pdf")]
    [InlineData("subdir\\file.pdf")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ResolvePath_RejectsPathTraversalKeys(string maliciousKey)
    {
        // Arrange
        using var contentStream = new MemoryStream(Encoding.UTF8.GetBytes("%PDF-1.4"));

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _storage.SaveAsync(maliciousKey, contentStream));
    }

    [Fact]
    public void Constructor_WithNullConfiguration_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() => new LocalDiskFileStorage(null!));
    }
}
