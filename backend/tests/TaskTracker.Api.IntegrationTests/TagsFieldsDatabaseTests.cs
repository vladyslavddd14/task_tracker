using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskTracker.Api.IntegrationTests.Infrastructure;
using TaskTracker.Application.Dtos;
using TaskTracker.Domain.Enums;
using TaskTracker.Infrastructure;
using TaskTracker.Infrastructure.Persistence;
using TaskTracker.Infrastructure.Seed;
using Xunit;

namespace TaskTracker.Api.IntegrationTests;

[Collection("api")]
public class TagsAndFieldsTests(ApiFactory factory)
{
    [Fact]
    public async Task Tags_CanBeCreatedRenamedAndDeleted()
    {
        var user = await TestData.RegisterAsync(factory);
        var tag = await user.CreateTagAsync("work", "#aabbcc");

        var rename = await user.Client.PutJson($"/api/tags/{tag.Id}", new TagRequest("job", "#112233"));
        rename.StatusCode.Should().Be(HttpStatusCode.OK);
        var renamed = await rename.Read<TagDto>();
        renamed.Name.Should().Be("job");
        renamed.Color.Should().Be("#112233");

        (await user.Client.DeleteAsync($"/api/tags/{tag.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await (await user.Client.GetAsync("/api/tags")).Read<List<TagDto>>()).Should().BeEmpty();
    }

    [Fact]
    public async Task TagName_MustBeUniqueIgnoringCase_Returns409()
    {
        var user = await TestData.RegisterAsync(factory);
        await user.CreateTagAsync("Work");

        var duplicate = await user.Client.PostJson("/api/tags", new TagRequest("wORK", null));

        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task RenamingATag_ToAnotherExistingName_Returns409_ButChangingOwnCaseIsAllowed()
    {
        var user = await TestData.RegisterAsync(factory);
        var work = await user.CreateTagAsync("work");
        await user.CreateTagAsync("home");

        (await user.Client.PutJson($"/api/tags/{work.Id}", new TagRequest("HOME", null)))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await user.Client.PutJson($"/api/tags/{work.Id}", new TagRequest("WORK", null)))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Tag_WithEmptyName_Returns400(string name)
    {
        var user = await TestData.RegisterAsync(factory);

        (await user.Client.PostJson("/api/tags", new TagRequest(name, null))).StatusCode
            .Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Tag_WithNameOver30Characters_Returns400()
    {
        var user = await TestData.RegisterAsync(factory);

        (await user.Client.PostJson("/api/tags", new TagRequest(new string('t', 31), null))).StatusCode
            .Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeletingATag_KeepsTheTask_AndOnlyRemovesTheLink()
    {
        var user = await TestData.RegisterAsync(factory);
        var work = await user.CreateTagAsync("work");
        var home = await user.CreateTagAsync("home");
        var task = await user.CreateTaskAsync("Keep me", tagIds: [work.Id, home.Id]);

        await user.Client.DeleteAsync($"/api/tags/{work.Id}");

        var saved = await user.GetTaskAsync(task.Id);
        saved.Title.Should().Be("Keep me");
        saved.Tags.Select(t => t.Name).Should().Equal("home");
    }

    [Fact]
    public async Task CustomFields_CanBeCreatedRenamedReorderedAndDeleted()
    {
        var user = await TestData.RegisterAsync(factory);
        var first = await user.CreateFieldAsync("Subject", CustomFieldType.Text);
        var second = await user.CreateFieldAsync("Grade", CustomFieldType.Number);

        var rename = await user.Client.PutJson($"/api/custom-fields/{second.Id}",
            new CustomFieldRequest("Mark", CustomFieldType.Number, 0));
        rename.StatusCode.Should().Be(HttpStatusCode.OK);
        await user.Client.PutJson($"/api/custom-fields/{first.Id}",
            new CustomFieldRequest("Subject", CustomFieldType.Text, 1));

        var fields = await (await user.Client.GetAsync("/api/custom-fields")).Read<List<CustomFieldDto>>();
        fields.Select(f => f.Name).Should().Equal("Mark", "Subject");

        (await user.Client.DeleteAsync($"/api/custom-fields/{first.Id}")).StatusCode
            .Should().Be(HttpStatusCode.NoContent);
        (await (await user.Client.GetAsync("/api/custom-fields")).Read<List<CustomFieldDto>>()).Should().ContainSingle();
    }

    [Fact]
    public async Task CustomField_NameMustBeUnique_AndTypeCannotChange()
    {
        var user = await TestData.RegisterAsync(factory);
        var field = await user.CreateFieldAsync("Client", CustomFieldType.Text);

        (await user.Client.PostJson("/api/custom-fields", new CustomFieldRequest("CLIENT", CustomFieldType.Number, null)))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await user.Client.PutJson($"/api/custom-fields/{field.Id}",
                new CustomFieldRequest("Client", CustomFieldType.Number, null)))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CustomField_WithNameOver40Characters_Returns400()
    {
        var user = await TestData.RegisterAsync(factory);

        (await user.Client.PostJson("/api/custom-fields",
                new CustomFieldRequest(new string('f', 41), CustomFieldType.Text, null)))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData(CustomFieldType.Number, "abc")]
    [InlineData(CustomFieldType.Checkbox, "maybe")]
    [InlineData(CustomFieldType.Date, "yesterday")]
    public async Task CustomFieldValues_AreValidatedByType(CustomFieldType type, string value)
    {
        var user = await TestData.RegisterAsync(factory);
        var field = await user.CreateFieldAsync("Field", type);

        var response = await user.Client.PostJson("/api/tasks", new CreateTaskRequest("T", null, null, null, null,
            null, null, [new CustomFieldValueInput(field.Id, value)]));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await user.ListAsync()).Total.Should().Be(0);
    }

    [Fact]
    public async Task CustomFieldValues_AreStoredTyped_AndBlankClearsTheValue()
    {
        var user = await TestData.RegisterAsync(factory);
        var number = await user.CreateFieldAsync("Rate", CustomFieldType.Number);
        var check = await user.CreateFieldAsync("Invoiced", CustomFieldType.Checkbox);
        var date = await user.CreateFieldAsync("Paid on", CustomFieldType.Date);
        var task = await user.CreateTaskAsync("Job", customFields:
        [
            new(number.Id, "35.5"), new(check.Id, "TRUE"), new(date.Id, "2026-11-05")
        ]);

        var saved = await user.GetTaskAsync(task.Id);
        saved.CustomFields.Should().Contain(v => v.DefinitionId == number.Id && v.Value == "35.5");
        saved.CustomFields.Should().Contain(v => v.DefinitionId == check.Id && v.Value == "true");
        saved.CustomFields.Should().Contain(v => v.DefinitionId == date.Id && v.Value == "2026-11-05");

        await user.Client.PutJson($"/api/tasks/{task.Id}", new UpdateTaskRequest("Job", null, null,
            TaskItemStatus.Todo, TaskPriority.Medium, null, null, [new CustomFieldValueInput(number.Id, "")]));

        var cleared = await user.GetTaskAsync(task.Id);
        cleared.CustomFields.Should().NotContain(v => v.DefinitionId == number.Id);
        cleared.CustomFields.Should().HaveCount(2, "values that were not sent stay unchanged");
    }
}

[Collection("api")]
public class DatabaseTests(ApiFactory factory)
{
    [Fact]
    public async Task DeletingATask_RemovesSubtasksTransitionsTagLinksAndFieldValues()
    {
        var user = await TestData.RegisterAsync(factory);
        var tag = await user.CreateTagAsync("work");
        var field = await user.CreateFieldAsync("Hours", CustomFieldType.Number);
        var task = await user.CreateTaskAsync("Cascade", tagIds: [tag.Id],
            customFields: [new CustomFieldValueInput(field.Id, "3")]);
        await user.Client.PostJson($"/api/tasks/{task.Id}/subtasks", new SubtaskRequest("Step", false));
        await user.Client.PatchJson($"/api/tasks/{task.Id}/status", new ChangeStatusRequest(TaskItemStatus.Done));

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Subtasks.CountAsync(s => s.TaskItemId == task.Id)).Should().Be(1);
            (await db.StatusTransitions.CountAsync(t => t.TaskItemId == task.Id)).Should().Be(1);
            (await db.TaskTags.CountAsync(l => l.TaskItemId == task.Id)).Should().Be(1);
            (await db.CustomFieldValues.CountAsync(v => v.TaskItemId == task.Id)).Should().Be(1);
        }

        await user.Client.DeleteAsync($"/api/tasks/{task.Id}");

        await using var after = factory.Services.CreateAsyncScope();
        var dbAfter = after.ServiceProvider.GetRequiredService<AppDbContext>();
        (await dbAfter.Subtasks.CountAsync(s => s.TaskItemId == task.Id)).Should().Be(0);
        (await dbAfter.StatusTransitions.CountAsync(t => t.TaskItemId == task.Id)).Should().Be(0);
        (await dbAfter.TaskTags.CountAsync(l => l.TaskItemId == task.Id)).Should().Be(0);
        (await dbAfter.CustomFieldValues.CountAsync(v => v.TaskItemId == task.Id)).Should().Be(0);
        (await dbAfter.Tags.CountAsync(t => t.Id == tag.Id)).Should().Be(1, "the tag itself must survive");
    }

    [Fact]
    public async Task DeletingACustomField_RemovesItsValues_ButKeepsTheTask()
    {
        var user = await TestData.RegisterAsync(factory);
        var field = await user.CreateFieldAsync("Hours", CustomFieldType.Number);
        var task = await user.CreateTaskAsync("Keep", customFields: [new CustomFieldValueInput(field.Id, "3")]);

        await user.Client.DeleteAsync($"/api/custom-fields/{field.Id}");

        (await user.GetTaskAsync(task.Id)).CustomFields.Should().BeEmpty();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.CustomFieldValues.CountAsync(v => v.CustomFieldDefinitionId == field.Id)).Should().Be(0);
    }

    [Fact]
    public async Task Migrations_AreIdempotent_RunningStartupInitializationAgainIsSafe()
    {
        _ = factory.CreateClient(); // make sure the host (and its first migration) has started

        var act = () => DatabaseInitializer.InitializeAsync(factory.Services);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ConcurrentUpdatesOfOneTask_AreDetected_InsteadOfSilentlyOverwritten()
    {
        var user = await TestData.RegisterAsync(factory);
        var task = await user.CreateTaskAsync("Contended");

        await using var first = factory.Services.CreateAsyncScope();
        await using var second = factory.Services.CreateAsyncScope();
        var db1 = first.ServiceProvider.GetRequiredService<AppDbContext>();
        var db2 = second.ServiceProvider.GetRequiredService<AppDbContext>();
        var copy1 = await db1.Tasks.SingleAsync(t => t.Id == task.Id);
        var copy2 = await db2.Tasks.SingleAsync(t => t.Id == task.Id);

        copy1.Update("Edited by first", null, null, TaskPriority.High, null, DateTime.UtcNow);
        await db1.SaveChangesAsync();
        copy2.Update("Edited by second", null, null, TaskPriority.Low, null, DateTime.UtcNow);
        var act = () => db2.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
        (await user.GetTaskAsync(task.Id)).Title.Should().Be("Edited by first");
    }

    [Fact]
    public async Task DemoSeed_CreatesARealisticUser_AndIsRepeatable()
    {
        _ = factory.CreateClient();
        var email = $"demo-{Guid.NewGuid():N}@example.com";

        for (var i = 0; i < 2; i++)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<DemoDataSeeder>().SeedAsync(email, "demo-pass-123");
        }

        var login = await factory.CreateClient()
            .PostJson("/api/auth/login", new LoginRequest(email, "demo-pass-123"));
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = await login.Read<AuthResponse>();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var user = new TestUser(client, auth.User);

        var tasks = (await user.ListAsync("?pageSize=200")).Items;
        tasks.Should().HaveCountGreaterThan(5, "the second run must not duplicate anything");
        tasks.Select(t => t.Status).Distinct().Should().HaveCount(3);
        tasks.Select(t => t.Priority).Distinct().Should().HaveCount(3);
        tasks.Should().Contain(t => t.IsOverdue);
        tasks.Should().Contain(t => t.Tags.Count > 0);
        tasks.Should().Contain(t => t.SubtasksTotal > 0);
        tasks.Should().NotContain(t => t.Title.StartsWith("Task "));
    }
}
