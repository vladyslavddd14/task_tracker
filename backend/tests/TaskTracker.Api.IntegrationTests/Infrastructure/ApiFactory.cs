using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;
using TaskTracker.Application.Common;
using TaskTracker.Application.Dtos;
using TaskTracker.Domain.Enums;
using Xunit;

namespace TaskTracker.Api.IntegrationTests.Infrastructure;

/// <summary>Starts the real API against a throw-away PostgreSQL container shared by all tests.</summary>
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    Task IAsyncLifetime.InitializeAsync() => _database.StartAsync();

    Task IAsyncLifetime.DisposeAsync() => _database.DisposeAsync().AsTask();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = _database.GetConnectionString(),
            ["Jwt:Secret"] = "integration-tests-only-secret-0123456789",
            ["SEED_DEMO"] = "false"
        }));
    }
}

[CollectionDefinition("api")]
public class ApiCollection : ICollectionFixture<ApiFactory>;

public static class Json
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
}

public static class HttpExtensions
{
    public static Task<HttpResponseMessage> PostJson<T>(this HttpClient client, string url, T body) =>
        client.PostAsJsonAsync(url, body, Json.Options);

    public static Task<HttpResponseMessage> PutJson<T>(this HttpClient client, string url, T body) =>
        client.PutAsJsonAsync(url, body, Json.Options);

    public static Task<HttpResponseMessage> PatchJson<T>(this HttpClient client, string url, T body)
    {
        var request = new HttpRequestMessage(HttpMethod.Patch, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(body, Json.Options), Encoding.UTF8,
                "application/json")
        };
        return client.SendAsync(request);
    }

    public static async Task<T> Read<T>(this HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(Json.Options))!;

    /// <summary>Returns the "errors" dictionary of an RFC 7807 validation problem.</summary>
    public static async Task<Dictionary<string, string[]>> ReadErrors(this HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        return document.RootElement.TryGetProperty("errors", out var errors)
            ? errors.Deserialize<Dictionary<string, string[]>>()!
            : new Dictionary<string, string[]>();
    }
}

public sealed class TestUser(HttpClient client, UserDto user)
{
    public HttpClient Client { get; } = client;
    public UserDto User { get; } = user;
    public Guid Id => User.Id;

    public async Task<TaskDetailsDto> CreateTaskAsync(string title, TaskItemStatus? status = null,
        TaskPriority? priority = null, DateOnly? deadline = null, List<Guid>? tagIds = null,
        string? description = null, string? notes = null, List<CustomFieldValueInput>? customFields = null)
    {
        var response = await Client.PostJson("/api/tasks",
            new CreateTaskRequest(title, description, notes, status, priority, deadline, tagIds, customFields));
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return await response.Read<TaskDetailsDto>();
    }

    public async Task<TagDto> CreateTagAsync(string name, string? color = null)
    {
        var response = await Client.PostJson("/api/tags", new TagRequest(name, color));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return await response.Read<TagDto>();
    }

    public async Task<CustomFieldDto> CreateFieldAsync(string name, CustomFieldType type)
    {
        var response = await Client.PostJson("/api/custom-fields", new CustomFieldRequest(name, type, null));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return await response.Read<CustomFieldDto>();
    }

    public async Task<TaskDetailsDto> GetTaskAsync(Guid id)
    {
        var response = await Client.GetAsync($"/api/tasks/{id}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Read<TaskDetailsDto>();
    }

    public async Task<PagedResult<TaskListItemDto>> ListAsync(string query = "")
    {
        var response = await Client.GetAsync("/api/tasks" + query);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.Read<PagedResult<TaskListItemDto>>();
    }

    public async Task<List<string>> TitlesAsync(string query = "") =>
        (await ListAsync(query)).Items.Select(t => t.Title).ToList();
}

public static class TestData
{
    public static async Task<TestUser> RegisterAsync(ApiFactory factory, ProfileTemplate? template = null,
        string timeZone = "UTC", string password = "password123")
    {
        var email = $"user-{Guid.NewGuid():N}@example.com";
        var response = await factory.CreateClient()
            .PostJson("/api/auth/register", new RegisterRequest(email, password, "Test User", timeZone, template));
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());

        var auth = await response.Read<AuthResponse>();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return new TestUser(client, auth.User);
    }

    public static DateOnly UtcToday => DateOnly.FromDateTime(DateTime.UtcNow);
}
