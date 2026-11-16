using FluentAssertions;
using FluentValidation;
using NSubstitute;
using TaskTracker.Application.Abstractions;
using TaskTracker.Application.Common;
using TaskTracker.Application.Dtos;
using TaskTracker.Application.Filtering;
using TaskTracker.Application.Services;
using TaskTracker.Application.Validators;
using TaskTracker.Domain.Entities;
using TaskTracker.Domain.Enums;

namespace TaskTracker.Application.Tests;

/// <summary>Use cases are tested with substituted repositories, so no database is needed.</summary>
public class TaskServiceTests
{
    private static readonly DateTime Now = new(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc);

    private readonly Guid _userId;
    private readonly User _user;
    private readonly ITaskRepository _tasks = Substitute.For<ITaskRepository>();
    private readonly ITagRepository _tags = Substitute.For<ITagRepository>();
    private readonly ICustomFieldRepository _fields = Substitute.For<ICustomFieldRepository>();
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly TaskService _service;

    public TaskServiceTests()
    {
        _user = User.Create("a@b.co", "hash", "Anna", "UTC", ProfileTemplate.Universal, Now);
        _userId = _user.Id;
        _users.GetByIdAsync(_userId, Arg.Any<CancellationToken>()).Returns(_user);

        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(Now);

        _service = new TaskService(_tasks, _tags, _fields, _users, _unitOfWork, clock,
            new CreateTaskRequestValidator(), new UpdateTaskRequestValidator(), new ChangeStatusRequestValidator(),
            new MoveTaskRequestValidator(), new TaskQueryValidator(), new CalendarQueryValidator());
    }

    private TaskItem ExistingTask(TaskItemStatus status = TaskItemStatus.Todo, double position = 1000)
    {
        var task = TaskItem.Create(_userId, "Existing", status, TaskPriority.Medium, position, Now);
        _tasks.GetAsync(_userId, task.Id, Arg.Any<CancellationToken>()).Returns(task);
        return task;
    }

    [Fact]
    public async Task Create_WithEmptyTitle_IsRejected_AndNothingIsSaved()
    {
        var act = () => _service.CreateAsync(_userId, new CreateTaskRequest("  ", null, null, null, null, null, null,
            null));

        await act.Should().ThrowAsync<ValidationException>();
        _tasks.DidNotReceive().Add(Arg.Any<TaskItem>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_DefaultsToTodoAndMediumPriority_AndPutsTaskAtEndOfColumn()
    {
        _tasks.GetMaxPositionAsync(_userId, TaskItemStatus.Todo, Arg.Any<CancellationToken>()).Returns(3000d);

        var result = await _service.CreateAsync(_userId,
            new CreateTaskRequest(" Buy milk ", null, null, null, null, null, null, null));

        result.Title.Should().Be("Buy milk");
        result.Status.Should().Be(TaskItemStatus.Todo);
        result.Priority.Should().Be(TaskPriority.Medium);
        result.Position.Should().Be(4000);
        result.CreatedAt.Should().Be(Now);
        _tasks.Received(1).Add(Arg.Any<TaskItem>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_WithUnknownTag_IsRejected()
    {
        var tagId = Guid.NewGuid();
        _tags.GetByIdsAsync(_userId, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Tag>());

        var act = () => _service.CreateAsync(_userId,
            new CreateTaskRequest("x", null, null, null, null, null, [tagId], null));

        await act.Should().ThrowAsync<ValidationException>();
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_WithWrongTypeCustomValue_IsRejected()
    {
        var field = CustomFieldDefinition.Create(_userId, "Hours", CustomFieldType.Number, 0);
        _fields.GetAllAsync(_userId, Arg.Any<CancellationToken>()).Returns(new List<CustomFieldDefinition> { field });

        var act = () => _service.CreateAsync(_userId, new CreateTaskRequest("x", null, null, null, null, null, null,
            [new CustomFieldValueInput(field.Id, "many")]));

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Get_ForTaskOfAnotherUser_IsNotFound()
    {
        // The repository filters by user id, so another user's task simply comes back as null.
        var act = () => _service.GetAsync(_userId, Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Delete_ForTaskOfAnotherUser_IsNotFound_AndNothingIsRemoved()
    {
        var act = () => _service.DeleteAsync(_userId, Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>();
        _tasks.DidNotReceive().Remove(Arg.Any<TaskItem>());
    }

    [Fact]
    public async Task ChangeStatus_RecordsTransition_AndSavesOnce()
    {
        var task = ExistingTask();

        var result = await _service.ChangeStatusAsync(_userId, task.Id, new ChangeStatusRequest(TaskItemStatus.Done));

        result.Status.Should().Be(TaskItemStatus.Done);
        result.CompletedAt.Should().Be(Now);
        result.Transitions.Should().ContainSingle(t =>
            t.FromStatus == TaskItemStatus.Todo && t.ToStatus == TaskItemStatus.Done && t.ChangedAt == Now);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_WithEmptyTitle_IsRejected_AndTaskIsUnchanged()
    {
        var task = ExistingTask();

        var act = () => _service.UpdateAsync(_userId, task.Id,
            new UpdateTaskRequest("", null, null, TaskItemStatus.Todo, TaskPriority.Low, null, null, null));

        await act.Should().ThrowAsync<ValidationException>();
        task.Title.Should().Be("Existing");
    }

    [Fact]
    public async Task Move_ToAnotherColumn_ChangesStatusAndPosition()
    {
        var task = ExistingTask();
        _tasks.GetColumnAsync(_userId, TaskItemStatus.InProgress, Arg.Any<CancellationToken>())
            .Returns(new List<TaskItem>());

        var result = await _service.MoveAsync(_userId, task.Id, new MoveTaskRequest(TaskItemStatus.InProgress, 2500));

        result.Status.Should().Be(TaskItemStatus.InProgress);
        result.Position.Should().Be(2500);
        result.Transitions.Should().ContainSingle();
    }

    [Fact]
    public async Task Move_WithinColumn_KeepsStatus_AndRecordsNoTransition()
    {
        var task = ExistingTask();
        _tasks.GetColumnAsync(_userId, TaskItemStatus.Todo, Arg.Any<CancellationToken>())
            .Returns(new List<TaskItem> { task });

        var result = await _service.MoveAsync(_userId, task.Id, new MoveTaskRequest(TaskItemStatus.Todo, 500));

        result.Status.Should().Be(TaskItemStatus.Todo);
        result.Position.Should().Be(500);
        result.Transitions.Should().BeEmpty();
    }

    [Fact]
    public async Task Move_RenumbersTheColumn_WhenPositionsGetTooClose()
    {
        var first = TaskItem.Create(_userId, "First", TaskItemStatus.Todo, TaskPriority.Low, 1000, Now);
        var second = TaskItem.Create(_userId, "Second", TaskItemStatus.Todo, TaskPriority.Low, 2000, Now);
        var moved = ExistingTask(position: 9000);
        _tasks.GetColumnAsync(_userId, TaskItemStatus.Todo, Arg.Any<CancellationToken>())
            .Returns(new List<TaskItem> { first, second, moved });

        // Dropped between "first" and "second" at almost exactly the position of "first".
        await _service.MoveAsync(_userId, moved.Id, new MoveTaskRequest(TaskItemStatus.Todo, 1000.0000001));

        new[] { first, moved, second }.Select(t => t.Position).Should().Equal(1000, 2000, 3000);
    }

    [Fact]
    public async Task Today_QueriesUnfinishedTasksDueTodayOrEarlier_SortedByPriority()
    {
        TaskQuery? captured = null;
        _tasks.SearchAsync(_userId, Arg.Do<TaskQuery>(q => captured = q), Arg.Any<DateOnly>(), false,
                Arg.Any<CancellationToken>())
            .Returns(new PagedResult<TaskItem>([], 0, 1, 0));

        await _service.TodayAsync(_userId);

        captured.Should().NotBeNull();
        captured!.Status.Should().BeEquivalentTo([TaskItemStatus.Todo, TaskItemStatus.InProgress]);
        captured.DeadlineTo.Should().Be(new DateOnly(2026, 11, 5));
        captured.Sort.Should().Be(TaskSortField.Priority);
    }

    [Fact]
    public async Task List_WithInvalidPageSize_IsRejected()
    {
        var act = () => _service.ListAsync(_userId, new TaskQuery { PageSize = 500 });

        await act.Should().ThrowAsync<ValidationException>();
        await _tasks.DidNotReceive().SearchAsync(Arg.Any<Guid>(), Arg.Any<TaskQuery>(), Arg.Any<DateOnly>(),
            Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }
}

public class SubtaskServiceTests
{
    private static readonly DateTime Now = new(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc);

    private readonly Guid _userId = Guid.NewGuid();
    private readonly ITaskRepository _tasks = Substitute.For<ITaskRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly SubtaskService _service;

    public SubtaskServiceTests()
    {
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(Now);
        _service = new SubtaskService(_tasks, _unitOfWork, clock, new SubtaskRequestValidator(),
            new ReorderSubtasksRequestValidator());
    }

    private TaskItem ExistingTask()
    {
        var task = TaskItem.Create(_userId, "Parent", TaskItemStatus.Todo, TaskPriority.Low, 1, Now);
        _tasks.GetAsync(_userId, task.Id, Arg.Any<CancellationToken>()).Returns(task);
        return task;
    }

    [Fact]
    public async Task Add_AppendsSubtask_AndSaves()
    {
        var task = ExistingTask();

        var dto = await _service.AddAsync(_userId, task.Id, new SubtaskRequest(" Step ", false));

        dto.Title.Should().Be("Step");
        task.Subtasks.Should().ContainSingle();
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Add_ToTaskOfAnotherUser_IsNotFound()
    {
        var act = () => _service.AddAsync(_userId, Guid.NewGuid(), new SubtaskRequest("Step", false));

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Update_MarksSubtaskDone()
    {
        var task = ExistingTask();
        var subtask = task.AddSubtask("Step", Now);

        var dto = await _service.UpdateAsync(_userId, task.Id, subtask.Id, new SubtaskRequest("Step", true));

        dto.IsDone.Should().BeTrue();
        task.Status.Should().Be(TaskItemStatus.Todo);
    }

    [Fact]
    public async Task Update_UnknownSubtask_IsNotFound()
    {
        var task = ExistingTask();

        var act = () => _service.UpdateAsync(_userId, task.Id, Guid.NewGuid(), new SubtaskRequest("Step", true));

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Delete_RemovesSubtask()
    {
        var task = ExistingTask();
        var subtask = task.AddSubtask("Step", Now);

        await _service.DeleteAsync(_userId, task.Id, subtask.Id);

        task.Subtasks.Should().BeEmpty();
    }
}

public class ProfileTemplateServiceTests
{
    private readonly ITagRepository _tags = Substitute.For<ITagRepository>();
    private readonly ICustomFieldRepository _fields = Substitute.For<ICustomFieldRepository>();

    private User NewUser() =>
        User.Create("a@b.co", "hash", "Anna", "UTC", ProfileTemplate.Universal, DateTime.UtcNow);

    [Fact]
    public async Task Student_CreatesDefaultTagsAndFields()
    {
        var user = NewUser();
        _tags.GetAllAsync(user.Id, Arg.Any<CancellationToken>()).Returns(new List<Tag>());
        _fields.GetAllAsync(user.Id, Arg.Any<CancellationToken>()).Returns(new List<CustomFieldDefinition>());

        await new ProfileTemplateService(_tags, _fields).ApplyAsync(user, ProfileTemplate.Student);

        _tags.Received(3).Add(Arg.Any<Tag>());
        _fields.Received(2).Add(Arg.Any<CustomFieldDefinition>());
    }

    [Fact]
    public async Task ExistingData_IsKept_AndDuplicatesByNameAreSkipped()
    {
        var user = NewUser();
        _tags.GetAllAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(new List<Tag> { Tag.Create(user.Id, "ЛЕКЦІЯ", null), Tag.Create(user.Id, "my own", null) });
        _fields.GetAllAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(new List<CustomFieldDefinition>
            {
                CustomFieldDefinition.Create(user.Id, "предмет", CustomFieldType.Text, 4)
            });

        await new ProfileTemplateService(_tags, _fields).ApplyAsync(user, ProfileTemplate.Student);

        _tags.Received(2).Add(Arg.Is<Tag>(t => t.Name != "лекція"));
        _fields.Received(1).Add(Arg.Is<CustomFieldDefinition>(f => f.Name == "Оцінка" && f.Position == 5));
        _tags.DidNotReceive().Remove(Arg.Any<Tag>());
    }

    [Fact]
    public async Task Universal_AddsNothing()
    {
        await new ProfileTemplateService(_tags, _fields).ApplyAsync(NewUser(), ProfileTemplate.Universal);

        _tags.DidNotReceive().Add(Arg.Any<Tag>());
        _fields.DidNotReceive().Add(Arg.Any<CustomFieldDefinition>());
    }
}

public class AuthServiceTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly ITokenService _tokens = Substitute.For<ITokenService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly AuthService _service;

    public AuthServiceTests()
    {
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(DateTime.UtcNow);
        _hasher.Hash(Arg.Any<string>()).Returns(call => "hashed:" + call.Arg<string>());
        _tokens.Create(Arg.Any<User>()).Returns(new AccessToken("token", DateTime.UtcNow.AddHours(1)));

        var templates = new ProfileTemplateService(Substitute.For<ITagRepository>(),
            Substitute.For<ICustomFieldRepository>());
        _service = new AuthService(_users, _hasher, _tokens, clock, _unitOfWork, templates,
            new RegisterRequestValidator(), new LoginRequestValidator());
    }

    [Fact]
    public async Task Register_StoresOnlyTheHash_AndLowercasesEmail()
    {
        User? saved = null;
        _users.When(u => u.Add(Arg.Any<User>())).Do(call => saved = call.Arg<User>());

        var response = await _service.RegisterAsync(
            new RegisterRequest("Anna@Example.com", "password123", "Anna", "Europe/Kyiv", null));

        saved.Should().NotBeNull();
        saved!.Email.Should().Be("anna@example.com");
        saved.PasswordHash.Should().Be("hashed:password123").And.NotBe("password123");
        response.AccessToken.Should().Be("token");
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Register_WithExistingEmail_IsConflict()
    {
        _users.EmailExistsAsync("anna@example.com", Arg.Any<CancellationToken>()).Returns(true);

        var act = () => _service.RegisterAsync(
            new RegisterRequest("ANNA@example.com", "password123", "Anna", null, null));

        await act.Should().ThrowAsync<ConflictException>();
        _users.DidNotReceive().Add(Arg.Any<User>());
    }

    [Fact]
    public async Task Register_WithShortPassword_IsRejected()
    {
        var act = () => _service.RegisterAsync(new RegisterRequest("a@b.co", "short", "Anna", null, null));

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Login_WithWrongPassword_IsUnauthorized()
    {
        var user = User.Create("a@b.co", "hash", "Anna", "UTC", ProfileTemplate.Universal, DateTime.UtcNow);
        _users.GetByEmailAsync("a@b.co", Arg.Any<CancellationToken>()).Returns(user);
        _hasher.Verify("wrong", "hash").Returns(false);

        var act = () => _service.LoginAsync(new LoginRequest("a@b.co", "wrong"));

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Login_WithUnknownEmail_IsUnauthorized()
    {
        var act = () => _service.LoginAsync(new LoginRequest("nobody@b.co", "password123"));

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Login_WithCorrectPassword_ReturnsToken()
    {
        var user = User.Create("a@b.co", "hash", "Anna", "UTC", ProfileTemplate.Universal, DateTime.UtcNow);
        _users.GetByEmailAsync("a@b.co", Arg.Any<CancellationToken>()).Returns(user);
        _hasher.Verify("password123", "hash").Returns(true);

        var response = await _service.LoginAsync(new LoginRequest("A@B.co", "password123"));

        response.AccessToken.Should().Be("token");
        response.User.Email.Should().Be("a@b.co");
    }
}

public class TagServiceTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly ITagRepository _tags = Substitute.For<ITagRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private TagService Service() => new(_tags, _unitOfWork, new TagRequestValidator());

    [Fact]
    public async Task Create_WithDuplicateNameIgnoringCase_IsConflict()
    {
        _tags.NameExistsAsync(_userId, "work", null, Arg.Any<CancellationToken>()).Returns(true);

        var act = () => Service().CreateAsync(_userId, new TagRequest("WORK", null));

        await act.Should().ThrowAsync<ConflictException>();
        _tags.DidNotReceive().Add(Arg.Any<Tag>());
    }

    [Fact]
    public async Task Rename_OfAnotherUsersTag_IsNotFound()
    {
        var act = () => Service().UpdateAsync(_userId, Guid.NewGuid(), new TagRequest("x", null));

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Create_SavesTrimmedName()
    {
        var dto = await Service().CreateAsync(_userId, new TagRequest("  home ", "#112233"));

        dto.Name.Should().Be("home");
        dto.Color.Should().Be("#112233");
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
