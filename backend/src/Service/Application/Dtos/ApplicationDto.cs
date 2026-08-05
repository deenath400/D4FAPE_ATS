namespace Ats.Service.Application.Dtos;

using System;

/// <summary>Response for a successful Application submission (FR-1, FR-7, FR-13). Mirrors api.md §4.</summary>
public record ApplicationDto(
    Guid Id,
    Guid RequisitionId,
    Guid CandidateId,
    DateTime SubmittedAtUtc,
    ApplicationCvSummaryDto Cv);

/// <summary>CV metadata embedded in <see cref="ApplicationDto"/>.</summary>
public record ApplicationCvSummaryDto(
    string FileName,
    string ContentType,
    long SizeBytes);
