namespace Ats.Service.Common;

using System.Collections.Generic;

/// <summary>
/// Generic pagination envelope. Established by spec 0003 as the project's first collection
/// endpoint response shape; mirrors <c>api.md</c> §4 <c>Paged&lt;T&gt;</c> exactly.
/// </summary>
public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; }
    public int Page { get; }
    public int PageSize { get; }
    public int Total { get; }

    public PagedResult(IReadOnlyList<T> items, int page, int pageSize, int total)
    {
        Items = items;
        Page = page;
        PageSize = pageSize;
        Total = total;
    }
}
