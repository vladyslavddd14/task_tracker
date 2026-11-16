using System.Globalization;
using TaskTracker.Domain;
using TaskTracker.Domain.Enums;

namespace TaskTracker.Application.Services;

/// <summary>Checks a custom field value against the field type and converts it to its stored text form.</summary>
public static class CustomFieldValueRules
{
    /// <summary>
    /// Returns true when the value is valid. A blank value normalizes to null, which clears the field.
    /// </summary>
    public static bool TryNormalize(CustomFieldType type, string? raw, out string? normalized, out string? error)
    {
        normalized = null;
        error = null;

        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        var text = raw.Trim();
        switch (type)
        {
            case CustomFieldType.Text:
                if (text.Length > DomainLimits.CustomFieldValueMax)
                {
                    error = $"Text must be at most {DomainLimits.CustomFieldValueMax} characters.";
                    return false;
                }

                normalized = text;
                return true;

            case CustomFieldType.Number:
                if (!decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                {
                    error = "Value must be a number.";
                    return false;
                }

                normalized = number.ToString(CultureInfo.InvariantCulture);
                return true;

            case CustomFieldType.Checkbox:
                if (!bool.TryParse(text, out var flag))
                {
                    error = "Value must be true or false.";
                    return false;
                }

                normalized = flag ? "true" : "false";
                return true;

            case CustomFieldType.Date:
                if (!DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None,
                        out var date))
                {
                    error = "Value must be a date in the format yyyy-MM-dd.";
                    return false;
                }

                normalized = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                return true;

            default:
                error = "Unknown field type.";
                return false;
        }
    }
}
