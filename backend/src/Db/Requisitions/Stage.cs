namespace Ats.Db.Requisitions;

using System;

public class Stage
{
    public Guid Id { get; private set; }
    public Guid RequisitionId { get; private set; }
    public string Name { get; private set; } = string.Empty;

    private Stage() { } // EF Core

    public static Stage Create(Guid requisitionId, string name)
    {
        if (requisitionId == Guid.Empty)
        {
            throw new ArgumentException("RequisitionId cannot be empty.", nameof(requisitionId));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name cannot be empty.", nameof(name));
        }

        return new Stage { Id = Guid.NewGuid(), RequisitionId = requisitionId, Name = name };
    }
}
