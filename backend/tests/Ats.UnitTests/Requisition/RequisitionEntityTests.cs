namespace Ats.UnitTests.Requisition;

using System;
using Ats.Db.Requisitions;
using Xunit;

public class RequisitionEntityTests
{
    [Fact]
    public void Requisition_Create_StartsInDraft()
    {
        // Arrange
        var title = "Senior Engineer";
        var description = "Build things.";

        // Act
        var requisition = Ats.Db.Requisitions.Requisition.Create(title, description);

        // Assert
        Assert.NotEqual(Guid.Empty, requisition.Id);
        Assert.Equal(title, requisition.Title);
        Assert.Equal(description, requisition.Description);
        Assert.Equal(RequisitionStatus.Draft, requisition.Status);
        Assert.Equal(requisition.CreatedAtUtc, requisition.UpdatedAtUtc);
        Assert.Empty(requisition.Stages);
    }

    [Fact]
    public void Requisition_UpdateContent_UpdatesFieldsAndTimestamp()
    {
        // Arrange
        var requisition = Ats.Db.Requisitions.Requisition.Create("Old Title", "Old Description");
        var originalUpdatedAt = requisition.UpdatedAtUtc;

        // Act
        requisition.UpdateContent("New Title", "New Description");

        // Assert
        Assert.Equal("New Title", requisition.Title);
        Assert.Equal("New Description", requisition.Description);
        Assert.True(requisition.UpdatedAtUtc >= originalUpdatedAt);
    }

    [Fact]
    public void Requisition_Publish_TransitionsToPublished()
    {
        // Arrange
        var requisition = Ats.Db.Requisitions.Requisition.Create("Title", "Description");

        // Act
        requisition.Publish();

        // Assert
        Assert.Equal(RequisitionStatus.Published, requisition.Status);
    }

    [Fact]
    public void Requisition_Unpublish_TransitionsToDraft()
    {
        // Arrange
        var requisition = Ats.Db.Requisitions.Requisition.Create("Title", "Description");
        requisition.Publish();

        // Act
        requisition.Unpublish();

        // Assert
        Assert.Equal(RequisitionStatus.Draft, requisition.Status);
    }

    [Fact]
    public void Requisition_Close_TransitionsToClosed()
    {
        // Arrange
        var requisition = Ats.Db.Requisitions.Requisition.Create("Title", "Description");
        requisition.Publish();

        // Act
        requisition.Close();

        // Assert
        Assert.Equal(RequisitionStatus.Closed, requisition.Status);
    }

    [Fact]
    public void Stage_Create_IsScopedToItsOwnRequisition()
    {
        // Arrange
        var requisitionA = Ats.Db.Requisitions.Requisition.Create("Requisition A", "Description A");
        var requisitionB = Ats.Db.Requisitions.Requisition.Create("Requisition B", "Description B");

        // Act
        var stageA = Stage.Create(requisitionA.Id, "Applied");
        var stageB = Stage.Create(requisitionB.Id, "Applied");

        // Assert
        Assert.Equal(requisitionA.Id, stageA.RequisitionId);
        Assert.Equal(requisitionB.Id, stageB.RequisitionId);
        Assert.NotEqual(stageA.Id, stageB.Id);
        Assert.NotEqual(stageA.RequisitionId, stageB.RequisitionId);
    }

    [Fact]
    public void Stage_Create_WithEmptyRequisitionId_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentException>(() => Stage.Create(Guid.Empty, "Applied"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Stage_Create_WithInvalidName_ThrowsArgumentException(string? invalidName)
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentException>(() => Stage.Create(Guid.NewGuid(), invalidName!));
    }
}
