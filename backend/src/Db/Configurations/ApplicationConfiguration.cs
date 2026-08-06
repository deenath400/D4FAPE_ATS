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
        builder.Property(a => a.CurrentStageId).IsRequired().IsConcurrencyToken();
        builder.Property(a => a.IsRejected).IsRequired().HasDefaultValue(false).IsConcurrencyToken();

        builder.HasIndex(a => new { a.CandidateId, a.RequisitionId }).IsUnique();
        builder.HasIndex(a => a.RequisitionId);
        builder.HasIndex(a => new { a.RequisitionId, a.CurrentStageId });

        builder.HasOne<Ats.Db.Requisitions.Requisition>()
            .WithMany()
            .HasForeignKey(a => a.RequisitionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Ats.Shared.Auth.ApplicationUser>()
            .WithMany()
            .HasForeignKey(a => a.CandidateId)
            .OnDelete(DeleteBehavior.Cascade);

        // DB-level backstop for FR-4's occupied-Stage removal guard: a Stage that is any
        // Application's current Stage can never be deleted (erd.md §3.2).
        builder.HasOne<Ats.Db.Requisitions.Stage>()
            .WithMany()
            .HasForeignKey(a => a.CurrentStageId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.CvAttachment)
            .WithOne()
            .HasForeignKey<CvAttachment>(c => c.ApplicationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
