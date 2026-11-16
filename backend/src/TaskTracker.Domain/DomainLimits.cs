namespace TaskTracker.Domain;

/// <summary>Length limits shared by the domain rules and the validators.</summary>
public static class DomainLimits
{
    public const int TitleMax = 200;
    public const int DescriptionMax = 2000;
    public const int NotesMax = 20_000;
    public const int TagNameMax = 30;
    public const int CustomFieldNameMax = 40;
    public const int CustomFieldValueMax = 1000;
    public const int MaxSubtasksPerTask = 50;
    public const int EmailMax = 254;
    public const int PasswordMin = 8;
    public const int PasswordMax = 100;
    public const int DisplayNameMax = 100;
}
