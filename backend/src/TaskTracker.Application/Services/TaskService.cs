using FluentValidation;
using TaskTracker.Application.Abstractions;
using TaskTracker.Application.Common;
using TaskTracker.Application.Dtos;
using TaskTracker.Application.Filtering;
using TaskTracker.Domain.Entities;
using TaskTracker.Domain.Enums;

namespace TaskTracker.Application.Services;

public class TaskService(
    ITaskRepository tasks,
    ITagRepository tags,
    ICustomFieldRepository fields,
    IUserRepository users,
    IUnitOfWork unitOfWork,
    IClock clock,
    IValidator<CreateTaskRequest> createValidator,
    IValidator<UpdateTaskRequest> updateValidator,
    IValidator<ChangeStatusRequest> statusValidator,
    IValidator<MoveTaskRequest> moveValidator,
    IValidator<TaskQuery> queryValidator,
    IValidator<CalendarQuery> calendarValidator)
{
    /// <summary>Gap between positions of neighbouring cards. Positions are renumbered when they get too close.</summary>
    private const double PositionStep = 1000;

    private const double MinPositionGap = 1e-6;

    // ----- Queries -----

    public async Task<PagedResult<TaskListItemDto>> ListAsync(Guid userId, TaskQuery query,
        CancellationToken ct = default)
    {
        await queryValidator.ValidateAndThrowAsync(query, ct);

        var today = await GetTodayAsync(userId, ct);
        var page = await tasks.SearchAsync(userId, query, today, paged: true, ct);
        return new PagedResult<TaskListItemDto>(
            page.Items.Select(t => t.ToListItem(today)).ToList(), page.Total, query.Page, query.PageSize);
    }

    /// <summary>Not finished tasks that are due today or overdue, most important first.</summary>
    public async Task<IReadOnlyList<TaskListItemDto>> TodayAsync(Guid userId, CancellationToken ct = default)
    {
        var today = await GetTodayAsync(userId, ct);
        var query = new TaskQuery
        {
            Status = [TaskItemStatus.Todo, TaskItemStatus.InProgress],
            DeadlineTo = today,
            Sort = TaskSortField.Priority
        };

        var page = await tasks.SearchAsync(userId, query, today, paged: false, ct);
        return page.Items.Select(t => t.ToListItem(today)).ToList();
    }

    public async Task<IReadOnlyList<TaskListItemDto>> CalendarAsync(Guid userId, CalendarQuery query,
        CancellationToken ct = default)
    {
        await calendarValidator.ValidateAndThrowAsync(query, ct);

        var today = await GetTodayAsync(userId, ct);
        var rangeQuery = query.WithDeadlineRange(query.From!.Value, query.To!.Value);
        var page = await tasks.SearchAsync(userId, rangeQuery, today, paged: false, ct);
        return page.Items.Select(t => t.ToListItem(today)).ToList();
    }

    public async Task<TaskDetailsDto> GetAsync(Guid userId, Guid id, CancellationToken ct = default)
    {
        var task = await LoadAsync(userId, id, ct);
        return task.ToDetails(await GetTodayAsync(userId, ct));
    }

    // ----- Commands -----

    public async Task<TaskDetailsDto> CreateAsync(Guid userId, CreateTaskRequest request,
        CancellationToken ct = default)
    {
        await createValidator.ValidateAndThrowAsync(request, ct);

        var status = request.Status ?? TaskItemStatus.Todo;
        var position = await tasks.GetMaxPositionAsync(userId, status, ct) + PositionStep;

        var task = TaskItem.Create(userId, request.Title, status, request.Priority ?? TaskPriority.Medium,
            position, clock.UtcNow);
        task.Update(request.Title, request.Description, request.NotesMarkdown, task.Priority, request.Deadline,
            clock.UtcNow);

        await ApplyTagsAndFieldsAsync(userId, task, request.TagIds, request.CustomFields, ct);

        tasks.Add(task);
        await unitOfWork.SaveChangesAsync(ct);
        return task.ToDetails(await GetTodayAsync(userId, ct));
    }

    public async Task<TaskDetailsDto> UpdateAsync(Guid userId, Guid id, UpdateTaskRequest request,
        CancellationToken ct = default)
    {
        await updateValidator.ValidateAndThrowAsync(request, ct);

        var task = await LoadAsync(userId, id, ct);
        var now = clock.UtcNow;

        task.Update(request.Title, request.Description, request.NotesMarkdown, request.Priority, request.Deadline,
            now);
        await ChangeStatusAtEndOfColumnAsync(userId, task, request.Status, now, ct);
        await ApplyTagsAndFieldsAsync(userId, task, request.TagIds, request.CustomFields, ct);

        await unitOfWork.SaveChangesAsync(ct);
        return task.ToDetails(await GetTodayAsync(userId, ct));
    }

    public async Task<TaskDetailsDto> ChangeStatusAsync(Guid userId, Guid id, ChangeStatusRequest request,
        CancellationToken ct = default)
    {
        await statusValidator.ValidateAndThrowAsync(request, ct);

        var task = await LoadAsync(userId, id, ct);
        await ChangeStatusAtEndOfColumnAsync(userId, task, request.Status, clock.UtcNow, ct);

        await unitOfWork.SaveChangesAsync(ct);
        return task.ToDetails(await GetTodayAsync(userId, ct));
    }

    /// <summary>Kanban move: new status and new position in one atomic save.</summary>
    public async Task<TaskDetailsDto> MoveAsync(Guid userId, Guid id, MoveTaskRequest request,
        CancellationToken ct = default)
    {
        await moveValidator.ValidateAndThrowAsync(request, ct);

        var task = await LoadAsync(userId, id, ct);
        task.ChangeStatus(request.Status, clock.UtcNow);
        task.SetPosition(request.Position);

        // The moved task may still be stored in another column, so add it to the target column explicitly.
        var column = (await tasks.GetColumnAsync(userId, request.Status, ct)).Where(t => t.Id != task.Id)
            .Append(task).OrderBy(t => t.Position).ThenBy(t => t.CreatedAt).ToList();
        RenumberIfCrowded(column);

        await unitOfWork.SaveChangesAsync(ct);
        return task.ToDetails(await GetTodayAsync(userId, ct));
    }

    public async Task DeleteAsync(Guid userId, Guid id, CancellationToken ct = default)
    {
        var task = await LoadAsync(userId, id, ct);
        tasks.Remove(task);
        await unitOfWork.SaveChangesAsync(ct);
    }

    // ----- Helpers -----

    private async Task<TaskItem> LoadAsync(Guid userId, Guid id, CancellationToken ct) =>
        await tasks.GetAsync(userId, id, ct) ?? throw new NotFoundException("Task not found.");

    private async Task<DateOnly> GetTodayAsync(Guid userId, CancellationToken ct)
    {
        var user = await users.GetByIdAsync(userId, ct) ?? throw new UnauthorizedException("The user no longer exists.");
        return UserTime.Today(user.TimeZoneId, clock.UtcNow);
    }

    /// <summary>A task whose status changes goes to the bottom of its new column.</summary>
    private async Task ChangeStatusAtEndOfColumnAsync(Guid userId, TaskItem task, TaskItemStatus status,
        DateTime now, CancellationToken ct)
    {
        if (task.Status == status)
        {
            return;
        }

        var position = await tasks.GetMaxPositionAsync(userId, status, ct) + PositionStep;
        task.ChangeStatus(status, now);
        task.SetPosition(position);
    }

    private async Task ApplyTagsAndFieldsAsync(Guid userId, TaskItem task, List<Guid>? tagIds,
        List<CustomFieldValueInput>? customFields, CancellationToken ct)
    {
        if (tagIds is not null)
        {
            var ids = tagIds.Distinct().ToList();
            var found = await tags.GetByIdsAsync(userId, ids, ct);
            if (found.Count != ids.Count)
            {
                throw Fail.Validation("tagIds", "One or more tags do not exist.");
            }

            task.SetTags(found);
        }

        if (customFields is not null)
        {
            task.SetCustomFieldValues(await ResolveCustomFieldsAsync(userId, customFields, ct));
        }
    }

    /// <summary>Checks every value against the type of its field. Blank values clear the field.</summary>
    private async Task<List<(Guid DefinitionId, string? Value)>> ResolveCustomFieldsAsync(
        Guid userId, List<CustomFieldValueInput> inputs, CancellationToken ct)
    {
        var result = new List<(Guid, string?)>();
        if (inputs.Count == 0)
        {
            return result;
        }

        var definitions = await fields.GetAllAsync(userId, ct);
        foreach (var input in inputs)
        {
            var definition = definitions.FirstOrDefault(d => d.Id == input.DefinitionId)
                             ?? throw Fail.Validation("customFields", "Unknown custom field.");

            if (!CustomFieldValueRules.TryNormalize(definition.Type, input.Value, out var value, out var error))
            {
                throw Fail.Validation($"customFields.{definition.Name}", error!);
            }

            result.Add((definition.Id, value));
        }

        return result;
    }

    /// <summary>Spreads the cards of a column evenly when two positions are almost equal.</summary>
    private static void RenumberIfCrowded(List<TaskItem> column)
    {
        var crowded = false;
        for (var i = 1; i < column.Count; i++)
        {
            if (column[i].Position - column[i - 1].Position < MinPositionGap)
            {
                crowded = true;
                break;
            }
        }

        if (!crowded)
        {
            return;
        }

        for (var i = 0; i < column.Count; i++)
        {
            column[i].SetPosition((i + 1) * PositionStep);
        }
    }
}
