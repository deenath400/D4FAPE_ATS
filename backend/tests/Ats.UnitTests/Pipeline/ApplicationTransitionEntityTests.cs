namespace Ats.UnitTests.Pipeline;

using System;
using Ats.Db.Pipeline;
using Xunit;

public class ApplicationTransitionEntityTests
{
    [Fact]
    public void StageTransition_CreateMove_PopulatesBothStages()
    {
        // Arrange
        var applicationId = Guid.NewGuid();
        var fromStageId = Guid.NewGuid();
        var toStageId = Guid.NewGuid();
        var actorUserId = Guid.NewGuid();

        // Act
        var transition = StageTransition.CreateMove(
            applicationId, fromStageId, "Applied", toStageId, "Screening", actorUserId, "Jane Recruiter", "Looks strong.");

        // Assert
        Assert.NotEqual(Guid.Empty, transition.Id);
        Assert.Equal(applicationId, transition.ApplicationId);
        Assert.Equal(fromStageId, transition.FromStageId);
        Assert.Equal("Applied", transition.FromStageName);
        Assert.Equal(toStageId, transition.ToStageId);
        Assert.Equal("Screening", transition.ToStageName);
        Assert.Equal(StageTransitionKind.Move, transition.Kind);
        Assert.Equal(StageTransitionActorKind.User, transition.ActorKind);
        Assert.Equal(actorUserId, transition.ActorUserId);
        Assert.Equal("Jane Recruiter", transition.ActorDisplayLabel);
        Assert.Equal("Looks strong.", transition.Note);
        Assert.True(transition.OccurredAtUtc <= DateTime.UtcNow);
    }

    [Fact]
    public void StageTransition_CreateMove_WithNullNote_LeavesNoteNull()
    {
        // Arrange & Act
        var transition = StageTransition.CreateMove(
            Guid.NewGuid(), Guid.NewGuid(), "Applied", Guid.NewGuid(), "Screening", Guid.NewGuid(), "Jane Recruiter", null);

        // Assert
        Assert.Null(transition.Note);
    }

    [Fact]
    public void StageTransition_CreateRejection_LeavesToStageNull()
    {
        // Arrange
        var applicationId = Guid.NewGuid();
        var fromStageId = Guid.NewGuid();
        var actorUserId = Guid.NewGuid();

        // Act
        var transition = StageTransition.CreateRejection(
            applicationId, fromStageId, "Interview", actorUserId, "Jane Recruiter", "Not a fit.");

        // Assert
        Assert.Equal(applicationId, transition.ApplicationId);
        Assert.Equal(fromStageId, transition.FromStageId);
        Assert.Equal("Interview", transition.FromStageName);
        Assert.Null(transition.ToStageId);
        Assert.Null(transition.ToStageName);
        Assert.Equal(StageTransitionKind.Reject, transition.Kind);
        Assert.Equal(StageTransitionActorKind.User, transition.ActorKind);
        Assert.Equal(actorUserId, transition.ActorUserId);
        Assert.Equal("Jane Recruiter", transition.ActorDisplayLabel);
        Assert.Equal("Not a fit.", transition.Note);
    }

    [Fact]
    public void StageTransition_CreateRejection_WithNullNote_LeavesNoteNull()
    {
        // Arrange & Act
        var transition = StageTransition.CreateRejection(
            Guid.NewGuid(), Guid.NewGuid(), "Interview", Guid.NewGuid(), "Jane Recruiter", null);

        // Assert
        Assert.Null(transition.Note);
    }

    [Fact]
    public void StageTransition_CreateMove_WithEmptyApplicationId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => StageTransition.CreateMove(
            Guid.Empty, Guid.NewGuid(), "Applied", Guid.NewGuid(), "Screening", Guid.NewGuid(), "Jane Recruiter", null));
    }

    [Fact]
    public void StageTransition_CreateMove_WithEmptyToStageId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => StageTransition.CreateMove(
            Guid.NewGuid(), Guid.NewGuid(), "Applied", Guid.Empty, "Screening", Guid.NewGuid(), "Jane Recruiter", null));
    }

    [Fact]
    public void StageTransition_CreateRejection_WithEmptyFromStageId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => StageTransition.CreateRejection(
            Guid.NewGuid(), Guid.Empty, "Interview", Guid.NewGuid(), "Jane Recruiter", null));
    }

    [Fact]
    public void StageTransition_CreateRejection_WithEmptyActorUserId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => StageTransition.CreateRejection(
            Guid.NewGuid(), Guid.NewGuid(), "Interview", Guid.Empty, "Jane Recruiter", null));
    }
}
