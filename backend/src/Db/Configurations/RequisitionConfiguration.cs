namespace Ats.Db.Configurations;

using Ats.Db.Requisitions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class RequisitionConfiguration : IEntityTypeConfiguration<Requisition>
{
    public void Configure(EntityTypeBuilder<Requisition> builder)
    {
        builder.ToTable("Requisitions");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Title).IsRequired().HasMaxLength(200);
        builder.Property(r => r.Description).IsRequired();
        builder.Property(r => r.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasDefaultValue(RequisitionStatus.Draft);

        builder.HasIndex(r => r.Status);

        builder.HasMany(r => r.Stages)
            .WithOne()
            .HasForeignKey(s => s.RequisitionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata.FindNavigation(nameof(Requisition.Stages))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
