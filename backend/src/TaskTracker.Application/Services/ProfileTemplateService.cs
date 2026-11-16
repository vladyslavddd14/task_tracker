using TaskTracker.Application.Abstractions;
using TaskTracker.Domain;
using TaskTracker.Domain.Entities;
using TaskTracker.Domain.Enums;

namespace TaskTracker.Application.Services;

/// <summary>Creates the default tags and custom fields of a profile template.</summary>
public class ProfileTemplateService(ITagRepository tags, ICustomFieldRepository fields)
{
    /// <summary>Adds missing tags and fields. Existing data is kept; duplicates by name are skipped.</summary>
    public async Task ApplyAsync(User user, ProfileTemplate template, CancellationToken ct = default)
    {
        var definition = ProfileTemplateCatalog.Get(template);
        if (definition.Tags.Count == 0 && definition.Fields.Count == 0)
        {
            return;
        }

        var existingTags = (await tags.GetAllAsync(user.Id, ct)).Select(t => t.NormalizedName).ToHashSet();
        foreach (var tag in definition.Tags.Where(t => !existingTags.Contains(Tag.Normalize(t.Name))))
        {
            tags.Add(Tag.Create(user.Id, tag.Name, tag.Color));
        }

        var existingFields = await fields.GetAllAsync(user.Id, ct);
        var existingNames = existingFields.Select(f => f.NormalizedName).ToHashSet();
        var nextPosition = existingFields.Count == 0 ? 0 : existingFields.Max(f => f.Position) + 1;
        foreach (var field in definition.Fields.Where(f => !existingNames.Contains(CustomFieldDefinition.Normalize(f.Name))))
        {
            fields.Add(CustomFieldDefinition.Create(user.Id, field.Name, field.Type, nextPosition++));
        }
    }
}
