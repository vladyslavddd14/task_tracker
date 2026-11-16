using FluentAssertions;
using TaskTracker.Domain;
using TaskTracker.Domain.Entities;
using TaskTracker.Domain.Enums;

namespace TaskTracker.Domain.Tests;

public class TaskItemTests
{
    private static readonly DateTime Now = new(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid UserId = Guid.NewGuid();

    private static TaskItem NewTask(string title = "Write report", TaskItemStatus status = TaskItemStatus.Todo) =>
        TaskItem.Create(UserId, title, status, TaskPriority.Medium, 1000, Now);

    // ----- Title -----

    [Fact]
    public void Create_TrimsTitle()
    {
        NewTask("  Write report  ").Title.Should().Be("Write report");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_RejectsEmptyTitle(string? title)
    {
        var act = () => NewTask(title!);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_RejectsTitleLongerThan200()
    {
        var act = () => NewTask(new string('a', 201));

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_AcceptsTitleOfExactly200()
    {
        NewTask(new string('a', 200)).Title.Should().HaveLength(200);
    }

    [Fact]
    public void Update_RejectsEmptyTitle()
    {
        var task = NewTask();

        var act = () => task.Update("  ", null, null, TaskPriority.Low, null, Now);

        act.Should().Throw<DomainException>();
        task.Title.Should().Be("Write report");
    }

    [Fact]
    public void Update_RejectsTooLongDescriptionAndNotes()
    {
        var task = NewTask();

        task.Invoking(t => t.Update("x", new string('d', 2001), null, TaskPriority.Low, null, Now))
            .Should().Throw<DomainException>();
        task.Invoking(t => t.Update("x", null, new string('n', 20_001), TaskPriority.Low, null, Now))
            .Should().Throw<DomainException>();
    }

    [Fact]
    public void Update_TurnsBlankTextIntoNull_AndSetsAllFields()
    {
        var task = NewTask();
        var later = Now.AddHours(1);

        task.Update("New title", "  ", "# Notes", TaskPriority.High, new DateOnly(2026, 12, 1), later);

        task.Title.Should().Be("New title");
        task.Description.Should().BeNull();
        task.NotesMarkdown.Should().Be("# Notes");
        task.Priority.Should().Be(TaskPriority.High);
        task.Deadline.Should().Be(new DateOnly(2026, 12, 1));
        task.UpdatedAt.Should().Be(later);
    }

    // ----- Status -----

    [Fact]
    public void Create_SetsCreatedAt_AndDefaultsToNoCompletion()
    {
        var task = NewTask();

        task.CreatedAt.Should().Be(Now);
        task.CompletedAt.Should().BeNull();
        task.Transitions.Should().BeEmpty();
    }

    [Fact]
    public void ChangeStatus_ToDone_SetsCompletedAt()
    {
        var task = NewTask();
        var doneAt = Now.AddHours(2);

        task.ChangeStatus(TaskItemStatus.Done, doneAt);

        task.Status.Should().Be(TaskItemStatus.Done);
        task.CompletedAt.Should().Be(doneAt);
    }

    [Fact]
    public void ChangeStatus_LeavingDone_ClearsCompletedAt()
    {
        var task = NewTask(status: TaskItemStatus.Done);

        task.ChangeStatus(TaskItemStatus.InProgress, Now.AddHours(1));

        task.CompletedAt.Should().BeNull();
    }

    [Fact]
    public void Create_AsDone_SetsCompletedAt()
    {
        NewTask(status: TaskItemStatus.Done).CompletedAt.Should().Be(Now);
    }

    [Fact]
    public void ChangeStatus_ToSameStatus_RecordsNothing()
    {
        var task = NewTask();

        task.ChangeStatus(TaskItemStatus.Todo, Now.AddHours(1));

        task.Transitions.Should().BeEmpty();
        task.UpdatedAt.Should().Be(Now);
    }

    [Theory]
    [InlineData(TaskItemStatus.Todo, TaskItemStatus.InProgress)]
    [InlineData(TaskItemStatus.Todo, TaskItemStatus.Done)]
    [InlineData(TaskItemStatus.InProgress, TaskItemStatus.Todo)]
    [InlineData(TaskItemStatus.InProgress, TaskItemStatus.Done)]
    [InlineData(TaskItemStatus.Done, TaskItemStatus.Todo)]
    [InlineData(TaskItemStatus.Done, TaskItemStatus.InProgress)]
    public void ChangeStatus_AllowsAnyTransition_AndRecordsIt(TaskItemStatus from, TaskItemStatus to)
    {
        var task = NewTask(status: from);
        var changedAt = Now.AddMinutes(30);

        task.ChangeStatus(to, changedAt);

        task.Status.Should().Be(to);
        var transition = task.Transitions.Should().ContainSingle().Subject;
        transition.FromStatus.Should().Be(from);
        transition.ToStatus.Should().Be(to);
        transition.ChangedAt.Should().Be(changedAt);
    }

    // ----- Overdue -----

    private static readonly DateOnly Today = new(2026, 11, 5);

    [Fact]
    public void IsOverdue_TrueWhenDeadlineIsBeforeToday_AndNotDone()
    {
        var task = NewTask();
        task.Update("x", null, null, TaskPriority.Low, Today.AddDays(-1), Now);

        task.IsOverdue(Today).Should().BeTrue();
    }

    [Fact]
    public void IsOverdue_FalseWhenDone()
    {
        var task = NewTask(status: TaskItemStatus.Done);
        task.Update("x", null, null, TaskPriority.Low, Today.AddDays(-1), Now);

        task.IsOverdue(Today).Should().BeFalse();
    }

    [Fact]
    public void IsOverdue_FalseWhenDeadlineIsTodayInFutureOrMissing()
    {
        var task = NewTask();

        task.IsOverdue(Today).Should().BeFalse();
        task.Update("x", null, null, TaskPriority.Low, Today, Now);
        task.IsOverdue(Today).Should().BeFalse();
        task.Update("x", null, null, TaskPriority.Low, Today.AddDays(3), Now);
        task.IsOverdue(Today).Should().BeFalse();
    }

    // ----- Subtasks -----

    [Fact]
    public void Subtasks_ReportProgress()
    {
        var task = NewTask();
        var first = task.AddSubtask("One", Now);
        task.AddSubtask("Two", Now);
        first.Update("One", true);

        task.SubtasksTotal.Should().Be(2);
        task.SubtasksDone.Should().Be(1);
    }

    [Fact]
    public void AddSubtask_RejectsEmptyTitle()
    {
        var act = () => NewTask().AddSubtask("  ", Now);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void AddSubtask_AllowsAtMost50()
    {
        var task = NewTask();
        for (var i = 0; i < 50; i++)
        {
            task.AddSubtask($"Step {i}", Now);
        }

        var act = () => task.AddSubtask("One too many", Now);

        act.Should().Throw<DomainException>();
        task.SubtasksTotal.Should().Be(50);
    }

    [Fact]
    public void CompletingAllSubtasks_DoesNotChangeTaskStatus()
    {
        var task = NewTask();
        task.AddSubtask("One", Now).Update("One", true);

        task.Status.Should().Be(TaskItemStatus.Todo);
    }

    [Fact]
    public void ReorderSubtasks_AssignsNewPositions()
    {
        var task = NewTask();
        var a = task.AddSubtask("A", Now);
        var b = task.AddSubtask("B", Now);
        var c = task.AddSubtask("C", Now);

        task.ReorderSubtasks([c.Id, a.Id, b.Id], Now);

        task.Subtasks.OrderBy(s => s.Position).Select(s => s.Title).Should().Equal("C", "A", "B");
    }

    [Fact]
    public void ReorderSubtasks_RejectsIncompleteOrUnknownIds()
    {
        var task = NewTask();
        var a = task.AddSubtask("A", Now);
        task.AddSubtask("B", Now);

        task.Invoking(t => t.ReorderSubtasks([a.Id], Now)).Should().Throw<DomainException>();
        task.Invoking(t => t.ReorderSubtasks([a.Id, Guid.NewGuid()], Now)).Should().Throw<DomainException>();
        task.Invoking(t => t.ReorderSubtasks([a.Id, a.Id], Now)).Should().Throw<DomainException>();
    }

    // ----- Tags and custom fields -----

    [Fact]
    public void SetTags_ReplacesTheSetOfTags()
    {
        var task = NewTask();
        var work = Tag.Create(UserId, "work", null);
        var home = Tag.Create(UserId, "home", null);

        task.SetTags([work, home]);
        task.SetTags([home]);

        task.TaskTags.Select(l => l.TagId).Should().Equal(home.Id);
    }

    [Fact]
    public void SetCustomFieldValues_UpsertsAndRemoves()
    {
        var task = NewTask();
        var fieldId = Guid.NewGuid();

        task.SetCustomFieldValues([(fieldId, "first")]);
        task.SetCustomFieldValues([(fieldId, "second")]);
        task.CustomFieldValues.Should().ContainSingle().Which.Value.Should().Be("second");

        task.SetCustomFieldValues([(fieldId, null)]);
        task.CustomFieldValues.Should().BeEmpty();
    }
}
