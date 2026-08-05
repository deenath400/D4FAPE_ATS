namespace Ats.Db.Applications;

using System;

public class CvAttachment
{
    public Guid Id { get; private set; }
    public Guid ApplicationId { get; private set; }
    public string StorageKey { get; private set; } = string.Empty;
    public string OriginalFileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }
    public DateTime UploadedAtUtc { get; private set; }

    private CvAttachment() { } // EF Core

    public static CvAttachment Create(
        Guid applicationId, string storageKey, string originalFileName, string contentType, long sizeBytes)
    {
        if (applicationId == Guid.Empty)
        {
            throw new ArgumentException("ApplicationId cannot be empty.", nameof(applicationId));
        }

        if (string.IsNullOrWhiteSpace(storageKey))
        {
            throw new ArgumentException("StorageKey cannot be empty.", nameof(storageKey));
        }

        if (sizeBytes <= 0)
        {
            throw new ArgumentException("SizeBytes must be positive.", nameof(sizeBytes));
        }

        return new CvAttachment
        {
            Id = Guid.NewGuid(),
            ApplicationId = applicationId,
            StorageKey = storageKey,
            OriginalFileName = originalFileName,
            ContentType = contentType,
            SizeBytes = sizeBytes,
            UploadedAtUtc = DateTime.UtcNow
        };
    }
}
