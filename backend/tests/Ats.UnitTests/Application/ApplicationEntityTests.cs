namespace Ats.UnitTests.Application;

using System;
using Ats.Db.Applications;
using Xunit;

public class ApplicationEntityTests
{
    [Fact]
    public void Application_Create_SetsFieldsAndStartsWithNoCv()
    {
        // Arrange
        var requisitionId = Guid.NewGuid();
        var candidateId = Guid.NewGuid();
        var currentStageId = Guid.NewGuid();

        // Act
        var application = Ats.Db.Applications.Application.Create(requisitionId, candidateId, currentStageId);

        // Assert
        Assert.NotEqual(Guid.Empty, application.Id);
        Assert.Equal(requisitionId, application.RequisitionId);
        Assert.Equal(candidateId, application.CandidateId);
        Assert.Equal(currentStageId, application.CurrentStageId);
        Assert.False(application.IsRejected);
        Assert.True(application.SubmittedAtUtc <= DateTime.UtcNow);
        Assert.Null(application.CvAttachment);
    }

    [Fact]
    public void Application_Create_WithEmptyRequisitionId_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentException>(
            () => Ats.Db.Applications.Application.Create(Guid.Empty, Guid.NewGuid(), Guid.NewGuid()));
    }

    [Fact]
    public void Application_Create_WithEmptyCandidateId_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentException>(
            () => Ats.Db.Applications.Application.Create(Guid.NewGuid(), Guid.Empty, Guid.NewGuid()));
    }

    [Fact]
    public void Application_Create_WithEmptyCurrentStageId_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentException>(
            () => Ats.Db.Applications.Application.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty));
    }

    [Fact]
    public void Application_MoveToStage_UpdatesCurrentStageId()
    {
        // Arrange
        var application = Ats.Db.Applications.Application.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var newStageId = Guid.NewGuid();

        // Act
        application.MoveToStage(newStageId);

        // Assert
        Assert.Equal(newStageId, application.CurrentStageId);
    }

    [Fact]
    public void Application_Reject_SetsIsRejectedKeepsCurrentStageId()
    {
        // Arrange
        var currentStageId = Guid.NewGuid();
        var application = Ats.Db.Applications.Application.Create(Guid.NewGuid(), Guid.NewGuid(), currentStageId);

        // Act
        application.Reject();

        // Assert
        Assert.True(application.IsRejected);
        Assert.Equal(currentStageId, application.CurrentStageId);
    }

    [Fact]
    public void Application_AttachCv_SetsCvAttachment()
    {
        // Arrange
        var application = Ats.Db.Applications.Application.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var cv = CvAttachment.Create(application.Id, "abc123.pdf", "resume.pdf", "application/pdf", 1024);

        // Act
        application.AttachCv(cv);

        // Assert
        Assert.Same(cv, application.CvAttachment);
    }

    [Fact]
    public void Application_AttachCv_CalledTwice_Throws()
    {
        // Arrange
        var application = Ats.Db.Applications.Application.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var firstCv = CvAttachment.Create(application.Id, "abc123.pdf", "resume.pdf", "application/pdf", 1024);
        var secondCv = CvAttachment.Create(application.Id, "def456.pdf", "resume2.pdf", "application/pdf", 2048);
        application.AttachCv(firstCv);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => application.AttachCv(secondCv));
    }

    [Fact]
    public void Application_AttachCv_WithNull_ThrowsArgumentNullException()
    {
        // Arrange
        var application = Ats.Db.Applications.Application.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => application.AttachCv(null!));
    }

    [Fact]
    public void CvAttachment_Create_SetsFields()
    {
        // Arrange
        var applicationId = Guid.NewGuid();

        // Act
        var cv = CvAttachment.Create(applicationId, "storagekey123.pdf", "resume.pdf", "application/pdf", 2048);

        // Assert
        Assert.NotEqual(Guid.Empty, cv.Id);
        Assert.Equal(applicationId, cv.ApplicationId);
        Assert.Equal("storagekey123.pdf", cv.StorageKey);
        Assert.Equal("resume.pdf", cv.OriginalFileName);
        Assert.Equal("application/pdf", cv.ContentType);
        Assert.Equal(2048, cv.SizeBytes);
        Assert.True(cv.UploadedAtUtc <= DateTime.UtcNow);
    }

    [Fact]
    public void CvAttachment_Create_WithEmptyApplicationId_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentException>(
            () => CvAttachment.Create(Guid.Empty, "storagekey.pdf", "resume.pdf", "application/pdf", 1024));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void CvAttachment_Create_WithInvalidStorageKey_ThrowsArgumentException(string? invalidStorageKey)
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentException>(
            () => CvAttachment.Create(Guid.NewGuid(), invalidStorageKey!, "resume.pdf", "application/pdf", 1024));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CvAttachment_Create_WithNonPositiveSize_Throws(long invalidSize)
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentException>(
            () => CvAttachment.Create(Guid.NewGuid(), "storagekey.pdf", "resume.pdf", "application/pdf", invalidSize));
    }
}
