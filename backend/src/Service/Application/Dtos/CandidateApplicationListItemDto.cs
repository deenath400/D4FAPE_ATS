namespace Ats.Service.Application.Dtos;

using System;

/// <summary>One row of a Candidate's own Applications list (FR-8). Mirrors api.md §4.
/// <c>CurrentStageName</c>/<c>IsRejected</c> are additive fields (0005 FR-17) — a rejected
/// Application still carries its retained current Stage name; the frontend, not the API,
/// decides to show a rejected indicator instead (AC-23).</summary>
public record CandidateApplicationListItemDto(
    Guid Id,
    Guid RequisitionId,
    string RequisitionTitle,
    DateTime SubmittedAtUtc,
    string CvDownloadUrl,
    string CurrentStageName,
    bool IsRejected);
