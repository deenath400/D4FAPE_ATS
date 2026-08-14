namespace Ats.UnitTests.Screening;

using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ats.Db.Applications;
using Ats.Service.Screening;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

public sealed class GeminiScreeningServiceTests
{
    private sealed class TestHttpMessageHandler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, HttpResponseMessage>? HandlerFunc { get; set; }
        public int CallCount { get; private set; }
        public string? LastRequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            if (request.Content != null)
            {
                LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            if (HandlerFunc != null)
            {
                return HandlerFunc(request);
            }

            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    private static GeminiScreeningService CreateService(
        HttpMessageHandler handler,
        string apiKey = "test-api-key",
        int maxCvLength = 50_000)
    {
        var httpClient = new HttpClient(handler);
        var options = Options.Create(new GeminiOptions
        {
            ApiKey = apiKey,
            Model = "gemini-2.0-flash",
            BaseUrl = "https://generativelanguage.googleapis.com",
            TimeoutSeconds = 5,
            MaxCvTextLength = maxCvLength
        });

        return new GeminiScreeningService(httpClient, options, NullLogger<GeminiScreeningService>.Instance);
    }

    private static string CreateValidGeminiResponseJson(
        int score = 85,
        int skillsScore = 90,
        int experienceScore = 80,
        int educationScore = 85,
        string recommendation = "Advance",
        string summary = "Strong applicant match.")
    {
        var output = new GeminiScreeningOutput
        {
            Score = score,
            SkillsScore = skillsScore,
            ExperienceScore = experienceScore,
            EducationScore = educationScore,
            Recommendation = recommendation,
            Summary = summary,
            Strengths = new() { "Strong .NET background", "System design skills" },
            Concerns = new() { "Limited cloud experience" }
        };

        var structuredJson = JsonSerializer.Serialize(output);

        var responseEnvelope = new
        {
            candidates = new[]
            {
                new
                {
                    content = new
                    {
                        parts = new[]
                        {
                            new { text = structuredJson }
                        },
                        role = "model"
                    },
                    finishReason = "STOP"
                }
            }
        };

        return JsonSerializer.Serialize(responseEnvelope);
    }

    [Fact]
    public async Task EvaluateAsync_ValidResponse_ReturnsScreeningResultWithCategoryScores()
    {
        // Arrange
        var handler = new TestHttpMessageHandler
        {
            HandlerFunc = _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(CreateValidGeminiResponseJson(85, 92, 80, 75, "Advance", "Excellent fit."))
            }
        };

        var service = CreateService(handler);

        // Act
        var result = await service.EvaluateAsync(
            "Senior Developer",
            "Must have C# and .NET experience",
            "Experienced C# developer with 8 years in .NET");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(85, result.Score);
        Assert.Equal(ScreeningRecommendation.Advance, result.Recommendation);
        Assert.Equal("Excellent fit.", result.Summary);
        Assert.Equal(92, result.SkillsScore);
        Assert.Equal(80, result.ExperienceScore);
        Assert.Equal(75, result.EducationScore);
        Assert.Contains("Strong .NET background", result.Strengths);
        Assert.Contains("Limited cloud experience", result.Concerns);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task EvaluateAsync_ReviewRecommendation_ReturnsReviewResult()
    {
        // Arrange
        var handler = new TestHttpMessageHandler
        {
            HandlerFunc = _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(CreateValidGeminiResponseJson(60, 65, 55, 60, "Review", "Moderate fit."))
            }
        };

        var service = CreateService(handler);

        // Act
        var result = await service.EvaluateAsync(
            "Senior Developer",
            "Must have C# and .NET experience",
            "Junior developer looking for first role");

        // Assert
        Assert.Equal(60, result.Score);
        Assert.Equal(ScreeningRecommendation.Review, result.Recommendation);
        Assert.Equal(65, result.SkillsScore);
    }

    [Fact]
    public async Task EvaluateAsync_Http401_ThrowsWithAuthMessage()
    {
        // Arrange
        var handler = new TestHttpMessageHandler
        {
            HandlerFunc = _ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("{\"error\": {\"message\": \"API key not valid.\"}}")
            }
        };

        var service = CreateService(handler);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.EvaluateAsync("Title", "Description", "CV text"));

        Assert.Contains("Gemini authentication failed: check API key", ex.Message);
        Assert.Equal(1, handler.CallCount); // Should NOT retry on auth failure
    }

    [Fact]
    public async Task EvaluateAsync_Http429_RetriesThenThrows()
    {
        // Arrange
        var handler = new TestHttpMessageHandler
        {
            HandlerFunc = _ => new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Content = new StringContent("{\"error\": {\"message\": \"Resource has been exhausted.\"}}")
            }
        };

        var service = CreateService(handler);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            service.EvaluateAsync("Title", "Description", "CV text"));

        Assert.Contains("rate limit exceeded", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(3, handler.CallCount); // 1 initial + 2 retries
    }

    [Fact]
    public async Task EvaluateAsync_Http503_RetriesThenThrows()
    {
        // Arrange
        var handler = new TestHttpMessageHandler
        {
            HandlerFunc = _ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent("Service Unavailable")
            }
        };

        var service = CreateService(handler);

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            service.EvaluateAsync("Title", "Description", "CV text"));

        Assert.Equal(3, handler.CallCount); // 1 initial + 2 retries
    }

    [Fact]
    public async Task EvaluateAsync_MalformedJson_ThrowsWithParseMessage()
    {
        // Arrange
        var handler = new TestHttpMessageHandler
        {
            HandlerFunc = _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"candidates\": [{\"content\": {\"parts\": [{\"text\": \"invalid json\"}]}}]}")
            }
        };

        var service = CreateService(handler);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.EvaluateAsync("Title", "Description", "CV text"));

        Assert.Contains("Invalid structured JSON response from AI provider", ex.Message);
    }

    [Fact]
    public async Task EvaluateAsync_LongCvText_TruncatedBeforeSending()
    {
        // Arrange
        var handler = new TestHttpMessageHandler
        {
            HandlerFunc = _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(CreateValidGeminiResponseJson())
            }
        };

        var service = CreateService(handler, maxCvLength: 100);
        var longCvText = new string('A', 500);

        // Act
        await service.EvaluateAsync("Title", "Description", longCvText);

        // Assert
        Assert.NotNull(handler.LastRequestBody);
        Assert.DoesNotContain(longCvText, handler.LastRequestBody);
        Assert.Contains(new string('A', 100), handler.LastRequestBody);
    }
}
