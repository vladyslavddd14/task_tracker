namespace TaskTracker.Domain.Entities;

public class Tag
{
    private Tag()
    {
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Name { get; private set; } = string.Empty;

    /// <summary>Lower-cased name used for the case-insensitive unique index.</summary>
    public string NormalizedName { get; private set; } = string.Empty;

    public string? Color { get; private set; }

    public static string Normalize(string name) => name.Trim().ToLowerInvariant();

    public static Tag Create(Guid userId, string name, string? color)
    {
        var tag = new Tag { Id = Guid.NewGuid(), UserId = userId };
        tag.Update(name, color);
        return tag;
    }

    public void Update(string name, string? color)
    {
        Name = Guard.RequiredText(name, DomainLimits.TagNameMax, "Tag name");
        NormalizedName = Normalize(Name);
        Color = string.IsNullOrWhiteSpace(color) ? null : color.Trim();
    }
}
