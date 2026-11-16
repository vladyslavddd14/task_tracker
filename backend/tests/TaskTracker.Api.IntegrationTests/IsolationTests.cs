using System.Net;
using FluentAssertions;
using TaskTracker.Api.IntegrationTests.Infrastructure;
using TaskTracker.Application.Dtos;
using TaskTracker.Domain.Enums;
using Xunit;

namespace TaskTracker.Api.IntegrationTests;

/// <summary>Every user sees and changes only their own data; foreign resources look like they do not exist.</summary>
[Collection("api")]
public class IsolationTests(ApiFactory factory)
{
    [Fact]
    public async Task AnotherUser_CannotReadChangeMoveOrDeleteTheTask()
    {
        var owner = await TestData.RegisterAsync(factory);
        var intruder = await TestData.RegisterAsync(factory);
        var task = await owner.CreateTaskAsync("Private", deadline: TestData.UtcToday);
        var id = task.Id;

        (await intruder.Client.GetAsync($"/api/tasks/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await intruder.Client.PutJson($"/api/tasks/{id}",
                new UpdateTaskRequest("Hacked", null, null, TaskItemStatus.Done, TaskPriority.Low, null, null, null)))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await intruder.Client.PatchJson($"/api/tasks/{id}/status", new ChangeStatusRequest(TaskItemStatus.Done)))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await intruder.Client.PatchJson($"/api/tasks/{id}/move", new MoveTaskRequest(TaskItemStatus.Done, 1)))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await intruder.Client.DeleteAsync($"/api/tasks/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await intruder.Client.PostJson($"/api/tasks/{id}/subtasks", new SubtaskRequest("x", false)))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        var untouched = await owner.GetTaskAsync(id);
        untouched.Title.Should().Be("Private");
        untouched.Status.Should().Be(TaskItemStatus.Todo);
    }

    [Fact]
    public async Task Lists_NeverContainTasksOfAnotherUser()
    {
        var owner = await TestData.RegisterAsync(factory);
        var other = await TestData.RegisterAsync(factory);
        await owner.CreateTaskAsync("Owner task", deadline: TestData.UtcToday.AddDays(-1));
        await owner.CreateTaskAsync("Owner due today", deadline: TestData.UtcToday);

        (await other.ListAsync()).Total.Should().Be(0);
        (await other.ListAsync("?q=owner")).Total.Should().Be(0);
        (await other.Client.GetAsync("/api/tasks/today")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await (await other.Client.GetAsync("/api/tasks/today")).Read<List<TaskListItemDto>>()).Should().BeEmpty();

        var from = TestData.UtcToday.AddDays(-30);
        var to = TestData.UtcToday.AddDays(30);
        var calendar = await other.Client.GetAsync($"/api/tasks/calendar?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}");
        (await calendar.Read<List<TaskListItemDto>>()).Should().BeEmpty();
    }

    [Fact]
    public async Task AnotherUser_CannotSeeOrChangeTagsAndFields()
    {
        var owner = await TestData.RegisterAsync(factory);
        var intruder = await TestData.RegisterAsync(factory);
        var tag = await owner.CreateTagAsync("secret");
        var field = await owner.CreateFieldAsync("Secret field", CustomFieldType.Text);

        (await (await intruder.Client.GetAsync("/api/tags")).Read<List<TagDto>>()).Should().BeEmpty();
        (await (await intruder.Client.GetAsync("/api/custom-fields")).Read<List<CustomFieldDto>>()).Should().BeEmpty();
        (await intruder.Client.PutJson($"/api/tags/{tag.Id}", new TagRequest("renamed", null)))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await intruder.Client.DeleteAsync($"/api/tags/{tag.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await intruder.Client.PutJson($"/api/custom-fields/{field.Id}",
                new CustomFieldRequest("renamed", CustomFieldType.Text, null)))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await intruder.Client.DeleteAsync($"/api/custom-fields/{field.Id}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AnotherUser_CannotAttachForeignTagOrFieldToOwnTask()
    {
        var owner = await TestData.RegisterAsync(factory);
        var intruder = await TestData.RegisterAsync(factory);
        var tag = await owner.CreateTagAsync("secret");
        var field = await owner.CreateFieldAsync("Secret field", CustomFieldType.Text);

        var withTag = await intruder.Client.PostJson("/api/tasks",
            new CreateTaskRequest("Mine", null, null, null, null, null, [tag.Id], null));
        var withField = await intruder.Client.PostJson("/api/tasks",
            new CreateTaskRequest("Mine", null, null, null, null, null, null,
                [new CustomFieldValueInput(field.Id, "x")]));

        withTag.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        withField.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await intruder.ListAsync()).Total.Should().Be(0);
    }

    [Fact]
    public async Task TagNames_AreUniquePerUser_NotGlobally()
    {
        var first = await TestData.RegisterAsync(factory);
        var second = await TestData.RegisterAsync(factory);

        await first.CreateTagAsync("shared-name");
        var other = await second.Client.PostJson("/api/tags", new TagRequest("shared-name", null));

        other.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
