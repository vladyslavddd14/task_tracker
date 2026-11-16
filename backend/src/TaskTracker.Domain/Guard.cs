namespace TaskTracker.Domain;

/// <summary>Small helpers that enforce text invariants.</summary>
internal static class Guard
{
    /// <summary>Trims the value and rejects empty or too long text.</summary>
    public static string RequiredText(string? value, int maxLength, string fieldName)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            throw new DomainException($"{fieldName} must not be empty.");
        }

        if (trimmed.Length > maxLength)
        {
            throw new DomainException($"{fieldName} must be at most {maxLength} characters.");
        }

        return trimmed;
    }

    /// <summary>Returns null for blank text and rejects text over the limit.</summary>
    public static string? OptionalText(string? value, int maxLength, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (value.Length > maxLength)
        {
            throw new DomainException($"{fieldName} must be at most {maxLength} characters.");
        }

        return value;
    }
}
