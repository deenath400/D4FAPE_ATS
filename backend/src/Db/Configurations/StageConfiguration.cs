namespace Ats.Db.Configurations;

using Ats.Db.Requisitions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class StageConfiguration : IEntityTypeConfiguration<Stage>
{
    public void Configure(EntityTypeBuilder<Stage> builder)
    {
        builder.ToTable("Stages");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Name).IsRequired().HasMaxLength(200);
        builder.Property(s => s.NormalizedName).IsRequired().HasMaxLength(200);
        builder.Property(s => s.SortOrder).IsRequired().HasDefaultValue(0);

        builder.HasIndex(s => s.RequisitionId);
        builder.HasIndex(s => new { s.RequisitionId, s.NormalizedName }).IsUnique();
    }
}
