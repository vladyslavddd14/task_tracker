namespace TaskTracker.Domain.Entities;

/// <summary>Value of a custom field for one task. Stored as text and interpreted by the field type.</summary>
public class CustomFieldValue
{
    private CustomFieldValue()
    {
    }

    public CustomFieldValue(Guid taskItemId, Guid definitionId, string value)
    {
        Id = Guid.NewGuid();
        TaskItemId = taskItemId;
        CustomFieldDefinitionId = definitionId;
        Value = value;
    }

    public Guid Id { get; private set; }
    public Guid TaskItemId { get; private set; }
    public Guid CustomFieldDefinitionId { get; private set; }
    public string Value { get; private set; } = string.Empty;

    public void SetValue(string value) => Value = value;
}
