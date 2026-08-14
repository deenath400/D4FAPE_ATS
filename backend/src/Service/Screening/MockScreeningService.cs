namespace Ats.Service.Screening;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Ats.Db.Applications;
using Microsoft.Extensions.Configuration;

public class MockScreeningService : IScreeningService
{
    private const int DefaultQualificationThreshold = 75;
    private static readonly Regex WordRegex = new(@"\b[a-zA-Z0-9_#\+\.]+\b", RegexOptions.Compiled);

    private readonly int _threshold;

    public MockScreeningService(IConfiguration? configuration = null)
    {
        if (configuration != null && int.TryParse(configuration["Screening:QualificationThreshold"], out var threshold))
        {
            _threshold = threshold;
        }
        else
        {
            _threshold = DefaultQualificationThreshold;
        }
    }

    public Task<ScreeningResult> EvaluateAsync(
        string requisitionTitle,
        string requisitionDescription,
        string cvText,
        CancellationToken ct = default)
    {
        requisitionTitle ??= string.Empty;
        requisitionDescription ??= string.Empty;
        cvText ??= string.Empty;

        var cvWords = new HashSet<string>(
            WordRegex.Matches(cvText)
                .Select(m => m.Value.ToLowerInvariant())
                .Where(w => w.Length >= 2),
            StringComparer.OrdinalIgnoreCase);

        var jobCriteria = $"{requisitionTitle} {requisitionDescription}";
        var jobWords = WordRegex.Matches(jobCriteria)
            .Select(m => m.Value.ToLowerInvariant())
            .Where(w => w.Length >= 3)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var matchedKeywords = jobWords
            .Where(w => cvWords.Contains(w))
            .ToList();

        var matchCount = matchedKeywords.Count;
        var score = Math.Clamp(matchCount * 10, 0, 100);
        var recommendation = score >= _threshold ? ScreeningRecommendation.Advance : ScreeningRecommendation.Review;

        var summary = $"Mock screening: {matchCount} keyword matches found.";
        var strengthsList = matchedKeywords.Take(3).Select(w => $"Demonstrated experience with {w}").ToList();
        if (strengthsList.Count == 0)
        {
            strengthsList.Add("General applicant qualifications");
        }

        var strengthsJson = JsonSerializer.Serialize(strengthsList);
        var concernsJson = JsonSerializer.Serialize(new[] { "Mock provider — no real AI evaluation performed" });

        var result = new ScreeningResult(
            score,
            recommendation,
            summary,
            strengthsJson,
            concernsJson);

        return Task.FromResult(result);
    }
}
