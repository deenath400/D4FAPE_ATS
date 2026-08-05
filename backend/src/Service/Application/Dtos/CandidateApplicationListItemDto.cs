namespace Ats.Service.Application.Dtos;

using System;

/// <summary>One row of a Candidate's own Applications list (FR-8). Mirrors api.md §4.</summary>
public record CandidateApplicationListItemDto(
    Guid Id,
    Guid RequisitionId,
    string RequisitionTitle,
    DateTime SubmittedAtUtc,
    string CvDownloadUrl);
