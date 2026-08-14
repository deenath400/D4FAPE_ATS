namespace Ats.UnitTests.Screening;

using System;
using Ats.Db.Applications;
using Xunit;

public sealed class ScreeningReportEntityTests
{
    [Fact]
    public void CreatePending_SetsInitialValues_CategoryScoresAreNull()
    {
        // Arrange
        var appId = Guid.NewGuid();

        // Act
        var report = ScreeningReport.CreatePending(appId);

        // Assert
        Assert.Equal(appId, report.ApplicationId);
        Assert.Equal(0, report.Score);
        Assert.Equal(ScreeningRecommendation.Review, report.Recommendation);
        Assert.Equal(ScreeningStatus.Pending, report.Status);
        Assert.Null(report.SkillsScore);
        Assert.Null(report.ExperienceScore);
        Assert.Null(report.EducationScore);
        Assert.Null(report.FailureReason);
    }

    [Fact]
    public void Complete_WithCategoryScores_SetsClampedValues()
    {
        // Arrange
        var report = ScreeningReport.CreatePending(Guid.NewGuid());

        // Act
        report.Complete(
            score: 85,
            recommendation: ScreeningRecommendation.Advance,
            summary: "Good match",
            strengths: "[\"C#\"]",
            concerns: "[]",
            skillsScore: 120, // Should clamp to 100
            experienceScore: -10, // Should clamp to 0
            educationScore: 75);

        // Assert
        Assert.Equal(85, report.Score);
        Assert.Equal(ScreeningRecommendation.Advance, report.Recommendation);
        Assert.Equal(ScreeningStatus.Completed, report.Status);
        Assert.Equal(100, report.SkillsScore);
        Assert.Equal(0, report.ExperienceScore);
        Assert.Equal(75, report.EducationScore);
    }

    [Fact]
    public void Complete_WithoutCategoryScores_CategoryScoresRemainNull()
    {
        // Arrange
        var report = ScreeningReport.CreatePending(Guid.NewGuid());

        // Act
        report.Complete(
            score: 70,
            recommendation: ScreeningRecommendation.Review,
            summary: "Moderate match",
            strengths: "[\"C#\"]",
            concerns: "[\"No cloud experience\"]");

        // Assert
        Assert.Equal(70, report.Score);
        Assert.Equal(ScreeningStatus.Completed, report.Status);
        Assert.Null(report.SkillsScore);
        Assert.Null(report.ExperienceScore);
        Assert.Null(report.EducationScore);
    }

    [Fact]
    public void Fail_SetsFailedStatus_CategoryScoresRemainNull()
    {
        // Arrange
        var report = ScreeningReport.CreatePending(Guid.NewGuid());

        // Act
        report.Fail("AI service unavailable");

        // Assert
        Assert.Equal(ScreeningStatus.Failed, report.Status);
        Assert.Equal("AI service unavailable", report.FailureReason);
        Assert.Null(report.SkillsScore);
        Assert.Null(report.ExperienceScore);
        Assert.Null(report.EducationScore);
    }
}
