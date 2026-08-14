namespace Ats.Db.Pipeline;

using System;

// Append-only audit record of every move and rejection (FR-12, FR-14). Snapshots
// FromStageName/ToStageName at write time rather than trusting a live join back to Stages, so
// history survives a later rename or (unoccupied) delete of the referenced Stage (HLD D-1).
public class StageTransition
{
    public Guid Id { get; private set; }
    public Guid ApplicationId { get; private set; }
    public Guid? FromStageId { get; private set; }
    public string FromStageName { get; private set; } = string.Empty;
    public Guid? ToStageId { get; private set; }
    public string? ToStageName { get; private set; }
    public StageTransitionKind Kind { get; private set; }
    public StageTransitionActorKind ActorKind { get; private set; }
    public Guid? ActorUserId { get; private set; }
    public string ActorDisplayLabel { get; private set; } = string.Empty;
    public string? Note { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }

    private StageTransition() { } // EF Core

    // No mutator exists beyond the two factories below — append-only by construction (FR-14).
    // Kind is the sole authoritative discriminator for "move vs. reject" — a reader must never
    // infer it from ToStageId's nullability alone (a move's ToStageId can independently become
    // null via ON DELETE SET NULL if its target Stage is later deleted).
    public static StageTransition CreateMove(
        Guid applicationId,
        Guid fromStageId,
        string fromStageName,
        Guid toStageId,
        string toStageName,
        Guid actorUserId,
        string actorDisplayLabel,
        string? note)
    {
        if (applicationId == Guid.Empty)
        {
            throw new ArgumentException("ApplicationId cannot be empty.", nameof(applicationId));
        }

        if (fromStageId == Guid.Empty)
        {
            throw new ArgumentException("FromStageId cannot be empty.", nameof(fromStageId));
        }

        if (string.IsNullOrWhiteSpace(fromStageName))
        {
            throw new ArgumentException("FromStageName cannot be empty.", nameof(fromStageName));
        }

        if (toStageId == Guid.Empty)
        {
            throw new ArgumentException("ToStageId cannot be empty.", nameof(toStageId));
        }

        if (string.IsNullOrWhiteSpace(toStageName))
        {
            throw new ArgumentException("ToStageName cannot be empty.", nameof(toStageName));
        }

        if (actorUserId == Guid.Empty)
        {
            throw new ArgumentException("ActorUserId cannot be empty.", nameof(actorUserId));
        }

        if (string.IsNullOrWhiteSpace(actorDisplayLabel))
        {
            throw new ArgumentException("ActorDisplayLabel cannot be empty.", nameof(actorDisplayLabel));
        }

        return new StageTransition
        {
            Id = Guid.NewGuid(),
            ApplicationId = applicationId,
            FromStageId = fromStageId,
            FromStageName = fromStageName,
            ToStageId = toStageId,
            ToStageName = toStageName,
            Kind = StageTransitionKind.Move,
            ActorKind = StageTransitionActorKind.User,
            ActorUserId = actorUserId,
            ActorDisplayLabel = actorDisplayLabel,
            Note = note,
            OccurredAtUtc = DateTime.UtcNow
        };
    }

    public static StageTransition CreateSystemMove(
        Guid applicationId,
        Guid fromStageId,
        string fromStageName,
        Guid toStageId,
        string toStageName,
        string actorDisplayLabel,
        string? note)
    {
        if (applicationId == Guid.Empty)
        {
            throw new ArgumentException("ApplicationId cannot be empty.", nameof(applicationId));
        }

        if (fromStageId == Guid.Empty)
        {
            throw new ArgumentException("FromStageId cannot be empty.", nameof(fromStageId));
        }

        if (string.IsNullOrWhiteSpace(fromStageName))
        {
            throw new ArgumentException("FromStageName cannot be empty.", nameof(fromStageName));
        }

        if (toStageId == Guid.Empty)
        {
            throw new ArgumentException("ToStageId cannot be empty.", nameof(toStageId));
        }

        if (string.IsNullOrWhiteSpace(toStageName))
        {
            throw new ArgumentException("ToStageName cannot be empty.", nameof(toStageName));
        }

        if (string.IsNullOrWhiteSpace(actorDisplayLabel))
        {
            throw new ArgumentException("ActorDisplayLabel cannot be empty.", nameof(actorDisplayLabel));
        }

        return new StageTransition
        {
            Id = Guid.NewGuid(),
            ApplicationId = applicationId,
            FromStageId = fromStageId,
            FromStageName = fromStageName,
            ToStageId = toStageId,
            ToStageName = toStageName,
            Kind = StageTransitionKind.Move,
            ActorKind = StageTransitionActorKind.System,
            ActorUserId = null,
            ActorDisplayLabel = actorDisplayLabel,
            Note = note,
            OccurredAtUtc = DateTime.UtcNow
        };
    }

    public static StageTransition CreateRejection(
        Guid applicationId,
        Guid fromStageId,
        string fromStageName,
        Guid actorUserId,
        string actorDisplayLabel,
        string? note)
    {
        if (applicationId == Guid.Empty)
        {
            throw new ArgumentException("ApplicationId cannot be empty.", nameof(applicationId));
        }

        if (fromStageId == Guid.Empty)
        {
            throw new ArgumentException("FromStageId cannot be empty.", nameof(fromStageId));
        }

        if (string.IsNullOrWhiteSpace(fromStageName))
        {
            throw new ArgumentException("FromStageName cannot be empty.", nameof(fromStageName));
        }

        if (actorUserId == Guid.Empty)
        {
            throw new ArgumentException("ActorUserId cannot be empty.", nameof(actorUserId));
        }

        if (string.IsNullOrWhiteSpace(actorDisplayLabel))
        {
            throw new ArgumentException("ActorDisplayLabel cannot be empty.", nameof(actorDisplayLabel));
        }

        return new StageTransition
        {
            Id = Guid.NewGuid(),
            ApplicationId = applicationId,
            FromStageId = fromStageId,
            FromStageName = fromStageName,
            ToStageId = null,
            ToStageName = null,
            Kind = StageTransitionKind.Reject,
            ActorKind = StageTransitionActorKind.User,
            ActorUserId = actorUserId,
            ActorDisplayLabel = actorDisplayLabel,
            Note = note,
            OccurredAtUtc = DateTime.UtcNow
        };
    }
}
