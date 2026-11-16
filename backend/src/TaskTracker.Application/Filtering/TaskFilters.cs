using TaskTracker.Domain.Entities;
using TaskTracker.Domain.Enums;

namespace TaskTracker.Application.Filtering;

/// <summary>
/// Filter and sort rules as plain LINQ over IQueryable. EF Core translates them to SQL
/// and unit tests run the same code over an in-memory list.
/// </summary>
public static class TaskFilters
{
    public static IQueryable<TaskItem> Apply(this IQueryable<TaskItem> tasks, TaskQuery filter, DateOnly today)
    {
        if (filter.Status.Count > 0)
        {
            var statuses = filter.Status.ToList();
            tasks = tasks.Where(t => statuses.Contains(t.Status));
        }

        if (filter.Priority is { } priority)
        {
            tasks = tasks.Where(t => t.Priority == priority);
        }

        if (filter.TagId.Count > 0)
        {
            var tagIds = filter.TagId.ToList();
            tasks = tasks.Where(t => t.TaskTags.Any(link => tagIds.Contains(link.TagId)));
        }

        switch (filter.Deadline)
        {
            case DeadlineFilter.Overdue:
                tasks = tasks.Where(t => t.Deadline != null && t.Deadline < today && t.Status != TaskItemStatus.Done);
                break;
            case DeadlineFilter.Today:
                tasks = tasks.Where(t => t.Deadline == today);
                break;
            case DeadlineFilter.Upcoming:
                tasks = tasks.Where(t => t.Deadline > today);
                break;
            case DeadlineFilter.None:
                tasks = tasks.Where(t => t.Deadline == null);
                break;
        }

        if (filter.DeadlineFrom is { } from)
        {
            tasks = tasks.Where(t => t.Deadline >= from);
        }

        if (filter.DeadlineTo is { } to)
        {
            tasks = tasks.Where(t => t.Deadline <= to);
        }

        if (!string.IsNullOrWhiteSpace(filter.Q))
        {
            var text = filter.Q.Trim().ToLower();
            tasks = tasks.Where(t =>
                t.Title.ToLower().Contains(text)
                || (t.Description != null && t.Description.ToLower().Contains(text))
                || (t.NotesMarkdown != null && t.NotesMarkdown.ToLower().Contains(text)));
        }

        if (filter.CustomFieldId is { } fieldId && !string.IsNullOrWhiteSpace(filter.CustomFieldValue))
        {
            var value = filter.CustomFieldValue.Trim().ToLower();
            tasks = tasks.Where(t => t.CustomFieldValues.Any(v =>
                v.CustomFieldDefinitionId == fieldId && v.Value.ToLower().Contains(value)));
        }

        return tasks;
    }

    /// <summary>Applies the requested sort. Equal keys fall back to deadline, creation date and id.</summary>
    public static IQueryable<TaskItem> ApplyOrder(this IQueryable<TaskItem> tasks, TaskQuery filter)
    {
        var desc = filter.EffectiveDirection == SortDirection.Desc;

        IOrderedQueryable<TaskItem> ordered = filter.Sort switch
        {
            TaskSortField.Priority => desc
                ? tasks.OrderByDescending(t => t.Priority)
                : tasks.OrderBy(t => t.Priority),
            TaskSortField.Deadline => desc
                ? tasks.OrderBy(t => t.Deadline == null).ThenByDescending(t => t.Deadline)
                : tasks.OrderBy(t => t.Deadline == null).ThenBy(t => t.Deadline),
            TaskSortField.CreatedAt => desc
                ? tasks.OrderByDescending(t => t.CreatedAt)
                : tasks.OrderBy(t => t.CreatedAt),
            _ => desc
                ? tasks.OrderByDescending(t => t.Title.ToLower())
                : tasks.OrderBy(t => t.Title.ToLower())
        };

        return ordered
            .ThenBy(t => t.Deadline == null)
            .ThenBy(t => t.Deadline)
            .ThenByDescending(t => t.CreatedAt)
            .ThenBy(t => t.Id);
    }
}
