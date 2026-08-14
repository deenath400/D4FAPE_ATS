namespace Ats.Db.Configurations;

using Ats.Db.Applications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class ScreeningReportConfiguration : IEntityTypeConfiguration<ScreeningReport>
{
    public void Configure(EntityTypeBuilder<ScreeningReport> builder)
    {
        builder.ToTable("ScreeningReports");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.ApplicationId).IsRequired();
        builder.Property(s => s.Score).IsRequired();
        builder.Property(s => s.Recommendation)
            .IsRequired()
            .HasConversion<string>();
        builder.Property(s => s.Summary).IsRequired();
        builder.Property(s => s.Strengths).IsRequired();
        builder.Property(s => s.Concerns).IsRequired();
        builder.Property(s => s.Status)
            .IsRequired()
            .HasConversion<string>();
        builder.Property(s => s.FailureReason).IsRequired(false);
        builder.Property(s => s.EvaluatedAtUtc).IsRequired();
        builder.Property(s => s.SkillsScore).IsRequired(false);
        builder.Property(s => s.ExperienceScore).IsRequired(false);
        builder.Property(s => s.EducationScore).IsRequired(false);

        builder.HasIndex(s => s.ApplicationId).IsUnique();

        builder.HasOne<Application>()
            .WithOne(a => a.ScreeningReport)
            .HasForeignKey<ScreeningReport>(s => s.ApplicationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
