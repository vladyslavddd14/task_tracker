using TaskTracker.Domain.Enums;

namespace TaskTracker.Application.Filtering;

public enum DeadlineFilter
{
    Overdue,
    Today,
    Upcoming,
    None
}

public enum TaskSortField
{
    Priority,
    Deadline,
    CreatedAt,
    Title
}

public enum SortDirection
{
    Asc,
    Desc
}

/// <summary>Filters, sorting and paging of GET /api/tasks. All filters are combined with AND.</summary>
public class TaskQuery
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 200;

    public List<TaskItemStatus> Status { get; set; } = new();
    public TaskPriority? Priority { get; set; }

    /// <summary>A task matches when it has at least one of the given tags.</summary>
    public List<Guid> TagId { get; set; } = new();

    public DeadlineFilter? Deadline { get; set; }
    public DateOnly? DeadlineFrom { get; set; }
    public DateOnly? DeadlineTo { get; set; }
    public string? Q { get; set; }
    public Guid? CustomFieldId { get; set; }
    public string? CustomFieldValue { get; set; }
    public TaskSortField Sort { get; set; } = TaskSortField.Priority;
    public SortDirection? Dir { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = DefaultPageSize;

    /// <summary>Priority and creation date sort newest/highest first by default.</summary>
    public SortDirection EffectiveDirection => Dir ?? Sort switch
    {
        TaskSortField.Priority => SortDirection.Desc,
        TaskSortField.CreatedAt => SortDirection.Desc,
        _ => SortDirection.Asc
    };

    /// <summary>Copy of the filters with the deadline range narrowed to [from, to].</summary>
    public TaskQuery WithDeadlineRange(DateOnly from, DateOnly to) => new()
    {
        Status = Status,
        Priority = Priority,
        TagId = TagId,
        Deadline = Deadline,
        DeadlineFrom = DeadlineFrom is { } f && f > from ? f : from,
        DeadlineTo = DeadlineTo is { } t && t < to ? t : to,
        Q = Q,
        CustomFieldId = CustomFieldId,
        CustomFieldValue = CustomFieldValue,
        Sort = Sort,
        Dir = Dir,
        Page = Page,
        PageSize = PageSize
    };
}

/// <summary>Same filters as the list plus the visible date range of the calendar.</summary>
public class CalendarQuery : TaskQuery
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}
