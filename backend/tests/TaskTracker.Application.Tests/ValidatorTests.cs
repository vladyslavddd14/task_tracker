using FluentAssertions;
using FluentValidation.TestHelper;
using TaskTracker.Application.Common;
using TaskTracker.Application.Dtos;
using TaskTracker.Application.Filtering;
using TaskTracker.Application.Services;
using TaskTracker.Application.Validators;
using TaskTracker.Domain.Enums;

namespace TaskTracker.Application.Tests;

public class ValidatorTests
{
    // ----- Registration -----

    private static RegisterRequest Register(string email = "anna@example.com", string password = "password123",
        string name = "Anna", string? zone = "Europe/Kyiv") => new(email, password, name, zone, null);

    private readonly RegisterRequestValidator _register = new();

    [Fact]
    public void Register_AcceptsValidRequest() => _register.TestValidate(Register()).ShouldNotHaveAnyValidationErrors();

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public void Register_RejectsBadEmail(string email) =>
        _register.TestValidate(Register(email: email)).ShouldHaveValidationErrorFor(x => x.Email);

    [Fact]
    public void Register_RejectsEmailLongerThan254() =>
        _register.TestValidate(Register(email: new string('a', 250) + "@x.io"))
            .ShouldHaveValidationErrorFor(x => x.Email);

    [Fact]
    public void Register_RejectsShortPassword() =>
        _register.TestValidate(Register(password: "1234567")).ShouldHaveValidationErrorFor(x => x.Password);

    [Fact]
    public void Register_AcceptsPasswordOfExactlyEightCharacters() =>
        _register.TestValidate(Register(password: "12345678")).ShouldNotHaveValidationErrorFor(x => x.Password);

    [Fact]
    public void Register_RejectsPasswordLongerThan100() =>
        _register.TestValidate(Register(password: new string('p', 101))).ShouldHaveValidationErrorFor(x => x.Password);

    [Fact]
    public void Register_RejectsUnknownTimeZone() =>
        _register.TestValidate(Register(zone: "Mars/Olympus")).ShouldHaveValidationErrorFor(x => x.TimeZoneId!);

    [Fact]
    public void Register_RejectsEmptyDisplayName() =>
        _register.TestValidate(Register(name: " ")).ShouldHaveValidationErrorFor(x => x.DisplayName);

    // ----- Tasks -----

    private static CreateTaskRequest CreateTask(string title) => new(title, null, null, null, null, null, null, null);

    private readonly CreateTaskRequestValidator _createTask = new();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void CreateTask_RejectsEmptyTitle(string? title) =>
        _createTask.TestValidate(CreateTask(title!)).ShouldHaveValidationErrorFor(x => x.Title);

    [Fact]
    public void CreateTask_RejectsTitleOver200() =>
        _createTask.TestValidate(CreateTask(new string('a', 201))).ShouldHaveValidationErrorFor(x => x.Title);

    [Fact]
    public void CreateTask_AcceptsTitleOf200() =>
        _createTask.TestValidate(CreateTask(new string('a', 200))).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void CreateTask_RejectsLongDescriptionAndNotes()
    {
        var request = new CreateTaskRequest("x", new string('d', 2001), new string('n', 20_001), null, null, null,
            null, null);

        var result = _createTask.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Description);
        result.ShouldHaveValidationErrorFor(x => x.NotesMarkdown);
    }

    [Fact]
    public void UpdateTask_RejectsEmptyTitle()
    {
        var request = new UpdateTaskRequest(" ", null, null, TaskItemStatus.Todo, TaskPriority.Low, null, null, null);

        new UpdateTaskRequestValidator().TestValidate(request).ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void Subtask_RejectsEmptyTitle() =>
        new SubtaskRequestValidator().TestValidate(new SubtaskRequest(" ", false))
            .ShouldHaveValidationErrorFor(x => x.Title);

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Move_RejectsNonFinitePosition(double position) =>
        new MoveTaskRequestValidator().TestValidate(new MoveTaskRequest(TaskItemStatus.Todo, position))
            .ShouldHaveValidationErrorFor(x => x.Position);

    // ----- Query -----

    private readonly TaskQueryValidator _query = new();

    [Theory]
    [InlineData(1, true)]
    [InlineData(200, true)]
    [InlineData(0, false)]
    [InlineData(201, false)]
    [InlineData(-5, false)]
    public void Query_PageSizeMustBeBetween1And200(int pageSize, bool valid)
    {
        var result = _query.TestValidate(new TaskQuery { PageSize = pageSize });

        if (valid)
        {
            result.ShouldNotHaveValidationErrorFor(x => x.PageSize);
        }
        else
        {
            result.ShouldHaveValidationErrorFor(x => x.PageSize);
        }
    }

    [Fact]
    public void Query_PageMustBePositive() =>
        _query.TestValidate(new TaskQuery { Page = 0 }).ShouldHaveValidationErrorFor(x => x.Page);

    [Fact]
    public void Query_RangeMustNotBeReversed() =>
        _query.TestValidate(new TaskQuery
        {
            DeadlineFrom = new DateOnly(2026, 11, 10), DeadlineTo = new DateOnly(2026, 11, 1)
        }).ShouldHaveValidationErrorFor(x => x.DeadlineTo);

    [Fact]
    public void Calendar_RequiresFromAndTo()
    {
        var result = new CalendarQueryValidator().TestValidate(new CalendarQuery());

        result.ShouldHaveValidationErrorFor(x => x.From);
        result.ShouldHaveValidationErrorFor(x => x.To);
    }

    [Fact]
    public void Calendar_RejectsTooWideRange() =>
        new CalendarQueryValidator().TestValidate(new CalendarQuery
        {
            From = new DateOnly(2026, 1, 1), To = new DateOnly(2027, 1, 1)
        }).ShouldHaveValidationErrorFor(x => x.To);

    [Fact]
    public void Calendar_AcceptsAMonth() =>
        new CalendarQueryValidator().TestValidate(new CalendarQuery
        {
            From = new DateOnly(2026, 11, 1), To = new DateOnly(2026, 11, 30)
        }).ShouldNotHaveAnyValidationErrors();

    // ----- Tags and custom fields -----

    private readonly TagRequestValidator _tag = new();

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Tag_RejectsEmptyName(string name) =>
        _tag.TestValidate(new TagRequest(name, null)).ShouldHaveValidationErrorFor(x => x.Name);

    [Fact]
    public void Tag_RejectsNameOver30() =>
        _tag.TestValidate(new TagRequest(new string('t', 31), null)).ShouldHaveValidationErrorFor(x => x.Name);

    [Theory]
    [InlineData("#aabbcc", true)]
    [InlineData("#AABBCC", true)]
    [InlineData("red", false)]
    [InlineData("#abc", false)]
    public void Tag_ColorMustBeHex(string color, bool valid)
    {
        var result = _tag.TestValidate(new TagRequest("tag", color));

        if (valid)
        {
            result.ShouldNotHaveValidationErrorFor(x => x.Color);
        }
        else
        {
            result.ShouldHaveValidationErrorFor(x => x.Color);
        }
    }

    [Fact]
    public void CustomField_NameLengthIsLimitedTo40()
    {
        var validator = new CustomFieldRequestValidator();

        validator.TestValidate(new CustomFieldRequest(new string('f', 40), CustomFieldType.Text, null))
            .ShouldNotHaveAnyValidationErrors();
        validator.TestValidate(new CustomFieldRequest(new string('f', 41), CustomFieldType.Text, null))
            .ShouldHaveValidationErrorFor(x => x.Name);
        validator.TestValidate(new CustomFieldRequest("", CustomFieldType.Text, null))
            .ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void ChangePassword_NewPasswordMustBeAtLeast8() =>
        new ChangePasswordRequestValidator().TestValidate(new ChangePasswordRequest("old", "short"))
            .ShouldHaveValidationErrorFor(x => x.NewPassword);
}

public class CustomFieldValueRulesTests
{
    [Theory]
    [InlineData(CustomFieldType.Text, "  hello ", "hello")]
    [InlineData(CustomFieldType.Number, "12.50", "12.50")]
    [InlineData(CustomFieldType.Number, "-3", "-3")]
    [InlineData(CustomFieldType.Checkbox, "TRUE", "true")]
    [InlineData(CustomFieldType.Checkbox, "false", "false")]
    [InlineData(CustomFieldType.Date, "2026-11-05", "2026-11-05")]
    public void ValidValues_AreNormalized(CustomFieldType type, string raw, string expected)
    {
        CustomFieldValueRules.TryNormalize(type, raw, out var normalized, out var error).Should().BeTrue();

        normalized.Should().Be(expected);
        error.Should().BeNull();
    }

    [Theory]
    [InlineData(CustomFieldType.Number, "abc")]
    [InlineData(CustomFieldType.Number, "1,5")]
    [InlineData(CustomFieldType.Checkbox, "yes")]
    [InlineData(CustomFieldType.Date, "05.11.2026")]
    [InlineData(CustomFieldType.Date, "2026-13-40")]
    public void WrongType_IsRejected(CustomFieldType type, string raw)
    {
        CustomFieldValueRules.TryNormalize(type, raw, out _, out var error).Should().BeFalse();

        error.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData(CustomFieldType.Text, null)]
    [InlineData(CustomFieldType.Number, "")]
    [InlineData(CustomFieldType.Date, "   ")]
    public void BlankValue_ClearsTheField(CustomFieldType type, string? raw)
    {
        CustomFieldValueRules.TryNormalize(type, raw, out var normalized, out _).Should().BeTrue();

        normalized.Should().BeNull();
    }

    [Fact]
    public void Text_LongerThanLimit_IsRejected() =>
        CustomFieldValueRules.TryNormalize(CustomFieldType.Text, new string('x', 1001), out _, out _)
            .Should().BeFalse();
}

public class UserTimeTests
{
    [Fact]
    public void Today_DependsOnTheUserTimeZone()
    {
        // 22:30 UTC on 5 Nov is already 6 Nov in Kyiv (UTC+2) but still 5 Nov in UTC.
        var utcNow = new DateTime(2026, 11, 5, 22, 30, 0, DateTimeKind.Utc);

        UserTime.Today("UTC", utcNow).Should().Be(new DateOnly(2026, 11, 5));
        UserTime.Today("Europe/Kyiv", utcNow).Should().Be(new DateOnly(2026, 11, 6));
    }

    [Fact]
    public void Today_FallsBackToUtcForUnknownZone()
    {
        var utcNow = new DateTime(2026, 11, 5, 22, 30, 0, DateTimeKind.Utc);

        UserTime.Today("Not/AZone", utcNow).Should().Be(new DateOnly(2026, 11, 5));
    }
}
