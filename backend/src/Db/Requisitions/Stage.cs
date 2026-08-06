namespace Ats.Db.Requisitions;

using System;

public class Stage
{
    // FR-5's seed list, in pipeline order. The migration's raw-SQL backfill (erd.md §5) cannot
    // reference this constant and must be kept in sync by hand — verified by
    // PipelineMigrationBackfillTests (R-2, HLD §9).
    public static readonly string[] DefaultStageNames = { "Applied", "Screening", "Interview", "Offer" };

    public Guid Id { get; private set; }
    public Guid RequisitionId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public int SortOrder { get; private set; }

    private Stage() { } // EF Core

    public static Stage Create(Guid requisitionId, string name, int sortOrder)
    {
        if (requisitionId == Guid.Empty)
        {
            throw new ArgumentException("RequisitionId cannot be empty.", nameof(requisitionId));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name cannot be empty.", nameof(name));
        }

        return new Stage
        {
            Id = Guid.NewGuid(),
            RequisitionId = requisitionId,
            Name = name,
            NormalizedName = name.ToUpperInvariant(),
            SortOrder = sortOrder
        };
    }

    // NormalizedName is recomputed here and nowhere else — the two fields can never drift
    // because Rename is the only way to change either (FR-2, FR-24).
    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name cannot be empty.", nameof(name));
        }

        Name = name;
        NormalizedName = name.ToUpperInvariant();
    }

    // Unconditional, like Requisition.Publish()/Close() — PipelineService checks legality
    // (contiguity, uniqueness within a Requisition) before calling this (FR-3).
    public void ChangeSortOrder(int sortOrder) => SortOrder = sortOrder;
}
