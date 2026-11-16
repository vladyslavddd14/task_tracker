using FluentAssertions;
using TaskTracker.Application.Filtering;
using TaskTracker.Domain.Entities;
using TaskTracker.Domain.Enums;

namespace TaskTracker.Application.Tests;

/// <summary>The filter rules are plain LINQ, so they are tested over an in-memory list (no database).</summary>
public class TaskFiltersTests
{
    private static readonly DateTime Now = new(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = new(2026, 11, 5);
    private static readonly Guid UserId = Guid.NewGuid();

    private readonly Tag _work = Tag.Create(UserId, "work", null);
    private readonly Tag _home = Tag.Create(UserId, "home", null);
    private readonly Guid _gradeFieldId = Guid.NewGuid();

    private TaskItem Make(string title, TaskItemStatus status = TaskItemStatus.Todo,
        TaskPriority priority = TaskPriority.Medium, int? deadlineOffset = null, string? description = null,
        string? notes = null, Tag? tag = null, DateTime? created = null)
    {
        var task = TaskItem.Create(UserId, title, TaskItemStatus.Todo, priority, 1000, created ?? Now);
        task.Update(title, description, notes, priority, deadlineOffset is null ? null : Today.AddDays(deadlineOffset.Value),
            created ?? Now);
        task.ChangeStatus(status, Now);
        if (tag is not null)
        {
            task.SetTags([tag]);
        }

        return task;
    }

    private static List<string> Titles(IEnumerable<TaskItem> tasks, TaskQuery query) =>
        tasks.AsQueryable().Apply(query, Today).ApplyOrder(query).Select(t => t.Title).ToList();

    private List<TaskItem> Sample() =>
    [
        Make("Overdue work", deadlineOffset: -2, priority: TaskPriority.High, tag: _work),
        Make("Overdue home", deadlineOffset: -1, priority: TaskPriority.Low, tag: _home),
        Make("Overdue but done", TaskItemStatus.Done, deadlineOffset: -3, tag: _work),
        Make("Due today", deadlineOffset: 0, priority: TaskPriority.Medium),
        Make("Upcoming", TaskItemStatus.InProgress, deadlineOffset: 5, tag: _work),
        Make("No deadline", priority: TaskPriority.Low)
    ];

    [Fact]
    public void NoFilters_ReturnsEverything()
    {
        Titles(Sample(), new TaskQuery()).Should().HaveCount(6);
    }

    [Fact]
    public void Status_SupportsSeveralValues()
    {
        var query = new TaskQuery { Status = [TaskItemStatus.Done, TaskItemStatus.InProgress] };

        Titles(Sample(), query).Should().BeEquivalentTo("Overdue but done", "Upcoming");
    }

    [Fact]
    public void Priority_FiltersByExactValue()
    {
        Titles(Sample(), new TaskQuery { Priority = TaskPriority.Low }).Should()
            .BeEquivalentTo("Overdue home", "No deadline");
    }

    [Fact]
    public void Tags_MatchTasksWithAnyOfTheTags()
    {
        var query = new TaskQuery { TagId = [_work.Id, _home.Id] };

        Titles(Sample(), query).Should().BeEquivalentTo("Overdue work", "Overdue home", "Overdue but done", "Upcoming");
    }

    [Fact]
    public void Deadline_Overdue_ExcludesDoneTasks()
    {
        Titles(Sample(), new TaskQuery { Deadline = DeadlineFilter.Overdue }).Should()
            .BeEquivalentTo("Overdue work", "Overdue home");
    }

    [Fact]
    public void Deadline_Today_Upcoming_None()
    {
        Titles(Sample(), new TaskQuery { Deadline = DeadlineFilter.Today }).Should().Equal("Due today");
        Titles(Sample(), new TaskQuery { Deadline = DeadlineFilter.Upcoming }).Should().Equal("Upcoming");
        Titles(Sample(), new TaskQuery { Deadline = DeadlineFilter.None }).Should().Equal("No deadline");
    }

    [Fact]
    public void DeadlineRange_IsInclusive()
    {
        var query = new TaskQuery { DeadlineFrom = Today.AddDays(-2), DeadlineTo = Today };

        Titles(Sample(), query).Should().BeEquivalentTo("Overdue work", "Overdue home", "Due today");
    }

    [Fact]
    public void Search_IsCaseInsensitive_AndCoversTitleDescriptionAndNotes()
    {
        var tasks = new List<TaskItem>
        {
            Make("Buy MILK"),
            Make("Plan", description: "ask about the Report"),
            Make("Other", notes: "## Meeting notes\n- prepare REPORT"),
            Make("Unrelated")
        };

        Titles(tasks, new TaskQuery { Q = "milk" }).Should().Equal("Buy MILK");
        Titles(tasks, new TaskQuery { Q = "report" }).Should().BeEquivalentTo("Plan", "Other");
    }

    [Fact]
    public void Search_WorksForCyrillicText()
    {
        var tasks = new List<TaskItem> { Make("Підготувати ЗВІТ"), Make("Купити молоко") };

        Titles(tasks, new TaskQuery { Q = "звіт" }).Should().Equal("Підготувати ЗВІТ");
    }

    [Fact]
    public void Filters_AreCombinedWithAnd_OverdueWorkSortedByPriority()
    {
        var tasks = new List<TaskItem>
        {
            Make("Low overdue work", priority: TaskPriority.Low, deadlineOffset: -5, tag: _work),
            Make("High overdue work", priority: TaskPriority.High, deadlineOffset: -1, tag: _work),
            Make("Medium overdue work", priority: TaskPriority.Medium, deadlineOffset: -3, tag: _work),
            Make("High overdue home", priority: TaskPriority.High, deadlineOffset: -1, tag: _home),
            Make("High upcoming work", priority: TaskPriority.High, deadlineOffset: 4, tag: _work)
        };
        var query = new TaskQuery
        {
            TagId = [_work.Id], Deadline = DeadlineFilter.Overdue, Sort = TaskSortField.Priority
        };

        Titles(tasks, query).Should().Equal("High overdue work", "Medium overdue work", "Low overdue work");
    }

    [Fact]
    public void CustomFieldValue_FiltersByFieldAndText()
    {
        var withGrade = Make("Graded");
        withGrade.SetCustomFieldValues([(_gradeFieldId, "Excellent")]);
        var otherField = Make("Other field");
        otherField.SetCustomFieldValues([(Guid.NewGuid(), "Excellent")]);
        var tasks = new List<TaskItem> { withGrade, otherField, Make("Nothing") };

        var query = new TaskQuery { CustomFieldId = _gradeFieldId, CustomFieldValue = "excel" };

        Titles(tasks, query).Should().Equal("Graded");
    }

    // ----- Sorting -----

    [Fact]
    public void Sort_ByPriority_DefaultsToHighFirst()
    {
        var tasks = new List<TaskItem>
        {
            Make("L", priority: TaskPriority.Low), Make("H", priority: TaskPriority.High),
            Make("M", priority: TaskPriority.Medium)
        };

        Titles(tasks, new TaskQuery()).Should().Equal("H", "M", "L");
        Titles(tasks, new TaskQuery { Dir = SortDirection.Asc }).Should().Equal("L", "M", "H");
    }

    [Fact]
    public void Sort_ByDeadline_PutsTasksWithoutDeadlineLast_InBothDirections()
    {
        var tasks = new List<TaskItem>
        {
            Make("none"), Make("later", deadlineOffset: 5), Make("sooner", deadlineOffset: 1)
        };

        Titles(tasks, new TaskQuery { Sort = TaskSortField.Deadline }).Should().Equal("sooner", "later", "none");
        Titles(tasks, new TaskQuery { Sort = TaskSortField.Deadline, Dir = SortDirection.Desc }).Should()
            .Equal("later", "sooner", "none");
    }

    [Fact]
    public void Sort_ByTitle_IgnoresCase()
    {
        var tasks = new List<TaskItem> { Make("banana"), Make("Apple"), Make("cherry") };

        Titles(tasks, new TaskQuery { Sort = TaskSortField.Title }).Should().Equal("Apple", "banana", "cherry");
    }

    [Fact]
    public void Sort_ByCreatedAt_DefaultsToNewestFirst()
    {
        var tasks = new List<TaskItem>
        {
            Make("old", created: Now.AddDays(-5)), Make("new", created: Now), Make("mid", created: Now.AddDays(-2))
        };

        Titles(tasks, new TaskQuery { Sort = TaskSortField.CreatedAt }).Should().Equal("new", "mid", "old");
    }

    [Fact]
    public void WithDeadlineRange_NarrowsAnExistingRange()
    {
        var query = new TaskQuery { DeadlineFrom = Today.AddDays(2), DeadlineTo = Today.AddDays(100) };

        var narrowed = query.WithDeadlineRange(Today, Today.AddDays(30));

        narrowed.DeadlineFrom.Should().Be(Today.AddDays(2));
        narrowed.DeadlineTo.Should().Be(Today.AddDays(30));
    }
}
