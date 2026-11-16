using TaskTracker.Domain.Enums;

namespace TaskTracker.Domain.Entities;

public class TaskItem
{
    private readonly List<Subtask> _subtasks = new();
    private readonly List<TaskTag> _taskTags = new();
    private readonly List<StatusTransition> _transitions = new();
    private readonly List<CustomFieldValue> _customFieldValues = new();

    private TaskItem()
    {
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string? NotesMarkdown { get; private set; }
    public TaskItemStatus Status { get; private set; }
    public TaskPriority Priority { get; private set; }

    /// <summary>Calendar date without time, so time zones can never shift it by a day.</summary>
    public DateOnly? Deadline { get; private set; }

    /// <summary>Order inside a kanban column. Fractional, so a card can be dropped between two others.</summary>
    public double Position { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    public IReadOnlyCollection<Subtask> Subtasks => _subtasks;
    public IReadOnlyCollection<TaskTag> TaskTags => _taskTags;
    public IReadOnlyCollection<StatusTransition> Transitions => _transitions;
    public IReadOnlyCollection<CustomFieldValue> CustomFieldValues => _customFieldValues;

    public int SubtasksTotal => _subtasks.Count;
    public int SubtasksDone => _subtasks.Count(s => s.IsDone);

    public static TaskItem Create(Guid userId, string title, TaskItemStatus status, TaskPriority priority,
        double position, DateTime now)
    {
        return new TaskItem
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = Guard.RequiredText(title, DomainLimits.TitleMax, "Title"),
            Status = status,
            Priority = priority,
            Position = position,
            CreatedAt = now,
            UpdatedAt = now,
            CompletedAt = status == TaskItemStatus.Done ? now : null
        };
    }

    public void Update(string title, string? description, string? notesMarkdown, TaskPriority priority,
        DateOnly? deadline, DateTime now)
    {
        Title = Guard.RequiredText(title, DomainLimits.TitleMax, "Title");
        Description = Guard.OptionalText(description, DomainLimits.DescriptionMax, "Description");
        NotesMarkdown = Guard.OptionalText(notesMarkdown, DomainLimits.NotesMax, "Notes");
        Priority = priority;
        Deadline = deadline;
        UpdatedAt = now;
    }

    /// <summary>Any status can follow any other; every real change is recorded as a transition.</summary>
    public void ChangeStatus(TaskItemStatus newStatus, DateTime now)
    {
        if (newStatus == Status)
        {
            return;
        }

        _transitions.Add(new StatusTransition(Id, Status, newStatus, now));
        Status = newStatus;
        CompletedAt = newStatus == TaskItemStatus.Done ? now : null;
        UpdatedAt = now;
    }

    public void SetPosition(double position) => Position = position;

    public void Touch(DateTime now) => UpdatedAt = now;

    /// <summary>A task is overdue when its deadline is before today and it is not done.</summary>
    public bool IsOverdue(DateOnly today) =>
        Deadline.HasValue && Deadline.Value < today && Status != TaskItemStatus.Done;

    public Subtask AddSubtask(string title, DateTime now)
    {
        if (_subtasks.Count >= DomainLimits.MaxSubtasksPerTask)
        {
            throw new DomainException($"A task can have at most {DomainLimits.MaxSubtasksPerTask} subtasks.");
        }

        var position = _subtasks.Count == 0 ? 0 : _subtasks.Max(s => s.Position) + 1;
        var subtask = new Subtask(Id, title, position, now);
        _subtasks.Add(subtask);
        UpdatedAt = now;
        return subtask;
    }

    public void RemoveSubtask(Subtask subtask, DateTime now)
    {
        _subtasks.Remove(subtask);
        UpdatedAt = now;
    }

    /// <summary>Applies a new order. The id list must contain every subtask exactly once.</summary>
    public void ReorderSubtasks(IReadOnlyList<Guid> orderedIds, DateTime now)
    {
        var sameSet = orderedIds.Count == _subtasks.Count
                      && orderedIds.Distinct().Count() == orderedIds.Count
                      && _subtasks.All(s => orderedIds.Contains(s.Id));
        if (!sameSet)
        {
            throw new DomainException("The new order must contain every subtask exactly once.");
        }

        for (var i = 0; i < orderedIds.Count; i++)
        {
            _subtasks.First(s => s.Id == orderedIds[i]).SetPosition(i);
        }

        UpdatedAt = now;
    }

    /// <summary>Replaces the set of tags on the task.</summary>
    public void SetTags(IReadOnlyCollection<Tag> tags)
    {
        _taskTags.RemoveAll(link => tags.All(t => t.Id != link.TagId));
        foreach (var tag in tags.Where(t => _taskTags.All(link => link.TagId != t.Id)))
        {
            _taskTags.Add(new TaskTag(Id, tag));
        }
    }

    /// <summary>Upserts the given values. A null value removes the field value from the task.</summary>
    public void SetCustomFieldValues(IEnumerable<(Guid DefinitionId, string? Value)> values)
    {
        foreach (var (definitionId, value) in values)
        {
            var existing = _customFieldValues.FirstOrDefault(v => v.CustomFieldDefinitionId == definitionId);
            if (value is null)
            {
                if (existing is not null)
                {
                    _customFieldValues.Remove(existing);
                }
            }
            else if (existing is null)
            {
                _customFieldValues.Add(new CustomFieldValue(Id, definitionId, value));
            }
            else
            {
                existing.SetValue(value);
            }
        }
    }
}
