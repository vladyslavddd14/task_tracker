using FluentValidation;
using FluentValidation.Results;

namespace TaskTracker.Application.Common;

/// <summary>The resource does not exist or belongs to another user (mapped to 404).</summary>
public class NotFoundException(string message) : Exception(message);

/// <summary>The request conflicts with existing data, e.g. a duplicate email or tag (mapped to 409).</summary>
public class ConflictException(string message) : Exception(message);

/// <summary>Authentication failed (mapped to 401).</summary>
public class UnauthorizedException(string message) : Exception(message);

public record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

public static class Fail
{
    /// <summary>Throws a validation error for a single field (mapped to 400).</summary>
    public static Exception Validation(string field, string message) =>
        new ValidationException([new ValidationFailure(field, message)]);
}

public static class UserTime
{
    public static bool IsValidTimeZone(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return false;
        }

        return TryFind(timeZoneId) is not null;
    }

    /// <summary>Calendar date "today" in the user's time zone (falls back to UTC for an unknown zone).</summary>
    public static DateOnly Today(string timeZoneId, DateTime utcNow)
    {
        var zone = TryFind(timeZoneId) ?? TimeZoneInfo.Utc;
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utcNow, zone));
    }

    /// <summary>
    /// Older time zone databases (for example on Windows) only know the former name of some zones.
    /// </summary>
    private static readonly Dictionary<string, string> LegacyAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Europe/Kyiv"] = "Europe/Kiev"
    };

    private static TimeZoneInfo? TryFind(string id)
    {
        var zone = TryFindExact(id);
        if (zone is null && LegacyAliases.TryGetValue(id, out var legacyId))
        {
            zone = TryFindExact(legacyId);
        }

        return zone;
    }

    private static TimeZoneInfo? TryFindExact(string id)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return null;
        }
    }
}
