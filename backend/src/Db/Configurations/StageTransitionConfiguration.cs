namespace Ats.Db.Configurations;

using Ats.Db.Pipeline;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class StageTransitionConfiguration : IEntityTypeConfiguration<StageTransition>
{
    public void Configure(EntityTypeBuilder<StageTransition> builder)
    {
        builder.ToTable("StageTransitions");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.FromStageName).IsRequired().HasMaxLength(200);
        builder.Property(t => t.ToStageName).HasMaxLength(200);
        builder.Property(t => t.Kind).IsRequired().HasConversion<string>();
        builder.Property(t => t.ActorKind).IsRequired().HasConversion<string>();
        builder.Property(t => t.ActorDisplayLabel).IsRequired().HasMaxLength(200);
        builder.Property(t => t.Note).HasMaxLength(2000);
        builder.Property(t => t.OccurredAtUtc).IsRequired();

        builder.HasIndex(t => new { t.ApplicationId, t.OccurredAtUtc });

        builder.HasOne<Ats.Db.Applications.Application>()
            .WithMany()
            .HasForeignKey(t => t.ApplicationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Ats.Db.Requisitions.Stage>()
            .WithMany()
            .HasForeignKey(t => t.FromStageId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<Ats.Db.Requisitions.Stage>()
            .WithMany()
            .HasForeignKey(t => t.ToStageId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<Ats.Shared.Auth.ApplicationUser>()
            .WithMany()
            .HasForeignKey(t => t.ActorUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
