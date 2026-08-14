namespace Ats.Db.Applications;

using System;

public class ScreeningReport
{
    public Guid Id { get; private set; }
    public Guid ApplicationId { get; private set; }
    public int Score { get; private set; }
    public ScreeningRecommendation Recommendation { get; private set; }
    public string Summary { get; private set; } = string.Empty;
    public string Strengths { get; private set; } = "[]";
    public string Concerns { get; private set; } = "[]";
    public ScreeningStatus Status { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTime EvaluatedAtUtc { get; private set; }
    public int? SkillsScore { get; private set; }
    public int? ExperienceScore { get; private set; }
    public int? EducationScore { get; private set; }

    private ScreeningReport() { } // EF Core

    public static ScreeningReport CreatePending(Guid applicationId)
    {
        if (applicationId == Guid.Empty)
        {
            throw new ArgumentException("ApplicationId cannot be empty.", nameof(applicationId));
        }

        return new ScreeningReport
        {
            Id = Guid.NewGuid(),
            ApplicationId = applicationId,
            Score = 0,
            Recommendation = ScreeningRecommendation.Review,
            Summary = string.Empty,
            Strengths = "[]",
            Concerns = "[]",
            Status = ScreeningStatus.Pending,
            FailureReason = null,
            EvaluatedAtUtc = DateTime.UtcNow,
            SkillsScore = null,
            ExperienceScore = null,
            EducationScore = null
        };
    }

    public void Complete(
        int score,
        ScreeningRecommendation recommendation,
        string summary,
        string strengths,
        string concerns,
        int? skillsScore = null,
        int? experienceScore = null,
        int? educationScore = null)
    {
        if (Status != ScreeningStatus.Pending)
        {
            throw new InvalidOperationException("Cannot complete a report that is not in Pending status.");
        }

        Score = Math.Clamp(score, 0, 100);
        Recommendation = recommendation;
        Summary = summary ?? string.Empty;
        Strengths = string.IsNullOrWhiteSpace(strengths) ? "[]" : strengths;
        Concerns = string.IsNullOrWhiteSpace(concerns) ? "[]" : concerns;
        SkillsScore = skillsScore.HasValue ? Math.Clamp(skillsScore.Value, 0, 100) : null;
        ExperienceScore = experienceScore.HasValue ? Math.Clamp(experienceScore.Value, 0, 100) : null;
        EducationScore = educationScore.HasValue ? Math.Clamp(educationScore.Value, 0, 100) : null;
        Status = ScreeningStatus.Completed;
        FailureReason = null;
        EvaluatedAtUtc = DateTime.UtcNow;
    }

    public void Fail(string reason)
    {
        if (Status != ScreeningStatus.Pending)
        {
            throw new InvalidOperationException("Cannot fail a report that is not in Pending status.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Reason cannot be empty.", nameof(reason));
        }

        Status = ScreeningStatus.Failed;
        FailureReason = reason;
        EvaluatedAtUtc = DateTime.UtcNow;
    }
}
