namespace Ats.Service.Pipeline.Dtos;

using System;

/// <summary>One Application card on the staff pipeline board (FR-15). Mirrors api.md §4.</summary>
public record PipelineBoardApplicationDto(
    Guid ApplicationId,
    Guid CandidateId,
    string CandidateFirstName,
    string CandidateLastName,
    string CandidateEmail,
    DateTime SubmittedAtUtc,
    int? ScreeningScore = null,
    string? ScreeningRecommendation = null,
    string? ScreeningStatus = null);
