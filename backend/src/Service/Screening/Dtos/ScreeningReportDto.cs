namespace Ats.Service.Screening.Dtos;

using System;

public record ScreeningReportDto(
    Guid Id,
    Guid ApplicationId,
    int Score,
    string Recommendation,
    string Summary,
    string[] Strengths,
    string[] Concerns,
    string Status,
    string? FailureReason,
    DateTime EvaluatedAtUtc,
    int? SkillsScore = null,
    int? ExperienceScore = null,
    int? EducationScore = null);
