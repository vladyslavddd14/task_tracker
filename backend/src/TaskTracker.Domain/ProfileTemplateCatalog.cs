using TaskTracker.Domain.Enums;

namespace TaskTracker.Domain;

public record TemplateTag(string Name, string Color);

public record TemplateField(string Name, CustomFieldType Type);

public record ProfileTemplateDefinition(IReadOnlyList<TemplateTag> Tags, IReadOnlyList<TemplateField> Fields);

/// <summary>Default tags and custom fields for each profile template (the personal twist of the app).</summary>
public static class ProfileTemplateCatalog
{
    private static readonly ProfileTemplateDefinition Empty = new([], []);

    public static ProfileTemplateDefinition Get(ProfileTemplate template) => template switch
    {
        ProfileTemplate.Student => new ProfileTemplateDefinition(
            [new("лекція", "#3b82f6"), new("лабораторна", "#8b5cf6"), new("екзамен", "#ef4444")],
            [new("Предмет", CustomFieldType.Text), new("Оцінка", CustomFieldType.Number)]),
        ProfileTemplate.Freelancer => new ProfileTemplateDefinition(
            [new("клієнт", "#10b981"), new("рахунок", "#f59e0b")],
            [
                new("Клієнт", CustomFieldType.Text),
                new("Ставка/год", CustomFieldType.Number),
                new("Виставлено рахунок", CustomFieldType.Checkbox)
            ]),
        ProfileTemplate.Training => new ProfileTemplateDefinition(
            [new("біг", "#06b6d4"), new("зал", "#f97316")],
            [new("Дистанція, км", CustomFieldType.Number), new("Тривалість, хв", CustomFieldType.Number)]),
        _ => Empty
    };
}
