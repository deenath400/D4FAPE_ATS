namespace Ats.Db.Pipeline;

// FR-13/C-2: a forward-compatible column shape only. No code path in this spec produces
// ActorKind.System — every transition this spec writes is ActorKind.User.
public enum StageTransitionActorKind
{
    User,
    System
}
