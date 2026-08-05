namespace Ats.Db.Applications;

using System;

public class Application
{
    public Guid Id { get; private set; }
    public Guid RequisitionId { get; private set; }
    public Guid CandidateId { get; private set; }
    public DateTime SubmittedAtUtc { get; private set; }
    public CvAttachment? CvAttachment { get; private set; }

    private Application() { } // EF Core

    public static Application Create(Guid requisitionId, Guid candidateId)
    {
        if (requisitionId == Guid.Empty)
        {
            throw new ArgumentException("RequisitionId cannot be empty.", nameof(requisitionId));
        }

        if (candidateId == Guid.Empty)
        {
            throw new ArgumentException("CandidateId cannot be empty.", nameof(candidateId));
        }

        return new Application
        {
            Id = Guid.NewGuid(),
            RequisitionId = requisitionId,
            CandidateId = candidateId,
            SubmittedAtUtc = DateTime.UtcNow
        };
    }

    // Invariant: called exactly once, before the entity is ever added to the DbContext.
    // ApplicationService never calls DbContext.Applications.Add() without having called this
    // first (LLD §3.2 step 9) — the DB schema does not enforce "CvAttachment required" as a
    // constraint (same precedent as 0003's Status field: encoded in code shape, not a CHECK).
    public void AttachCv(CvAttachment cv)
    {
        ArgumentNullException.ThrowIfNull(cv);
        if (CvAttachment != null)
        {
            throw new InvalidOperationException("A CvAttachment is already attached.");
        }

        CvAttachment = cv;
    }
}
