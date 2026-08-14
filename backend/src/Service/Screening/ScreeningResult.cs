namespace Ats.Service.Screening;

using Ats.Db.Applications;

public record ScreeningResult(
    int Score,
    ScreeningRecommendation Recommendation,
    string Summary,
    string Strengths,
    string Concerns);
