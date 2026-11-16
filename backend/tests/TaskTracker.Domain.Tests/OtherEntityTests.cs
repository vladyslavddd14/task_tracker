using FluentAssertions;
using TaskTracker.Domain;
using TaskTracker.Domain.Entities;
using TaskTracker.Domain.Enums;

namespace TaskTracker.Domain.Tests;

public class OtherEntityTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public void Tag_NormalizesNameForCaseInsensitiveUniqueness()
    {
        var tag = Tag.Create(UserId, "  Work ", "#aabbcc");

        tag.Name.Should().Be("Work");
        tag.NormalizedName.Should().Be("work");
        tag.Color.Should().Be("#aabbcc");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Tag_RejectsEmptyName(string name)
    {
        var act = () => Tag.Create(UserId, name, null);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Tag_RejectsNameLongerThan30()
    {
        var act = () => Tag.Create(UserId, new string('t', 31), null);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void CustomField_RejectsNameLongerThan40()
    {
        var act = () => CustomFieldDefinition.Create(UserId, new string('f', 41), CustomFieldType.Text, 0);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void User_NormalizesEmailToLowerCase()
    {
        var user = User.Create("  Anna@Example.COM ", "hash", "Anna", "Europe/Kyiv", ProfileTemplate.Universal,
            DateTime.UtcNow);

        user.Email.Should().Be("anna@example.com");
    }

    [Fact]
    public void Subtask_RejectsEmptyTitleOnUpdate()
    {
        var task = TaskItem.Create(UserId, "Task", TaskItemStatus.Todo, TaskPriority.Low, 1, DateTime.UtcNow);
        var subtask = task.AddSubtask("Step", DateTime.UtcNow);

        var act = () => subtask.Update(" ", false);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Universal_Template_HasNoDefaults()
    {
        var template = ProfileTemplateCatalog.Get(ProfileTemplate.Universal);

        template.Tags.Should().BeEmpty();
        template.Fields.Should().BeEmpty();
    }

    [Fact]
    public void Student_Template_HasExpectedTagsAndFields()
    {
        var template = ProfileTemplateCatalog.Get(ProfileTemplate.Student);

        template.Tags.Select(t => t.Name).Should().BeEquivalentTo("лекція", "лабораторна", "екзамен");
        template.Fields.Should().Contain(f => f.Name == "Предмет" && f.Type == CustomFieldType.Text);
        template.Fields.Should().Contain(f => f.Name == "Оцінка" && f.Type == CustomFieldType.Number);
    }

    [Fact]
    public void Freelancer_Template_HasCheckboxField()
    {
        var template = ProfileTemplateCatalog.Get(ProfileTemplate.Freelancer);

        template.Tags.Select(t => t.Name).Should().BeEquivalentTo("клієнт", "рахунок");
        template.Fields.Should().Contain(f => f.Name == "Виставлено рахунок" && f.Type == CustomFieldType.Checkbox);
    }

    [Fact]
    public void Training_Template_HasNumberFields()
    {
        var template = ProfileTemplateCatalog.Get(ProfileTemplate.Training);

        template.Tags.Select(t => t.Name).Should().BeEquivalentTo("біг", "зал");
        template.Fields.Should().OnlyContain(f => f.Type == CustomFieldType.Number);
    }
}
