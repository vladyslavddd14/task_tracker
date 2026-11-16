using System.Net;
using FluentAssertions;
using TaskTracker.Api.IntegrationTests.Infrastructure;
using TaskTracker.Application.Dtos;
using TaskTracker.Domain.Enums;
using Xunit;

namespace TaskTracker.Api.IntegrationTests;

[Collection("api")]
public class SubtasksKanbanTests(ApiFactory factory)
{
    private static async Task<SubtaskDto> AddSubtask(TestUser user, Guid taskId, string title)
    {
        var response = await user.Client.PostJson($"/api/tasks/{taskId}/subtasks", new SubtaskRequest(title, false));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return await response.Read<SubtaskDto>();
    }

    // ----- Subtasks -----

    [Fact]
    public async Task Subtasks_CanBeAddedEditedMarkedAndDeleted()
    {
        var user = await TestData.RegisterAsync(factory);
        var task = await user.CreateTaskAsync("Parent");

        var step = await AddSubtask(user, task.Id, "Step");
        var edit = await user.Client.PutJson($"/api/tasks/{task.Id}/subtasks/{step.Id}",
            new SubtaskRequest("Renamed step", true));
        edit.StatusCode.Should().Be(HttpStatusCode.OK);

        var saved = await user.GetTaskAsync(task.Id);
        saved.Subtasks.Should().ContainSingle(s => s.Title == "Renamed step" && s.IsDone);

        (await user.Client.DeleteAsync($"/api/tasks/{task.Id}/subtasks/{step.Id}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await user.GetTaskAsync(task.Id)).Subtasks.Should().BeEmpty();
    }

    [Fact]
    public async Task Subtask_WithEmptyTitle_Returns400()
    {
        var user = await TestData.RegisterAsync(factory);
        var task = await user.CreateTaskAsync("Parent");

        var response = await user.Client.PostJson($"/api/tasks/{task.Id}/subtasks", new SubtaskRequest("  ", false));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Subtasks_AreLimitedTo50PerTask()
    {
        var user = await TestData.RegisterAsync(factory);
        var task = await user.CreateTaskAsync("Parent");
        for (var i = 0; i < 50; i++)
        {
            await AddSubtask(user, task.Id, $"Step {i}");
        }

        var response = await user.Client.PostJson($"/api/tasks/{task.Id}/subtasks", new SubtaskRequest("51st", false));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await user.GetTaskAsync(task.Id)).Subtasks.Should().HaveCount(50);
    }

    [Fact]
    public async Task Subtasks_CanBeReordered_AndOrderIsPersisted()
    {
        var user = await TestData.RegisterAsync(factory);
        var task = await user.CreateTaskAsync("Parent");
        var a = await AddSubtask(user, task.Id, "A");
        var b = await AddSubtask(user, task.Id, "B");
        var c = await AddSubtask(user, task.Id, "C");

        var response = await user.Client.PatchJson($"/api/tasks/{task.Id}/subtasks/reorder",
            new ReorderSubtasksRequest([c.Id, a.Id, b.Id]));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await user.GetTaskAsync(task.Id)).Subtasks.Select(s => s.Title).Should().Equal("C", "A", "B");
    }

    [Fact]
    public async Task Reorder_WithMissingIds_Returns400()
    {
        var user = await TestData.RegisterAsync(factory);
        var task = await user.CreateTaskAsync("Parent");
        var a = await AddSubtask(user, task.Id, "A");
        await AddSubtask(user, task.Id, "B");

        var response = await user.Client.PatchJson($"/api/tasks/{task.Id}/subtasks/reorder",
            new ReorderSubtasksRequest([a.Id]));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CompletingAllSubtasks_DoesNotChangeTheTaskStatus()
    {
        var user = await TestData.RegisterAsync(factory);
        var task = await user.CreateTaskAsync("Parent");
        var step = await AddSubtask(user, task.Id, "Only step");

        await user.Client.PutJson($"/api/tasks/{task.Id}/subtasks/{step.Id}", new SubtaskRequest("Only step", true));

        var saved = await user.GetTaskAsync(task.Id);
        saved.Status.Should().Be(TaskItemStatus.Todo);
        saved.Subtasks.Should().OnlyContain(s => s.IsDone);
    }

    [Fact]
    public async Task UnknownSubtask_Returns404()
    {
        var user = await TestData.RegisterAsync(factory);
        var task = await user.CreateTaskAsync("Parent");

        (await user.Client.PutJson($"/api/tasks/{task.Id}/subtasks/{Guid.NewGuid()}", new SubtaskRequest("x", false)))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await user.Client.DeleteAsync($"/api/tasks/{task.Id}/subtasks/{Guid.NewGuid()}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ----- Kanban -----

    private static async Task<List<TaskListItemDto>> Column(TestUser user, TaskItemStatus status)
    {
        var page = await user.ListAsync($"?status={status}&pageSize=200");
        return page.Items.OrderBy(t => t.Position).ToList();
    }

    [Fact]
    public async Task NewTasks_AreAppendedToTheEndOfTheirColumn()
    {
        var user = await TestData.RegisterAsync(factory);
        await user.CreateTaskAsync("A");
        await user.CreateTaskAsync("B");
        await user.CreateTaskAsync("C");

        (await Column(user, TaskItemStatus.Todo)).Select(t => t.Title).Should().Equal("A", "B", "C");
    }

    [Fact]
    public async Task Move_WithinColumn_ChangesOrder_AndItIsPersisted()
    {
        var user = await TestData.RegisterAsync(factory);
        var a = await user.CreateTaskAsync("A");
        var b = await user.CreateTaskAsync("B");
        var c = await user.CreateTaskAsync("C");

        // Drop C between A and B.
        var position = (a.Position + b.Position) / 2;
        var response = await user.Client.PatchJson($"/api/tasks/{c.Id}/move",
            new MoveTaskRequest(TaskItemStatus.Todo, position));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Column(user, TaskItemStatus.Todo)).Select(t => t.Title).Should().Equal("A", "C", "B");
        (await user.GetTaskAsync(c.Id)).Transitions.Should().BeEmpty("the status did not change");
    }

    [Fact]
    public async Task Move_ToAnotherColumn_ChangesStatus_RecordsTransition_AndKeepsPosition()
    {
        var user = await TestData.RegisterAsync(factory);
        var a = await user.CreateTaskAsync("A");
        var existing = await user.CreateTaskAsync("Already doing", TaskItemStatus.InProgress);

        var response = await user.Client.PatchJson($"/api/tasks/{a.Id}/move",
            new MoveTaskRequest(TaskItemStatus.InProgress, existing.Position - 500));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var moved = await user.GetTaskAsync(a.Id);
        moved.Status.Should().Be(TaskItemStatus.InProgress);
        moved.Transitions.Should().ContainSingle(t =>
            t.FromStatus == TaskItemStatus.Todo && t.ToStatus == TaskItemStatus.InProgress);
        (await Column(user, TaskItemStatus.InProgress)).Select(t => t.Title).Should().Equal("A", "Already doing");
        (await Column(user, TaskItemStatus.Todo)).Should().BeEmpty();
    }

    [Fact]
    public async Task Move_ToDone_SetsCompletedAt()
    {
        var user = await TestData.RegisterAsync(factory);
        var task = await user.CreateTaskAsync("A");

        await user.Client.PatchJson($"/api/tasks/{task.Id}/move", new MoveTaskRequest(TaskItemStatus.Done, 1000));

        (await user.GetTaskAsync(task.Id)).CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Move_RepeatedlyBetweenSameNeighbours_KeepsOrder_ByRenumbering()
    {
        var user = await TestData.RegisterAsync(factory);
        var a = await user.CreateTaskAsync("A");
        var b = await user.CreateTaskAsync("B");
        var c = await user.CreateTaskAsync("C");
        var d = await user.CreateTaskAsync("D");

        // Squeeze C and D into the gap between A and B again and again, always halving the gap.
        var low = a.Position;
        var high = b.Position;
        for (var i = 0; i < 45; i++)
        {
            var middle = (low + high) / 2;
            var mover = i % 2 == 0 ? c : d;
            var response = await user.Client.PatchJson($"/api/tasks/{mover.Id}/move",
                new MoveTaskRequest(TaskItemStatus.Todo, middle));
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            high = middle;
        }

        var order = (await Column(user, TaskItemStatus.Todo)).Select(t => t.Title).ToList();
        order.First().Should().Be("A");
        order.Last().Should().Be("B");
        order.Should().BeEquivalentTo("A", "B", "C", "D");
        (await Column(user, TaskItemStatus.Todo)).Select(t => t.Position).Distinct().Should().HaveCount(4);
    }

    [Fact]
    public async Task Move_WithNonFinitePosition_Returns400()
    {
        var user = await TestData.RegisterAsync(factory);
        var task = await user.CreateTaskAsync("A");

        var response = await user.Client.PatchJson($"/api/tasks/{task.Id}/move",
            new { status = "Todo", position = "NaN" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ChangingStatus_MovesTheCardToTheEndOfTheNewColumn()
    {
        var user = await TestData.RegisterAsync(factory);
        await user.CreateTaskAsync("Doing 1", TaskItemStatus.InProgress);
        var todo = await user.CreateTaskAsync("Todo");

        await user.Client.PatchJson($"/api/tasks/{todo.Id}/status", new ChangeStatusRequest(TaskItemStatus.InProgress));

        (await Column(user, TaskItemStatus.InProgress)).Select(t => t.Title).Should().Equal("Doing 1", "Todo");
    }
}
