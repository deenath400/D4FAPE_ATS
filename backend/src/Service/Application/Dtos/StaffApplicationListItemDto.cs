namespace Ats.Service.Application.Dtos;

using System;

/// <summary>One row of a Staff Requisition-scoped Applications list (FR-10). Mirrors api.md §4.</summary>
public record StaffApplicationListItemDto(
    Guid Id,
    StaffApplicationCandidateDto Candidate,
    DateTime SubmittedAtUtc,
    string CvDownloadUrl);

/// <summary>Applying Candidate's identity, embedded in <see cref="StaffApplicationListItemDto"/>.</summary>
public record StaffApplicationCandidateDto(
    Guid Id,
    string FirstName,
    string LastName,
    string Email);
