namespace Ats.UnitTests.Screening;

using System;
using Ats.Db.Applications;
using Xunit;

public class ScreeningReportTests
{
    [Fact]
    public void CreatePending_ValidApplicationId_SetsPendingStatusAndDefaults()
    {
        // Arrange
        var applicationId = Guid.NewGuid();

        // Act
        var report = ScreeningReport.CreatePending(applicationId);

        // Assert
        Assert.NotEqual(Guid.Empty, report.Id);
        Assert.Equal(applicationId, report.ApplicationId);
        Assert.Equal(0, report.Score);
        Assert.Equal(ScreeningRecommendation.Review, report.Recommendation);
        Assert.Equal(string.Empty, report.Summary);
        Assert.Equal("[]", report.Strengths);
        Assert.Equal("[]", report.Concerns);
        Assert.Equal(ScreeningStatus.Pending, report.Status);
        Assert.Null(report.FailureReason);
        Assert.True(report.EvaluatedAtUtc <= DateTime.UtcNow);
    }

    [Fact]
    public void CreatePending_EmptyApplicationId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => ScreeningReport.CreatePending(Guid.Empty));
    }

    [Fact]
    public void Complete_WhenPending_SetsCompletedStatusAndValues()
    {
        // Arrange
        var report = ScreeningReport.CreatePending(Guid.NewGuid());

        // Act
        report.Complete(
            85,
            ScreeningRecommendation.Advance,
            "Strong candidate match",
            "[\"C#\", \".NET\"]",
            "[\"No cloud cert\"]");

        // Assert
        Assert.Equal(85, report.Score);
        Assert.Equal(ScreeningRecommendation.Advance, report.Recommendation);
        Assert.Equal("Strong candidate match", report.Summary);
        Assert.Equal("[\"C#\", \".NET\"]", report.Strengths);
        Assert.Equal("[\"No cloud cert\"]", report.Concerns);
        Assert.Equal(ScreeningStatus.Completed, report.Status);
        Assert.Null(report.FailureReason);
    }

    [Theory]
    [InlineData(-10, 0)]
    [InlineData(0, 0)]
    [InlineData(75, 75)]
    [InlineData(100, 100)]
    [InlineData(150, 100)]
    public void Complete_ClampsScore_Between0And100(int inputScore, int expectedScore)
    {
        // Arrange
        var report = ScreeningReport.CreatePending(Guid.NewGuid());

        // Act
        report.Complete(inputScore, ScreeningRecommendation.Review, "Summary", "[]", "[]");

        // Assert
        Assert.Equal(expectedScore, report.Score);
    }

    [Fact]
    public void Complete_WhenAlreadyCompleted_ThrowsInvalidOperationException()
    {
        // Arrange
        var report = ScreeningReport.CreatePending(Guid.NewGuid());
        report.Complete(80, ScreeningRecommendation.Advance, "Summary", "[]", "[]");

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            report.Complete(90, ScreeningRecommendation.Advance, "Another", "[]", "[]"));
    }

    [Fact]
    public void Complete_WhenFailed_ThrowsInvalidOperationException()
    {
        // Arrange
        var report = ScreeningReport.CreatePending(Guid.NewGuid());
        report.Fail("Extraction failed");

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            report.Complete(80, ScreeningRecommendation.Advance, "Summary", "[]", "[]"));
    }

    [Fact]
    public void Fail_WhenPending_SetsFailedStatusAndReason()
    {
        // Arrange
        var report = ScreeningReport.CreatePending(Guid.NewGuid());

        // Act
        report.Fail("Unreadable PDF");

        // Assert
        Assert.Equal(ScreeningStatus.Failed, report.Status);
        Assert.Equal("Unreadable PDF", report.FailureReason);
    }

    [Fact]
    public void Fail_WhenAlreadyCompleted_ThrowsInvalidOperationException()
    {
        // Arrange
        var report = ScreeningReport.CreatePending(Guid.NewGuid());
        report.Complete(80, ScreeningRecommendation.Advance, "Summary", "[]", "[]");

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => report.Fail("Late error"));
    }

    [Fact]
    public void Fail_WhenAlreadyFailed_ThrowsInvalidOperationException()
    {
        // Arrange
        var report = ScreeningReport.CreatePending(Guid.NewGuid());
        report.Fail("Initial error");

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => report.Fail("Second error"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Fail_WithEmptyReason_ThrowsArgumentException(string? invalidReason)
    {
        // Arrange
        var report = ScreeningReport.CreatePending(Guid.NewGuid());

        // Act & Assert
        Assert.Throws<ArgumentException>(() => report.Fail(invalidReason!));
    }
}
