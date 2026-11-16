using TaskTracker.Domain.Entities;

namespace TaskTracker.Application.Dtos;

/// <summary>Plain entity-to-DTO mapping. No mapping library on purpose.</summary>
public static class Mapper
{
    public static UserDto ToDto(this User user) =>
        new(user.Id, user.Email, user.DisplayName, user.TimeZoneId, user.ProfileTemplate, user.CreatedAt);

    public static TagDto ToDto(this Tag tag) => new(tag.Id, tag.Name, tag.Color);

    public static CustomFieldDto ToDto(this CustomFieldDefinition field) =>
        new(field.Id, field.Name, field.Type, field.Position);

    public static SubtaskDto ToDto(this Subtask subtask) =>
        new(subtask.Id, subtask.Title, subtask.IsDone, subtask.Position);

    public static TaskListItemDto ToListItem(this TaskItem task, DateOnly today) =>
        new(task.Id, task.Title, task.Description, task.Status, task.Priority, task.Deadline, task.Position,
            task.CreatedAt, task.UpdatedAt, task.CompletedAt, task.IsOverdue(today),
            task.TaskTags.Select(link => link.Tag.ToDto()).OrderBy(t => t.Name).ToList(),
            task.SubtasksTotal, task.SubtasksDone);

    public static TaskDetailsDto ToDetails(this TaskItem task, DateOnly today) =>
        new(task.Id, task.Title, task.Description, task.NotesMarkdown, task.Status, task.Priority, task.Deadline,
            task.Position, task.CreatedAt, task.UpdatedAt, task.CompletedAt, task.IsOverdue(today),
            task.TaskTags.Select(link => link.Tag.ToDto()).OrderBy(t => t.Name).ToList(),
            task.Subtasks.OrderBy(s => s.Position).Select(s => s.ToDto()).ToList(),
            task.CustomFieldValues.Select(v => new CustomFieldValueDto(v.CustomFieldDefinitionId, v.Value)).ToList(),
            task.Transitions.OrderBy(t => t.ChangedAt)
                .Select(t => new StatusTransitionDto(t.FromStatus, t.ToStatus, t.ChangedAt)).ToList());
}
