namespace TaskTracker.Domain.Entities;

/// <summary>Many-to-many link between a task and a tag.</summary>
public class TaskTag
{
    private TaskTag()
    {
    }

    public TaskTag(Guid taskItemId, Tag tag)
    {
        TaskItemId = taskItemId;
        TagId = tag.Id;
        Tag = tag;
    }

    public Guid TaskItemId { get; private set; }
    public Guid TagId { get; private set; }
    public Tag Tag { get; private set; } = null!;
}
