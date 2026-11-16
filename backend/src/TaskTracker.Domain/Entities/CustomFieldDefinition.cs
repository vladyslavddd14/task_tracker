using TaskTracker.Domain.Enums;

namespace TaskTracker.Domain.Entities;

public class CustomFieldDefinition
{
    private CustomFieldDefinition()
    {
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Name { get; private set; } = string.Empty;

    /// <summary>Lower-cased name used for the case-insensitive unique index.</summary>
    public string NormalizedName { get; private set; } = string.Empty;

    /// <summary>The type is fixed at creation so stored values never change meaning.</summary>
    public CustomFieldType Type { get; private set; }

    public int Position { get; private set; }

    public static string Normalize(string name) => name.Trim().ToLowerInvariant();

    public static CustomFieldDefinition Create(Guid userId, string name, CustomFieldType type, int position)
    {
        var field = new CustomFieldDefinition { Id = Guid.NewGuid(), UserId = userId, Type = type };
        field.Update(name, position);
        return field;
    }

    public void Update(string name, int position)
    {
        Name = Guard.RequiredText(name, DomainLimits.CustomFieldNameMax, "Field name");
        NormalizedName = Normalize(Name);
        Position = position;
    }
}
