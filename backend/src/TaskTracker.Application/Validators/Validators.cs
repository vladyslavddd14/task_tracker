using FluentValidation;
using TaskTracker.Application.Common;
using TaskTracker.Application.Dtos;
using TaskTracker.Application.Filtering;
using TaskTracker.Domain;

namespace TaskTracker.Application.Validators;

internal static class Rules
{
    public static IRuleBuilderOptions<T, string> ValidEmail<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(DomainLimits.EmailMax).EmailAddress();

    public static IRuleBuilderOptions<T, string> ValidPassword<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MinimumLength(DomainLimits.PasswordMin).MaximumLength(DomainLimits.PasswordMax);

    /// <summary>Title must contain something other than whitespace.</summary>
    public static IRuleBuilderOptions<T, string> ValidTitle<T>(this IRuleBuilder<T, string> rule) =>
        rule.Must(title => !string.IsNullOrWhiteSpace(title)).WithMessage("'{PropertyName}' must not be empty.")
            .Must(title => title is null || title.Trim().Length <= DomainLimits.TitleMax)
            .WithMessage($"'{{PropertyName}}' must be at most {DomainLimits.TitleMax} characters.");
}

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email).ValidEmail();
        RuleFor(x => x.Password).ValidPassword();
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(DomainLimits.DisplayNameMax);
        RuleFor(x => x.TimeZoneId!).Must(UserTime.IsValidTimeZone).When(x => x.TimeZoneId is not null)
            .WithMessage("'{PropertyName}' must be a valid IANA time zone.");
        RuleFor(x => x.ProfileTemplate!.Value).IsInEnum().When(x => x.ProfileTemplate is not null);
    }
}

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(DomainLimits.DisplayNameMax);
        RuleFor(x => x.TimeZoneId).Must(UserTime.IsValidTimeZone)
            .WithMessage("'{PropertyName}' must be a valid IANA time zone.");
    }
}

public class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.NewPassword).ValidPassword();
    }
}

public class ApplyTemplateRequestValidator : AbstractValidator<ApplyTemplateRequest>
{
    public ApplyTemplateRequestValidator()
    {
        RuleFor(x => x.Template).IsInEnum();
    }
}

public class CreateTaskRequestValidator : AbstractValidator<CreateTaskRequest>
{
    public CreateTaskRequestValidator()
    {
        RuleFor(x => x.Title).ValidTitle();
        RuleFor(x => x.Description).MaximumLength(DomainLimits.DescriptionMax);
        RuleFor(x => x.NotesMarkdown).MaximumLength(DomainLimits.NotesMax);
        RuleFor(x => x.Status!.Value).IsInEnum().When(x => x.Status is not null);
        RuleFor(x => x.Priority!.Value).IsInEnum().When(x => x.Priority is not null);
    }
}

public class UpdateTaskRequestValidator : AbstractValidator<UpdateTaskRequest>
{
    public UpdateTaskRequestValidator()
    {
        RuleFor(x => x.Title).ValidTitle();
        RuleFor(x => x.Description).MaximumLength(DomainLimits.DescriptionMax);
        RuleFor(x => x.NotesMarkdown).MaximumLength(DomainLimits.NotesMax);
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.Priority).IsInEnum();
    }
}

public class ChangeStatusRequestValidator : AbstractValidator<ChangeStatusRequest>
{
    public ChangeStatusRequestValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
    }
}

public class MoveTaskRequestValidator : AbstractValidator<MoveTaskRequest>
{
    public MoveTaskRequestValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.Position).Must(p => double.IsFinite(p) && Math.Abs(p) <= 1e12)
            .WithMessage("'{PropertyName}' must be a finite number.");
    }
}

public class TaskQueryValidator : AbstractValidator<TaskQuery>
{
    public TaskQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, TaskQuery.MaxPageSize);
        RuleFor(x => x.Q).MaximumLength(200);
        RuleFor(x => x.CustomFieldValue).MaximumLength(DomainLimits.CustomFieldValueMax);
        RuleFor(x => x.DeadlineTo).GreaterThanOrEqualTo(x => x.DeadlineFrom)
            .When(x => x.DeadlineFrom is not null && x.DeadlineTo is not null)
            .WithMessage("'deadlineTo' must not be before 'deadlineFrom'.");
    }
}

public class CalendarQueryValidator : AbstractValidator<CalendarQuery>
{
    public const int MaxRangeDays = 120;

    public CalendarQueryValidator()
    {
        Include(new TaskQueryValidator());
        RuleFor(x => x.From).NotNull();
        RuleFor(x => x.To).NotNull();
        RuleFor(x => x.To).Must((x, to) => to >= x.From)
            .When(x => x.From is not null && x.To is not null)
            .WithMessage("'to' must not be before 'from'.");
        RuleFor(x => x.To).Must((x, to) => to!.Value.DayNumber - x.From!.Value.DayNumber <= MaxRangeDays)
            .When(x => x.From is not null && x.To is not null)
            .WithMessage($"The range must not exceed {MaxRangeDays} days.");
    }
}

public class SubtaskRequestValidator : AbstractValidator<SubtaskRequest>
{
    public SubtaskRequestValidator()
    {
        RuleFor(x => x.Title).ValidTitle();
    }
}

public class ReorderSubtasksRequestValidator : AbstractValidator<ReorderSubtasksRequest>
{
    public ReorderSubtasksRequestValidator()
    {
        RuleFor(x => x.OrderedIds).NotNull().Must(ids => ids is null || ids.Count <= DomainLimits.MaxSubtasksPerTask);
    }
}

public class TagRequestValidator : AbstractValidator<TagRequest>
{
    public TagRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(DomainLimits.TagNameMax);
        RuleFor(x => x.Color).Matches("^#[0-9a-fA-F]{6}$").When(x => !string.IsNullOrWhiteSpace(x.Color))
            .WithMessage("'{PropertyName}' must be a hex color like #aabbcc.");
    }
}

public class CustomFieldRequestValidator : AbstractValidator<CustomFieldRequest>
{
    public CustomFieldRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(DomainLimits.CustomFieldNameMax);
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Position!.Value).GreaterThanOrEqualTo(0).When(x => x.Position is not null);
    }
}
