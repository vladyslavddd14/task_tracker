namespace TaskTracker.Domain.Entities;

public class Subtask
{
    private Subtask()
    {
    }

    internal Subtask(Guid taskItemId, string title, double position, DateTime now)
    {
        Id = Guid.NewGuid();
        TaskItemId = taskItemId;
        Title = Guard.RequiredText(title, DomainLimits.TitleMax, "Subtask title");
        Position = position;
        CreatedAt = now;
    }

    public Guid Id { get; private set; }
    public Guid TaskItemId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public bool IsDone { get; private set; }
    public double Position { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public void Update(string title, bool isDone)
    {
        Title = Guard.RequiredText(title, DomainLimits.TitleMax, "Subtask title");
        IsDone = isDone;
    }

    public void SetPosition(double position) => Position = position;
}
