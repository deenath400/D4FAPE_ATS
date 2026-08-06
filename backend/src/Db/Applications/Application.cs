namespace Ats.Db.Applications;

using System;

public class Application
{
    public Guid Id { get; private set; }
    public Guid RequisitionId { get; private set; }
    public Guid CandidateId { get; private set; }
    public DateTime SubmittedAtUtc { get; private set; }
    public Guid CurrentStageId { get; private set; }
    public bool IsRejected { get; private set; }
    public CvAttachment? CvAttachment { get; private set; }

    private Application() { } // EF Core

    // currentStageId is a required constructor argument, not assigned after the fact — G-1
    // requires the current-Stage reference to exist "from the moment it is submitted",
    // structurally, not as a follow-up call an operator could omit (unlike AttachCv, which
    // stays a post-hoc call since a CvAttachment is a distinct dependent row, not a scalar FK).
    public static Application Create(Guid requisitionId, Guid candidateId, Guid currentStageId)
    {
        if (requisitionId == Guid.Empty)
        {
            throw new ArgumentException("RequisitionId cannot be empty.", nameof(requisitionId));
        }

        if (candidateId == Guid.Empty)
        {
            throw new ArgumentException("CandidateId cannot be empty.", nameof(candidateId));
        }

        if (currentStageId == Guid.Empty)
        {
            throw new ArgumentException("CurrentStageId cannot be empty.", nameof(currentStageId));
        }

        return new Application
        {
            Id = Guid.NewGuid(),
            RequisitionId = requisitionId,
            CandidateId = candidateId,
            SubmittedAtUtc = DateTime.UtcNow,
            CurrentStageId = currentStageId,
            IsRejected = false
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

    // Unconditional, like Requisition.Publish()/Close() — PipelineService checks legality
    // (not rejected, Requisition not closed, target Stage in the same Requisition, FR-22's
    // expected-stage match) before calling this.
    public void MoveToStage(Guid stageId) => CurrentStageId = stageId;

    // Unconditional, same reasoning. CurrentStageId is deliberately left unchanged (FR-10:
    // "retains the Stage it was rejected from").
    public void Reject() => IsRejected = true;
}
