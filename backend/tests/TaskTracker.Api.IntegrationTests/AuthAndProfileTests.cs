using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using TaskTracker.Api.IntegrationTests.Infrastructure;
using TaskTracker.Application.Dtos;
using TaskTracker.Domain.Enums;
using TaskTracker.Infrastructure.Persistence;
using Xunit;

namespace TaskTracker.Api.IntegrationTests;

[Collection("api")]
public class AuthAndProfileTests(ApiFactory factory)
{
    private HttpClient Anonymous => factory.CreateClient();

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var response = await Anonymous.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Register_ReturnsTokenAndUser_AndTokenWorksForMe()
    {
        var user = await TestData.RegisterAsync(factory, timeZone: "Europe/Kyiv");

        var me = await user.Client.GetAsync("/api/me");

        me.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await me.Read<UserDto>();
        dto.Id.Should().Be(user.Id);
        dto.TimeZoneId.Should().Be("Europe/Kyiv");
        dto.ProfileTemplate.Should().Be(ProfileTemplate.Universal);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409_IgnoringCase()
    {
        var first = await TestData.RegisterAsync(factory);

        var response = await Anonymous.PostJson("/api/auth/register",
            new RegisterRequest(first.User.Email.ToUpperInvariant(), "password123", "Other", null, null));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Register_WithShortPassword_Returns400_WithFieldErrors()
    {
        var response = await Anonymous.PostJson("/api/auth/register",
            new RegisterRequest("short@example.com", "1234567", "Anna", null, null));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ReadErrors()).Should().ContainKey("password");
    }

    [Fact]
    public async Task Register_WithInvalidEmail_Returns400()
    {
        var response = await Anonymous.PostJson("/api/auth/register",
            new RegisterRequest("not-an-email", "password123", "Anna", null, null));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ReadErrors()).Should().ContainKey("email");
    }

    [Fact]
    public async Task Passwords_AreStoredOnlyAsSaltedHashes()
    {
        var first = await TestData.RegisterAsync(factory, password: "same-password-1");
        var second = await TestData.RegisterAsync(factory, password: "same-password-1");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hashes = await db.Users.Where(u => u.Id == first.Id || u.Id == second.Id)
            .Select(u => u.PasswordHash).ToListAsync();

        hashes.Should().HaveCount(2);
        hashes.Should().OnlyContain(h => h.StartsWith("$2") && !h.Contains("same-password-1"));
        hashes.Distinct().Should().HaveCount(2, "each hash has its own random salt");
    }

    [Fact]
    public async Task Login_WithCorrectPassword_ReturnsToken()
    {
        var user = await TestData.RegisterAsync(factory, password: "my-secret-pass");

        var response = await Anonymous.PostJson("/api/auth/login", new LoginRequest(user.User.Email, "my-secret-pass"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Read<AuthResponse>()).AccessToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        var user = await TestData.RegisterAsync(factory);

        var response = await Anonymous.PostJson("/api/auth/login", new LoginRequest(user.User.Email, "wrong-password"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("/api/tasks")]
    [InlineData("/api/tasks/today")]
    [InlineData("/api/tags")]
    [InlineData("/api/custom-fields")]
    [InlineData("/api/me")]
    public async Task ProtectedEndpoints_WithoutToken_Return401(string url)
    {
        var response = await Anonymous.GetAsync(url);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task InvalidToken_Returns401()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "not.a.token");

        (await client.GetAsync("/api/tasks")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateProfile_ChangesNameAndTimeZone()
    {
        var user = await TestData.RegisterAsync(factory);

        var response = await user.Client.PutJson("/api/me", new UpdateProfileRequest("New Name", "Europe/Kyiv"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Read<UserDto>();
        dto.DisplayName.Should().Be("New Name");
        dto.TimeZoneId.Should().Be("Europe/Kyiv");
    }

    [Fact]
    public async Task UpdateProfile_WithUnknownTimeZone_Returns400()
    {
        var user = await TestData.RegisterAsync(factory);

        var response = await user.Client.PutJson("/api/me", new UpdateProfileRequest("Name", "Mars/Olympus"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ChangePassword_RequiresCurrentPassword_AndNewPasswordWorksForLogin()
    {
        var user = await TestData.RegisterAsync(factory, password: "old-password-1");

        var wrong = await user.Client.PutJson("/api/me/password", new ChangePasswordRequest("nope", "new-password-1"));
        wrong.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var ok = await user.Client.PutJson("/api/me/password",
            new ChangePasswordRequest("old-password-1", "new-password-1"));
        ok.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await Anonymous.PostJson("/api/auth/login", new LoginRequest(user.User.Email, "new-password-1")))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await Anonymous.PostJson("/api/auth/login", new LoginRequest(user.User.Email, "old-password-1")))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ----- Profile templates (the personal twist) -----

    [Fact]
    public async Task Register_WithStudentTemplate_CreatesDefaultTagsAndFields()
    {
        var user = await TestData.RegisterAsync(factory, ProfileTemplate.Student);

        var tags = await (await user.Client.GetAsync("/api/tags")).Read<List<TagDto>>();
        var fields = await (await user.Client.GetAsync("/api/custom-fields")).Read<List<CustomFieldDto>>();

        tags.Select(t => t.Name).Should().BeEquivalentTo("лекція", "лабораторна", "екзамен");
        fields.Select(f => f.Name).Should().Equal("Предмет", "Оцінка");
        user.User.ProfileTemplate.Should().Be(ProfileTemplate.Student);
    }

    [Fact]
    public async Task ApplyTemplate_KeepsExistingData_AndSkipsDuplicates()
    {
        var user = await TestData.RegisterAsync(factory);
        await user.CreateTagAsync("Клієнт");
        var ownTag = await user.CreateTagAsync("my own");
        await user.CreateFieldAsync("клієнт", CustomFieldType.Text);

        var first = await user.Client.PostJson("/api/me/apply-template", new ApplyTemplateRequest(ProfileTemplate.Freelancer));
        var second = await user.Client.PostJson("/api/me/apply-template", new ApplyTemplateRequest(ProfileTemplate.Freelancer));

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        (await first.Read<UserDto>()).ProfileTemplate.Should().Be(ProfileTemplate.Freelancer);
        second.StatusCode.Should().Be(HttpStatusCode.OK);

        var tags = await (await user.Client.GetAsync("/api/tags")).Read<List<TagDto>>();
        tags.Select(t => t.Name.ToLowerInvariant()).Should().BeEquivalentTo("клієнт", "рахунок", "my own");
        tags.Should().Contain(t => t.Id == ownTag.Id);

        var fields = await (await user.Client.GetAsync("/api/custom-fields")).Read<List<CustomFieldDto>>();
        fields.Select(f => f.Name.ToLowerInvariant()).Should()
            .BeEquivalentTo("клієнт", "ставка/год", "виставлено рахунок");
    }
}
