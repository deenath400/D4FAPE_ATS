namespace Ats.Db.Configurations;

using Ats.Db.Applications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class CvAttachmentConfiguration : IEntityTypeConfiguration<CvAttachment>
{
    public void Configure(EntityTypeBuilder<CvAttachment> builder)
    {
        builder.ToTable("CvAttachments");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.StorageKey).IsRequired().HasMaxLength(300);
        builder.Property(c => c.OriginalFileName).IsRequired().HasMaxLength(260);
        builder.Property(c => c.ContentType).IsRequired().HasMaxLength(100);
        builder.Property(c => c.SizeBytes).IsRequired();
        builder.Property(c => c.UploadedAtUtc).IsRequired();

        builder.HasIndex(c => c.ApplicationId).IsUnique();
    }
}
