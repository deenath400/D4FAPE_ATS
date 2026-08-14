namespace Ats.UnitTests.Pipeline;

using System;
using Ats.Db.Pipeline;
using Xunit;

public class StageTransitionSystemMoveTests
{
    [Fact]
    public void CreateSystemMove_PopulatesSystemFieldsAndNullActorUserId()
    {
        // Arrange
        var applicationId = Guid.NewGuid();
        var fromStageId = Guid.NewGuid();
        var toStageId = Guid.NewGuid();

        // Act
        var transition = StageTransition.CreateSystemMove(
            applicationId, fromStageId, "Applied", toStageId, "Screening", "AI Screening Agent", "Screening score 85");

        // Assert
        Assert.NotEqual(Guid.Empty, transition.Id);
        Assert.Equal(applicationId, transition.ApplicationId);
        Assert.Equal(fromStageId, transition.FromStageId);
        Assert.Equal("Applied", transition.FromStageName);
        Assert.Equal(toStageId, transition.ToStageId);
        Assert.Equal("Screening", transition.ToStageName);
        Assert.Equal(StageTransitionKind.Move, transition.Kind);
        Assert.Equal(StageTransitionActorKind.System, transition.ActorKind);
        Assert.Null(transition.ActorUserId);
        Assert.Equal("AI Screening Agent", transition.ActorDisplayLabel);
        Assert.Equal("Screening score 85", transition.Note);
        Assert.True(transition.OccurredAtUtc <= DateTime.UtcNow);
    }

    [Fact]
    public void CreateSystemMove_WithNullNote_LeavesNoteNull()
    {
        // Arrange & Act
        var transition = StageTransition.CreateSystemMove(
            Guid.NewGuid(), Guid.NewGuid(), "Applied", Guid.NewGuid(), "Screening", "AI Screening Agent", null);

        // Assert
        Assert.Null(transition.Note);
    }

    [Fact]
    public void CreateSystemMove_WithEmptyApplicationId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => StageTransition.CreateSystemMove(
            Guid.Empty, Guid.NewGuid(), "Applied", Guid.NewGuid(), "Screening", "AI Screening Agent", null));
    }

    [Fact]
    public void CreateSystemMove_WithEmptyFromStageId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => StageTransition.CreateSystemMove(
            Guid.NewGuid(), Guid.Empty, "Applied", Guid.NewGuid(), "Screening", "AI Screening Agent", null));
    }

    [Fact]
    public void CreateSystemMove_WithEmptyToStageId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => StageTransition.CreateSystemMove(
            Guid.NewGuid(), Guid.NewGuid(), "Applied", Guid.Empty, "Screening", "AI Screening Agent", null));
    }

    [Fact]
    public void CreateSystemMove_WithEmptyFromStageName_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => StageTransition.CreateSystemMove(
            Guid.NewGuid(), Guid.NewGuid(), " ", Guid.NewGuid(), "Screening", "AI Screening Agent", null));
    }

    [Fact]
    public void CreateSystemMove_WithEmptyToStageName_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => StageTransition.CreateSystemMove(
            Guid.NewGuid(), Guid.NewGuid(), "Applied", Guid.NewGuid(), " ", "AI Screening Agent", null));
    }

    [Fact]
    public void CreateSystemMove_WithEmptyActorDisplayLabel_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => StageTransition.CreateSystemMove(
            Guid.NewGuid(), Guid.NewGuid(), "Applied", Guid.NewGuid(), "Screening", " ", null));
    }
}
