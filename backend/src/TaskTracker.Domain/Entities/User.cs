using TaskTracker.Domain.Enums;

namespace TaskTracker.Domain.Entities;

public class User
{
    private User()
    {
    }

    public Guid Id { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string TimeZoneId { get; private set; } = "UTC";
    public ProfileTemplate ProfileTemplate { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public static User Create(string email, string passwordHash, string displayName, string timeZoneId,
        ProfileTemplate template, DateTime now)
    {
        return new User
        {
            Id = Guid.NewGuid(),
            Email = Guard.RequiredText(NormalizeEmail(email), DomainLimits.EmailMax, "Email"),
            PasswordHash = passwordHash,
            DisplayName = Guard.RequiredText(displayName, DomainLimits.DisplayNameMax, "Display name"),
            TimeZoneId = Guard.RequiredText(timeZoneId, 100, "Time zone"),
            ProfileTemplate = template,
            CreatedAt = now
        };
    }

    public void UpdateProfile(string displayName, string timeZoneId)
    {
        DisplayName = Guard.RequiredText(displayName, DomainLimits.DisplayNameMax, "Display name");
        TimeZoneId = Guard.RequiredText(timeZoneId, 100, "Time zone");
    }

    public void SetProfileTemplate(ProfileTemplate template) => ProfileTemplate = template;

    public void ChangePasswordHash(string passwordHash) => PasswordHash = passwordHash;
}
