namespace Ats.UnitTests.Pipeline;

using System;
using Ats.Db.Requisitions;
using Xunit;

public class StageEntityTests
{
    [Fact]
    public void Stage_Create_SetsNormalizedNameAndSortOrder()
    {
        // Arrange
        var requisitionId = Guid.NewGuid();

        // Act
        var stage = Stage.Create(requisitionId, "Applied", 2);

        // Assert
        Assert.NotEqual(Guid.Empty, stage.Id);
        Assert.Equal(requisitionId, stage.RequisitionId);
        Assert.Equal("Applied", stage.Name);
        Assert.Equal("APPLIED", stage.NormalizedName);
        Assert.Equal(2, stage.SortOrder);
    }

    [Fact]
    public void Stage_Create_WithEmptyRequisitionId_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentException>(() => Stage.Create(Guid.Empty, "Applied", 0));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Stage_Create_WithInvalidName_ThrowsArgumentException(string? invalidName)
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentException>(() => Stage.Create(Guid.NewGuid(), invalidName!, 0));
    }

    [Fact]
    public void Stage_Rename_RecomputesNormalizedName()
    {
        // Arrange
        var stage = Stage.Create(Guid.NewGuid(), "Screening", 1);

        // Act
        stage.Rename("Phone Screen");

        // Assert
        Assert.Equal("Phone Screen", stage.Name);
        Assert.Equal("PHONE SCREEN", stage.NormalizedName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Stage_Rename_WithInvalidName_ThrowsArgumentException(string? invalidName)
    {
        // Arrange
        var stage = Stage.Create(Guid.NewGuid(), "Applied", 0);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => stage.Rename(invalidName!));
    }

    [Fact]
    public void Stage_ChangeSortOrder_UpdatesSortOrder()
    {
        // Arrange
        var stage = Stage.Create(Guid.NewGuid(), "Applied", 0);

        // Act
        stage.ChangeSortOrder(3);

        // Assert
        Assert.Equal(3, stage.SortOrder);
    }

    [Fact]
    public void Stage_DefaultStageNames_HasFourNamesInPipelineOrder()
    {
        // Assert — FR-5's seed list; RequisitionService.CreateAsync and the migration's raw-SQL
        // backfill must both stay in sync with this constant (R-2).
        Assert.Equal(new[] { "Applied", "Screening", "Interview", "Offer" }, Stage.DefaultStageNames);
    }
}
