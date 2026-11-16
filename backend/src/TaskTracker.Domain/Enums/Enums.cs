namespace TaskTracker.Domain.Enums;

public enum TaskItemStatus
{
    Todo = 0,
    InProgress = 1,
    Done = 2
}

public enum TaskPriority
{
    Low = 0,
    Medium = 1,
    High = 2
}

public enum CustomFieldType
{
    Text = 0,
    Number = 1,
    Checkbox = 2,
    Date = 3
}

public enum ProfileTemplate
{
    Universal = 0,
    Student = 1,
    Freelancer = 2,
    Training = 3
}
