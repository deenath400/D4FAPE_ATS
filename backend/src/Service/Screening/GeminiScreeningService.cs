namespace Ats.Service.Screening;

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ats.Db.Applications;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public class GeminiScreeningService : IScreeningService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _options;
    private readonly ILogger<GeminiScreeningService> _logger;

    public GeminiScreeningService(
        HttpClient httpClient,
        IOptions<GeminiOptions> options,
        ILogger<GeminiScreeningService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ScreeningResult> EvaluateAsync(
        string requisitionTitle,
        string requisitionDescription,
        string cvText,
        CancellationToken ct = default)
    {
        requisitionTitle ??= string.Empty;
        requisitionDescription ??= string.Empty;
        cvText ??= string.Empty;

        // Truncate CV text to configured maximum length if necessary (E-2)
        if (cvText.Length > _options.MaxCvTextLength)
        {
            cvText = cvText.Substring(0, _options.MaxCvTextLength);
        }

        var requestBody = BuildGeminiRequest(requisitionTitle, requisitionDescription, cvText);
        var jsonPayload = JsonSerializer.Serialize(requestBody, JsonOptions);

        var baseUrl = string.IsNullOrWhiteSpace(_options.BaseUrl)
            ? "https://generativelanguage.googleapis.com"
            : _options.BaseUrl.TrimEnd('/');
        var model = string.IsNullOrWhiteSpace(_options.Model)
            ? "gemini-2.0-flash"
            : _options.Model;

        var requestUrl = $"{baseUrl}/v1beta/models/{model}:generateContent?key={_options.ApiKey}";

        var responseContent = await ExecuteWithRetryAsync(requestUrl, jsonPayload, ct);

        GeminiResponse? geminiResponse;
        try
        {
            geminiResponse = JsonSerializer.Deserialize<GeminiResponse>(responseContent, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize Gemini envelope response.");
            throw new InvalidOperationException("Invalid structured JSON response from AI provider", ex);
        }

        var candidate = geminiResponse?.Candidates?[0];
        var text = candidate?.Content?.Parts?[0]?.Text;

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("Invalid structured JSON response from AI provider");
        }

        GeminiScreeningOutput? output;
        try
        {
            output = JsonSerializer.Deserialize<GeminiScreeningOutput>(text, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse Gemini structured JSON screening output.");
            throw new InvalidOperationException("Invalid structured JSON response from AI provider", ex);
        }

        if (output == null)
        {
            throw new InvalidOperationException("Invalid structured JSON response from AI provider");
        }

        var score = Math.Clamp(output.Score, 0, 100);
        var skillsScore = Math.Clamp(output.SkillsScore, 0, 100);
        var experienceScore = Math.Clamp(output.ExperienceScore, 0, 100);
        var educationScore = Math.Clamp(output.EducationScore, 0, 100);

        var recommendation = string.Equals(output.Recommendation, "Advance", StringComparison.OrdinalIgnoreCase)
            ? ScreeningRecommendation.Advance
            : ScreeningRecommendation.Review;

        var summary = output.Summary ?? string.Empty;
        var strengthsJson = JsonSerializer.Serialize(output.Strengths ?? new List<string>());
        var concernsJson = JsonSerializer.Serialize(output.Concerns ?? new List<string>());

        return new ScreeningResult(
            score,
            recommendation,
            summary,
            strengthsJson,
            concernsJson,
            skillsScore,
            experienceScore,
            educationScore);
    }

    private async Task<string> ExecuteWithRetryAsync(string requestUrl, string jsonPayload, CancellationToken ct)
    {
        const int maxRetries = 2;
        var delay = TimeSpan.FromMilliseconds(500);

        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl)
            {
                Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json")
            };

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.SendAsync(request, ct);
            }
            catch (HttpRequestException ex) when (attempt < maxRetries)
            {
                _logger.LogWarning(ex, "Transient HTTP request exception communicating with Gemini API (attempt {Attempt}). Retrying in {Delay}ms...", attempt + 1, delay.TotalMilliseconds);
                await Task.Delay(delay, ct);
                delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, 4));
                continue;
            }

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsStringAsync(ct);
            }

            var statusCode = response.StatusCode;
            var errorBody = await response.Content.ReadAsStringAsync(ct);

            // Auth errors: 401 or 403 — fail immediately without retrying (E-1)
            if (statusCode == HttpStatusCode.Unauthorized || statusCode == HttpStatusCode.Forbidden)
            {
                _logger.LogError("Gemini API authentication failed ({StatusCode}). Check Gemini:ApiKey configuration.", statusCode);
                throw new InvalidOperationException("Gemini authentication failed: check API key");
            }

            // Transient errors: 429 Too Many Requests or 503 Service Unavailable — retry with backoff (E-5)
            if ((statusCode == HttpStatusCode.TooManyRequests || statusCode == HttpStatusCode.ServiceUnavailable) && attempt < maxRetries)
            {
                _logger.LogWarning("Gemini API returned transient status {StatusCode} (attempt {Attempt}). Retrying in {Delay}ms...", statusCode, attempt + 1, delay.TotalMilliseconds);
                await Task.Delay(delay, ct);
                delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, 4));
                continue;
            }

            // Terminal error
            if (statusCode == HttpStatusCode.TooManyRequests)
            {
                _logger.LogError("Gemini API rate limit exceeded after retries.");
                throw new HttpRequestException("Gemini API rate limit exceeded", null, statusCode);
            }

            _logger.LogError("Gemini API error ({StatusCode}): {ErrorExcerpt}", statusCode, errorBody.Length > 200 ? errorBody.Substring(0, 200) : errorBody);
            throw new HttpRequestException($"Gemini API error ({statusCode}): {errorBody}", null, statusCode);
        }

        throw new HttpRequestException("Gemini API request failed after retries.");
    }

    private static GeminiRequest BuildGeminiRequest(string title, string description, string cvText)
    {
        var systemInstructionText =
            "You are an expert AI recruiting assistant evaluating candidate resumes against job requisitions. " +
            "Evaluate the candidate CV thoroughly against the job title and job description. " +
            "Provide objective numerical scores from 0 to 100 for overall fit, skills fit, experience fit, and education fit. " +
            "If the overall score is 75 or higher, recommend 'Advance'; otherwise recommend 'Review'. " +
            "Provide a concise executive summary, bullet points of key candidate strengths, and bullet points of potential gaps or areas for review.";

        var userPromptText =
            $"### JOB REQUISITION\n" +
            $"Title: {title}\n\n" +
            $"Description:\n{description}\n\n" +
            $"### CANDIDATE CV TEXT\n{cvText}\n\n" +
            $"Evaluate the candidate against the job criteria and output the structured evaluation.";

        var properties = new Dictionary<string, GeminiSchemaProperty>
        {
            ["score"] = new() { Type = "INTEGER", Description = "Overall fit score from 0 to 100" },
            ["skillsScore"] = new() { Type = "INTEGER", Description = "Skills fit score from 0 to 100" },
            ["experienceScore"] = new() { Type = "INTEGER", Description = "Experience fit score from 0 to 100" },
            ["educationScore"] = new() { Type = "INTEGER", Description = "Education fit score from 0 to 100" },
            ["recommendation"] = new()
            {
                Type = "STRING",
                Description = "Recommendation: 'Advance' if score >= 75, otherwise 'Review'",
                Enum = new List<string> { "Advance", "Review" }
            },
            ["summary"] = new() { Type = "STRING", Description = "Executive match summary (2-4 sentences)" },
            ["strengths"] = new()
            {
                Type = "ARRAY",
                Description = "List of key candidate strengths and qualifications",
                Items = new GeminiSchemaProperty { Type = "STRING" }
            },
            ["concerns"] = new()
            {
                Type = "ARRAY",
                Description = "List of potential concerns, skill gaps, or areas for recruiter review",
                Items = new GeminiSchemaProperty { Type = "STRING" }
            }
        };

        var required = new List<string>
        {
            "score",
            "skillsScore",
            "experienceScore",
            "educationScore",
            "recommendation",
            "summary",
            "strengths",
            "concerns"
        };

        return new GeminiRequest
        {
            SystemInstruction = new GeminiSystemInstruction
            {
                Parts = new List<GeminiPart> { new() { Text = systemInstructionText } }
            },
            Contents = new List<GeminiContent>
            {
                new()
                {
                    Role = "user",
                    Parts = new List<GeminiPart> { new() { Text = userPromptText } }
                }
            },
            GenerationConfig = new GeminiGenerationConfig
            {
                ResponseMimeType = "application/json",
                ResponseSchema = new GeminiResponseSchema
                {
                    Type = "OBJECT",
                    Properties = properties,
                    Required = required
                }
            }
        };
    }
}
