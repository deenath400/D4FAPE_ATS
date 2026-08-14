namespace Ats.UnitTests.Screening;

using System.Threading.Tasks;
using Ats.Db.Applications;
using Ats.Service.Screening;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using Xunit;

public class MockScreeningServiceTests
{
    [Fact]
    public async Task EvaluateAsync_WithManyMatchingKeywords_ReturnsAdvanceScore()
    {
        // Arrange
        var service = new MockScreeningService();
        var title = "Senior .NET C# Backend Software Engineer";
        var description = "Looking for an expert with ASP.NET Core, EF Core, Sqlite, Architecture, Testing, Security, Pipelines.";
        var cvText = "Expert in .NET, C#, Backend, Software, Engineer, ASP.NET, Core, Sqlite, Architecture, Testing, Security, Pipelines.";

        // Act
        var result = await service.EvaluateAsync(title, description, cvText);

        // Assert
        Assert.True(result.Score >= 75);
        Assert.Equal(ScreeningRecommendation.Advance, result.Recommendation);
        Assert.Contains("keyword matches found", result.Summary);
        Assert.NotEmpty(result.Strengths);
        Assert.NotEmpty(result.Concerns);
    }

    [Fact]
    public async Task EvaluateAsync_WithFewMatchingKeywords_ReturnsReviewScore()
    {
        // Arrange
        var service = new MockScreeningService();
        var title = "Senior Backend Engineer";
        var description = "Kubernetes Docker Go Rust Distributed Systems";
        var cvText = "Junior Graphic Designer with Photoshop and Illustrator experience";

        // Act
        var result = await service.EvaluateAsync(title, description, cvText);

        // Assert
        Assert.True(result.Score < 75);
        Assert.Equal(ScreeningRecommendation.Review, result.Recommendation);
    }

    [Fact]
    public async Task EvaluateAsync_WithCustomThreshold_RespectsConfig()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Screening:QualificationThreshold"] = "50"
            })
            .Build();
        var service = new MockScreeningService(config);

        var title = "Backend Engineer";
        var description = "C# .NET Core SQL API Developer";
        var cvText = "I am a C# .NET Core SQL API Developer";

        // Act
        var result = await service.EvaluateAsync(title, description, cvText);

        // Assert
        Assert.True(result.Score >= 50);
        Assert.Equal(ScreeningRecommendation.Advance, result.Recommendation);
    }

    [Fact]
    public async Task EvaluateAsync_IsDeterministic_ForIdenticalInputs()
    {
        // Arrange
        var service = new MockScreeningService();
        var title = "Data Engineer";
        var description = "Python Spark SQL ETL AWS";
        var cvText = "Experienced with Python, SQL and AWS pipelines";

        // Act
        var result1 = await service.EvaluateAsync(title, description, cvText);
        var result2 = await service.EvaluateAsync(title, description, cvText);

        // Assert
        Assert.Equal(result1.Score, result2.Score);
        Assert.Equal(result1.Recommendation, result2.Recommendation);
        Assert.Equal(result1.Summary, result2.Summary);
        Assert.Equal(result1.Strengths, result2.Strengths);
        Assert.Equal(result1.Concerns, result2.Concerns);
    }
}
