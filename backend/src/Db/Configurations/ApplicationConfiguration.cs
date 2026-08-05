namespace Ats.Db.Configurations;

using Ats.Db.Applications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class ApplicationConfiguration : IEntityTypeConfiguration<Application>
{
    public void Configure(EntityTypeBuilder<Application> builder)
    {
        builder.ToTable("Applications");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.RequisitionId).IsRequired();
        builder.Property(a => a.CandidateId).IsRequired();
        builder.Property(a => a.SubmittedAtUtc).IsRequired();

        builder.HasIndex(a => new { a.CandidateId, a.RequisitionId }).IsUnique();
        builder.HasIndex(a => a.RequisitionId);

        builder.HasOne<Ats.Db.Requisitions.Requisition>()
            .WithMany()
            .HasForeignKey(a => a.RequisitionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Ats.Shared.Auth.ApplicationUser>()
            .WithMany()
            .HasForeignKey(a => a.CandidateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.CvAttachment)
            .WithOne()
            .HasForeignKey<CvAttachment>(c => c.ApplicationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
