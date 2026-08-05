namespace Ats.Db.Requisitions;

using System;
using System.Collections.Generic;

public class Requisition
{
    private readonly List<Stage> _stages = new();

    public Guid Id { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public RequisitionStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public IReadOnlyCollection<Stage> Stages => _stages.AsReadOnly();

    private Requisition() { } // EF Core

    public static Requisition Create(string title, string description)
    {
        var now = DateTime.UtcNow;
        return new Requisition
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = description,
            Status = RequisitionStatus.Draft,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    public void UpdateContent(string title, string description)
    {
        Title = title;
        Description = description;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Publish() => Transition(RequisitionStatus.Published);
    public void Unpublish() => Transition(RequisitionStatus.Draft);
    public void Close() => Transition(RequisitionStatus.Closed);

    private void Transition(RequisitionStatus target)
    {
        Status = target;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
