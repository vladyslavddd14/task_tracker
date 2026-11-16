using System.Net;
using FluentAssertions;
using TaskTracker.Api.IntegrationTests.Infrastructure;
using TaskTracker.Application.Dtos;
using TaskTracker.Domain.Enums;
using Xunit;

namespace TaskTracker.Api.IntegrationTests;

[Collection("api")]
public class FilterTests(ApiFactory factory)
{
    private static string Date(int offsetDays) => TestData.UtcToday.AddDays(offsetDays).ToString("yyyy-MM-dd");

    private record Scenario(TestUser User, TagDto Work, TagDto Home);

    /// <summary>
    /// Pay rent (High, Todo, overdue, work) · Write ЗВІТ (Medium, InProgress, today, work) ·
    /// Gym (Low, Todo, tomorrow, home) · Read book (Medium, Done, overdue) ·
    /// Someday (Low, Todo, no deadline, notes mention Milk).
    /// </summary>
    private async Task<Scenario> SetUpAsync()
    {
        var user = await TestData.RegisterAsync(factory);
        var work = await user.CreateTagAsync("work");
        var home = await user.CreateTagAsync("home");

        await user.CreateTaskAsync("Pay rent", TaskItemStatus.Todo, TaskPriority.High, TestData.UtcToday.AddDays(-1),
            [work.Id]);
        await user.CreateTaskAsync("Write ЗВІТ", TaskItemStatus.InProgress, TaskPriority.Medium, TestData.UtcToday,
            [work.Id], description: "Квартальний звіт");
        await user.CreateTaskAsync("Gym", TaskItemStatus.Todo, TaskPriority.Low, TestData.UtcToday.AddDays(1),
            [home.Id]);
        await user.CreateTaskAsync("Read book", TaskItemStatus.Done, TaskPriority.Medium,
            TestData.UtcToday.AddDays(-1));
        await user.CreateTaskAsync("Someday", TaskItemStatus.Todo, TaskPriority.Low, notes: "buy Milk");
        return new Scenario(user, work, home);
    }

    [Fact]
    public async Task Status_SupportsOneOrSeveralValues()
    {
        var s = await SetUpAsync();

        (await s.User.TitlesAsync("?status=Done")).Should().Equal("Read book");
        (await s.User.TitlesAsync("?status=Done&status=InProgress")).Should()
            .BeEquivalentTo("Read book", "Write ЗВІТ");
    }

    [Fact]
    public async Task Priority_Filter()
    {
        var s = await SetUpAsync();

        (await s.User.TitlesAsync("?priority=Low")).Should().BeEquivalentTo("Gym", "Someday");
    }

    [Fact]
    public async Task Tag_Filter_SupportsSeveralTags()
    {
        var s = await SetUpAsync();

        (await s.User.TitlesAsync($"?tagId={s.Home.Id}")).Should().Equal("Gym");
        (await s.User.TitlesAsync($"?tagId={s.Work.Id}&tagId={s.Home.Id}")).Should()
            .BeEquivalentTo("Pay rent", "Write ЗВІТ", "Gym");
    }

    [Fact]
    public async Task Deadline_Overdue_ExcludesDoneTasks()
    {
        var s = await SetUpAsync();

        (await s.User.TitlesAsync("?deadline=overdue")).Should().Equal("Pay rent");
    }

    [Fact]
    public async Task Deadline_TodayUpcomingAndNone()
    {
        var s = await SetUpAsync();

        (await s.User.TitlesAsync("?deadline=today")).Should().Equal("Write ЗВІТ");
        (await s.User.TitlesAsync("?deadline=upcoming")).Should().Equal("Gym");
        (await s.User.TitlesAsync("?deadline=none")).Should().Equal("Someday");
    }

    [Fact]
    public async Task Deadline_Range_IsInclusive()
    {
        var s = await SetUpAsync();

        var titles = await s.User.TitlesAsync($"?deadlineFrom={Date(0)}&deadlineTo={Date(1)}");

        titles.Should().BeEquivalentTo("Write ЗВІТ", "Gym");
    }

    [Fact]
    public async Task Search_IsCaseInsensitive_AndCoversTitleDescriptionAndNotes()
    {
        var s = await SetUpAsync();

        (await s.User.TitlesAsync("?q=PAY")).Should().Equal("Pay rent");
        (await s.User.TitlesAsync("?q=milk")).Should().Equal("Someday");
    }

    [Fact]
    public async Task Search_IsCaseInsensitiveForCyrillic_InTitleAndDescription()
    {
        var s = await SetUpAsync();

        (await s.User.TitlesAsync("?q=" + Uri.EscapeDataString("зВіТ"))).Should().Equal("Write ЗВІТ");
        (await s.User.TitlesAsync("?q=" + Uri.EscapeDataString("КВАРТАЛЬНИЙ"))).Should().Equal("Write ЗВІТ");
    }

    [Fact]
    public async Task Search_TreatsPercentAndUnderscoreAsPlainText()
    {
        var s = await SetUpAsync();

        (await s.User.TitlesAsync("?q=%25")).Should().BeEmpty();
        (await s.User.TitlesAsync("?q=_")).Should().BeEmpty();
    }

    [Fact]
    public async Task Filters_AreCombinedWithAnd_OverdueWorkTasksSortedByPriority()
    {
        var user = await TestData.RegisterAsync(factory);
        var work = await user.CreateTagAsync("work");
        var home = await user.CreateTagAsync("home");
        var overdue = TestData.UtcToday.AddDays(-2);
        await user.CreateTaskAsync("low", priority: TaskPriority.Low, deadline: overdue, tagIds: [work.Id]);
        await user.CreateTaskAsync("high", priority: TaskPriority.High, deadline: overdue, tagIds: [work.Id]);
        await user.CreateTaskAsync("medium", priority: TaskPriority.Medium, deadline: overdue, tagIds: [work.Id]);
        await user.CreateTaskAsync("home overdue", priority: TaskPriority.High, deadline: overdue, tagIds: [home.Id]);
        await user.CreateTaskAsync("future work", priority: TaskPriority.High, deadline: TestData.UtcToday.AddDays(3),
            tagIds: [work.Id]);
        await user.CreateTaskAsync("done work", TaskItemStatus.Done, TaskPriority.High, overdue, [work.Id]);

        var titles = await user.TitlesAsync($"?deadline=overdue&tagId={work.Id}&sort=priority");

        titles.Should().Equal("high", "medium", "low");
    }

    [Fact]
    public async Task Sorting_ByTitleDeadlineAndCreatedAt()
    {
        var user = await TestData.RegisterAsync(factory);
        await user.CreateTaskAsync("banana", deadline: TestData.UtcToday.AddDays(5));
        await user.CreateTaskAsync("Apple");
        await user.CreateTaskAsync("cherry", deadline: TestData.UtcToday.AddDays(1));

        (await user.TitlesAsync("?sort=title")).Should().Equal("Apple", "banana", "cherry");
        (await user.TitlesAsync("?sort=title&dir=desc")).Should().Equal("cherry", "banana", "Apple");
        (await user.TitlesAsync("?sort=deadline")).Should().Equal("cherry", "banana", "Apple");
        (await user.TitlesAsync("?sort=createdAt")).Should().Equal("cherry", "Apple", "banana");
        (await user.TitlesAsync("?sort=createdAt&dir=asc")).Should().Equal("banana", "Apple", "cherry");
    }

    [Fact]
    public async Task DefaultSort_IsPriorityHighFirst()
    {
        var user = await TestData.RegisterAsync(factory);
        await user.CreateTaskAsync("low", priority: TaskPriority.Low);
        await user.CreateTaskAsync("high", priority: TaskPriority.High);
        await user.CreateTaskAsync("medium", priority: TaskPriority.Medium);

        (await user.TitlesAsync()).Should().Equal("high", "medium", "low");
    }

    [Fact]
    public async Task Paging_ReturnsPagesAndTotal()
    {
        var user = await TestData.RegisterAsync(factory);
        for (var i = 1; i <= 5; i++)
        {
            await user.CreateTaskAsync($"task {i}");
        }

        var second = await user.ListAsync("?sort=title&pageSize=2&page=2");

        second.Total.Should().Be(5);
        second.Page.Should().Be(2);
        second.PageSize.Should().Be(2);
        second.Items.Select(t => t.Title).Should().Equal("task 3", "task 4");
        (await user.ListAsync()).PageSize.Should().Be(50);
    }

    [Theory]
    [InlineData("?pageSize=0")]
    [InlineData("?pageSize=201")]
    [InlineData("?page=0")]
    [InlineData("?deadline=someday")]
    [InlineData("?status=Finished")]
    [InlineData("?sort=colour")]
    public async Task InvalidQuery_Returns400(string query)
    {
        var user = await TestData.RegisterAsync(factory);

        var response = await user.Client.GetAsync("/api/tasks" + query);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PageSize200_IsAllowed()
    {
        var user = await TestData.RegisterAsync(factory);

        (await user.Client.GetAsync("/api/tasks?pageSize=200")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ListItems_ExposeOverdueFlagTagsAndSubtaskProgress()
    {
        var user = await TestData.RegisterAsync(factory);
        var tag = await user.CreateTagAsync("work");
        var task = await user.CreateTaskAsync("Late", deadline: TestData.UtcToday.AddDays(-3), tagIds: [tag.Id]);
        var created = await (await user.Client.PostJson($"/api/tasks/{task.Id}/subtasks", new SubtaskRequest("a", false)))
            .Read<SubtaskDto>();
        await user.Client.PostJson($"/api/tasks/{task.Id}/subtasks", new SubtaskRequest("b", false));
        await user.Client.PutJson($"/api/tasks/{task.Id}/subtasks/{created.Id}", new SubtaskRequest("a", true));

        var item = (await user.ListAsync()).Items.Single();

        item.IsOverdue.Should().BeTrue();
        item.Tags.Should().ContainSingle(t => t.Name == "work");
        item.SubtasksTotal.Should().Be(2);
        item.SubtasksDone.Should().Be(1);
    }

    [Fact]
    public async Task CustomFieldFilter_FindsTasksByFieldValue()
    {
        var user = await TestData.RegisterAsync(factory);
        var invoiced = await user.CreateFieldAsync("Invoiced", CustomFieldType.Checkbox);
        var client = await user.CreateFieldAsync("Client", CustomFieldType.Text);
        await user.CreateTaskAsync("Paid job",
            customFields: [new(invoiced.Id, "true"), new(client.Id, "Acme Corp")]);
        await user.CreateTaskAsync("Open job", customFields: [new(invoiced.Id, "false"), new(client.Id, "Globex")]);
        await user.CreateTaskAsync("No fields");

        (await user.TitlesAsync($"?customFieldId={invoiced.Id}&customFieldValue=true")).Should().Equal("Paid job");
        (await user.TitlesAsync($"?customFieldId={client.Id}&customFieldValue=acme")).Should().Equal("Paid job");
    }

    // ----- "Today" view -----

    [Fact]
    public async Task Today_ShowsUnfinishedTasksDueTodayOrOverdue_MostImportantFirst()
    {
        var user = await TestData.RegisterAsync(factory);
        await user.CreateTaskAsync("overdue low", priority: TaskPriority.Low, deadline: TestData.UtcToday.AddDays(-5));
        await user.CreateTaskAsync("today high", priority: TaskPriority.High, deadline: TestData.UtcToday);
        await user.CreateTaskAsync("overdue medium", priority: TaskPriority.Medium,
            deadline: TestData.UtcToday.AddDays(-1));
        await user.CreateTaskAsync("tomorrow", deadline: TestData.UtcToday.AddDays(1));
        await user.CreateTaskAsync("no deadline");
        await user.CreateTaskAsync("done overdue", TaskItemStatus.Done, deadline: TestData.UtcToday.AddDays(-2));

        var today = await (await user.Client.GetAsync("/api/tasks/today")).Read<List<TaskListItemDto>>();

        today.Select(t => t.Title).Should().Equal("today high", "overdue medium", "overdue low");
        today.Where(t => t.IsOverdue).Select(t => t.Title).Should().BeEquivalentTo("overdue medium", "overdue low");
    }

    [Fact]
    public async Task Today_UsesTheTimeZoneOfTheUser()
    {
        // UTC+14 is a calendar day ahead of UTC for ten hours of every day.
        const string zoneId = "Pacific/Kiritimati";
        var zone = TimeZoneInfo.FindSystemTimeZoneById(zoneId);
        var userToday = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone));
        var user = await TestData.RegisterAsync(factory, timeZone: zoneId);
        await user.CreateTaskAsync("due today for me", deadline: userToday);
        await user.CreateTaskAsync("due yesterday for me", deadline: userToday.AddDays(-1));
        await user.CreateTaskAsync("due tomorrow for me", deadline: userToday.AddDays(1));

        var today = await (await user.Client.GetAsync("/api/tasks/today")).Read<List<TaskListItemDto>>();

        today.Select(t => t.Title).Should().BeEquivalentTo("due today for me", "due yesterday for me");
        today.Single(t => t.Title == "due today for me").IsOverdue.Should().BeFalse();
        today.Single(t => t.Title == "due yesterday for me").IsOverdue.Should().BeTrue();
        (await user.TitlesAsync("?deadline=today")).Should().Equal("due today for me");
    }

    // ----- Calendar -----

    [Fact]
    public async Task Calendar_ReturnsTasksWithDeadlineInRange_AndRespectsFilters()
    {
        var user = await TestData.RegisterAsync(factory);
        var from = new DateOnly(2030, 6, 1);
        var to = new DateOnly(2030, 6, 30);
        await user.CreateTaskAsync("first day", priority: TaskPriority.High, deadline: from);
        await user.CreateTaskAsync("last day", priority: TaskPriority.Low, deadline: to);
        await user.CreateTaskAsync("outside", deadline: to.AddDays(1));
        await user.CreateTaskAsync("before", deadline: from.AddDays(-1));
        await user.CreateTaskAsync("no date");

        var all = await (await user.Client.GetAsync($"/api/tasks/calendar?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}"))
            .Read<List<TaskListItemDto>>();
        var filtered = await (await user.Client.GetAsync(
            $"/api/tasks/calendar?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}&priority=High")).Read<List<TaskListItemDto>>();

        all.Select(t => t.Title).Should().BeEquivalentTo("first day", "last day");
        filtered.Select(t => t.Title).Should().Equal("first day");
    }

    [Theory]
    [InlineData("")]
    [InlineData("?from=2030-06-01")]
    [InlineData("?from=2030-06-30&to=2030-06-01")]
    [InlineData("?from=2020-01-01&to=2030-01-01")]
    public async Task Calendar_WithMissingOrInvalidRange_Returns400(string query)
    {
        var user = await TestData.RegisterAsync(factory);

        (await user.Client.GetAsync("/api/tasks/calendar" + query)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
