namespace Ats.Service.Application.Dtos;

using System.IO;

/// <summary>
/// Stream + metadata for a CV download (FR-9, FR-11). The caller (the API layer) owns disposing
/// <see cref="Content"/> once the response has been written.
/// </summary>
public record CvDownloadResult(
    Stream Content,
    string FileName,
    string ContentType);
