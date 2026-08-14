namespace Ats.UnitTests.Screening;

using System;
using System.IO;
using System.Text;
using Ats.Service.Screening;
using Xunit;

public class PdfTextExtractorTests
{
    private readonly PdfTextExtractor _extractor = new();

    [Fact]
    public void ExtractText_WithNullStream_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _extractor.ExtractText(null!));
    }

    [Fact]
    public void ExtractText_WithInvalidPdfBytes_ReturnsEmptyString()
    {
        // Arrange
        var invalidBytes = Encoding.UTF8.GetBytes("This is not a valid PDF file");
        using var stream = new MemoryStream(invalidBytes);

        // Act
        var text = _extractor.ExtractText(stream);

        // Assert
        Assert.Equal(string.Empty, text);
    }
}
