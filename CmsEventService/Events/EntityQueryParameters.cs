using Microsoft.AspNetCore.Mvc;

namespace CmsEventService.Events;

public sealed class EntityQueryParameters
{
    private const int MaxPageSize = 100;
    private int _page = 1;
    private int _pageSize = 50;

    [FromQuery(Name = "page")]
    public int Page
    {
        get => _page;
        set => _page = value < 1 ? 1 : value;
    }

    [FromQuery(Name = "pageSize")]
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = Math.Clamp(value, 1, MaxPageSize);
    }
}
