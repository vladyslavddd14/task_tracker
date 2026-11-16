using System.Net;
using FluentAssertions;
using TaskTracker.Api.IntegrationTests.Infrastructure;
using TaskTracker.Application.Dtos;
using TaskTracker.Domain.Enums;
using Xunit;

namespace TaskTracker.Api.IntegrationTests;

[Collection("api")]
public class TaskCrudTests(ApiFactory factory)
{
    [Fact]
    public async Task Create_DefaultsToTodoAndMedium_AndStoresCreatedAt()
    {
        var user = await TestData.RegisterAsync(factory);
        var before = DateTime.UtcNow.AddSeconds(-5);

        var task = await user.CreateTaskAsync("Buy milk");

        task.Status.Should().Be(TaskItemStatus.Todo);
        task.Priority.Should().Be(TaskPriority.Medium);
        task.CreatedAt.Should().BeAfter(before);
        task.CompletedAt.Should().BeNull();
        (await user.TitlesAsync()).Should().Equal("Buy milk");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_WithEmptyTitle_Returns400_AndStoresNothing(string title)
    {
        var user = await TestData.RegisterAsync(factory);

        var response = await user.Client.PostJson("/api/tasks",
            new CreateTaskRequest(title, null, null, null, null, null, null, null));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        (await response.ReadErrors()).Should().ContainKey("title");
        (await user.ListAsync()).Total.Should().Be(0);
    }

    [Fact]
    public async Task Create_WithTitleOver200_Returns400()
    {
        var user = await TestData.RegisterAsync(factory);

        var response = await user.Client.PostJson("/api/tasks",
            new CreateTaskRequest(new string('a', 201), null, null, null, null, null, null, null));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_TrimsTitle()
    {
        var user = await TestData.RegisterAsync(factory);

        var task = await user.CreateTaskAsync("  padded  ");

        task.Title.Should().Be("padded");
    }

    [Fact]
    public async Task Update_ChangesAllFields()
    {
        var user = await TestData.RegisterAsync(factory);
        var tag = await user.CreateTagAsync("work");
        var task = await user.CreateTaskAsync("Original");
        var deadline = TestData.UtcToday.AddDays(7);

        var response = await user.Client.PutJson($"/api/tasks/{task.Id}",
            new UpdateTaskRequest("Edited", "Plain description", "# Notes\n- [ ] item", TaskItemStatus.InProgress,
                TaskPriority.High, deadline, [tag.Id], null));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var saved = await user.GetTaskAsync(task.Id);
        saved.Title.Should().Be("Edited");
        saved.Description.Should().Be("Plain description");
        saved.NotesMarkdown.Should().Be("# Notes\n- [ ] item");
        saved.Status.Should().Be(TaskItemStatus.InProgress);
        saved.Priority.Should().Be(TaskPriority.High);
        saved.Deadline.Should().Be(deadline);
        saved.Tags.Should().ContainSingle(t => t.Id == tag.Id);
        saved.UpdatedAt.Should().BeOnOrAfter(task.UpdatedAt);
    }

    [Fact]
    public async Task Update_WithEmptyTitle_Returns400_AndKeepsOldTitle()
    {
        var user = await TestData.RegisterAsync(factory);
        var task = await user.CreateTaskAsync("Keep me");

        var response = await user.Client.PutJson($"/api/tasks/{task.Id}",
            new UpdateTaskRequest("  ", null, null, TaskItemStatus.Todo, TaskPriority.Low, null, null, null));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await user.GetTaskAsync(task.Id)).Title.Should().Be("Keep me");
    }

    [Fact]
    public async Task Update_RemovesTagsAndDeadline_WhenOmitted()
    {
        var user = await TestData.RegisterAsync(factory);
        var tag = await user.CreateTagAsync("work");
        var task = await user.CreateTaskAsync("Task", deadline: TestData.UtcToday, tagIds: [tag.Id]);

        await user.Client.PutJson($"/api/tasks/{task.Id}",
            new UpdateTaskRequest("Task", null, null, TaskItemStatus.Todo, TaskPriority.Medium, null, [], null));

        var saved = await user.GetTaskAsync(task.Id);
        saved.Tags.Should().BeEmpty();
        saved.Deadline.Should().BeNull();
    }

    [Fact]
    public async Task StatusChanges_AreRecordedAsTransitions_AndCompletedAtFollowsDone()
    {
        var user = await TestData.RegisterAsync(factory);
        var task = await user.CreateTaskAsync("Flow");

        async Task<TaskDetailsDto> Change(TaskItemStatus status)
        {
            var response = await user.Client.PatchJson($"/api/tasks/{task.Id}/status", new ChangeStatusRequest(status));
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            return await response.Read<TaskDetailsDto>();
        }

        (await Change(TaskItemStatus.InProgress)).CompletedAt.Should().BeNull();
        var done = await Change(TaskItemStatus.Done);
        done.CompletedAt.Should().NotBeNull();
        var reopened = await Change(TaskItemStatus.Todo);
        reopened.CompletedAt.Should().BeNull();

        var saved = await user.GetTaskAsync(task.Id);
        saved.Transitions.Select(t => (t.FromStatus, t.ToStatus)).Should().Equal(
            (TaskItemStatus.Todo, TaskItemStatus.InProgress),
            (TaskItemStatus.InProgress, TaskItemStatus.Done),
            (TaskItemStatus.Done, TaskItemStatus.Todo));
        saved.Transitions.Should().OnlyContain(t => t.ChangedAt > DateTime.UtcNow.AddMinutes(-5));
    }

    [Fact]
    public async Task StatusChange_ToSameStatus_AddsNoTransition()
    {
        var user = await TestData.RegisterAsync(factory);
        var task = await user.CreateTaskAsync("Same");

        await user.Client.PatchJson($"/api/tasks/{task.Id}/status", new ChangeStatusRequest(TaskItemStatus.Todo));

        (await user.GetTaskAsync(task.Id)).Transitions.Should().BeEmpty();
    }

    [Fact]
    public async Task StatusChange_ViaUpdate_IsRecordedToo()
    {
        var user = await TestData.RegisterAsync(factory);
        var task = await user.CreateTaskAsync("Via PUT");

        await user.Client.PutJson($"/api/tasks/{task.Id}",
            new UpdateTaskRequest("Via PUT", null, null, TaskItemStatus.Done, TaskPriority.Medium, null, null, null));

        var saved = await user.GetTaskAsync(task.Id);
        saved.Status.Should().Be(TaskItemStatus.Done);
        saved.Transitions.Should().ContainSingle();
    }

    [Fact]
    public async Task StatusChange_WithUnknownStatus_Returns400()
    {
        var user = await TestData.RegisterAsync(factory);
        var task = await user.CreateTaskAsync("Bad status");

        var response = await user.Client.PatchJson($"/api/tasks/{task.Id}/status", new { status = "Finished" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Delete_RemovesTask()
    {
        var user = await TestData.RegisterAsync(factory);
        var task = await user.CreateTaskAsync("Doomed");

        (await user.Client.DeleteAsync($"/api/tasks/{task.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await user.Client.GetAsync($"/api/tasks/{task.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await user.Client.DeleteAsync($"/api/tasks/{task.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_UnknownId_Returns404ProblemDetails()
    {
        var user = await TestData.RegisterAsync(factory);

        var response = await user.Client.GetAsync($"/api/tasks/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        (await response.Content.ReadAsStringAsync()).Should().NotContain("   at ", "stack traces must not leak");
    }

    [Fact]
    public async Task Notes_Over20000Characters_Return400()
    {
        var user = await TestData.RegisterAsync(factory);
        var task = await user.CreateTaskAsync("Notes");

        var response = await user.Client.PutJson($"/api/tasks/{task.Id}",
            new UpdateTaskRequest("Notes", null, new string('n', 20_001), TaskItemStatus.Todo, TaskPriority.Low, null,
                null, null));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Notes_AreStoredAsRawMarkdown_SanitizingHappensInTheBrowser()
    {
        var user = await TestData.RegisterAsync(factory);
        const string markdown = "## Title\n\n<script>alert(1)</script>\n\n[x](javascript:alert(1))";

        var task = await user.CreateTaskAsync("Notes", notes: markdown);

        (await user.GetTaskAsync(task.Id)).NotesMarkdown.Should().Be(markdown);
    }

    [Fact]
    public async Task Details_ContainSubtasksTagsAndCustomFields()
    {
        var user = await TestData.RegisterAsync(factory);
        var tag = await user.CreateTagAsync("home");
        var field = await user.CreateFieldAsync("Hours", CustomFieldType.Number);
        var task = await user.CreateTaskAsync("Full", tagIds: [tag.Id],
            customFields: [new CustomFieldValueInput(field.Id, "2.5")]);
        await user.Client.PostJson($"/api/tasks/{task.Id}/subtasks", new SubtaskRequest("Step 1", false));

        var saved = await user.GetTaskAsync(task.Id);

        saved.Tags.Should().ContainSingle(t => t.Name == "home");
        saved.Subtasks.Should().ContainSingle(s => s.Title == "Step 1");
        saved.CustomFields.Should().ContainSingle(v => v.DefinitionId == field.Id && v.Value == "2.5");
    }

    [Fact]
    public async Task Deadline_IsACalendarDate_NotShiftedByTimeZones()
    {
        var user = await TestData.RegisterAsync(factory, timeZone: "Pacific/Kiritimati");
        var deadline = new DateOnly(2027, 3, 1);

        var task = await user.CreateTaskAsync("Date", deadline: deadline);

        (await user.GetTaskAsync(task.Id)).Deadline.Should().Be(deadline);
    }
}
