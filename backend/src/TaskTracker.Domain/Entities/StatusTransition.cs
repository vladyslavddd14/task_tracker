using TaskTracker.Domain.Enums;

namespace TaskTracker.Domain.Entities;

/// <summary>Immutable record of one status change.</summary>
public class StatusTransition
{
    private StatusTransition()
    {
    }

    public StatusTransition(Guid taskItemId, TaskItemStatus from, TaskItemStatus to, DateTime changedAt)
    {
        Id = Guid.NewGuid();
        TaskItemId = taskItemId;
        FromStatus = from;
        ToStatus = to;
        ChangedAt = changedAt;
    }

    public Guid Id { get; private set; }
    public Guid TaskItemId { get; private set; }
    public TaskItemStatus FromStatus { get; private set; }
    public TaskItemStatus ToStatus { get; private set; }
    public DateTime ChangedAt { get; private set; }
}
