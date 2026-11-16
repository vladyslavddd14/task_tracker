using FluentValidation;
using TaskTracker.Application.Abstractions;
using TaskTracker.Application.Common;
using TaskTracker.Application.Dtos;
using TaskTracker.Domain.Entities;

namespace TaskTracker.Application.Services;

public class SubtaskService(
    ITaskRepository tasks,
    IUnitOfWork unitOfWork,
    IClock clock,
    IValidator<SubtaskRequest> validator,
    IValidator<ReorderSubtasksRequest> reorderValidator)
{
    public async Task<SubtaskDto> AddAsync(Guid userId, Guid taskId, SubtaskRequest request,
        CancellationToken ct = default)
    {
        await validator.ValidateAndThrowAsync(request, ct);

        var task = await LoadTaskAsync(userId, taskId, ct);
        var subtask = task.AddSubtask(request.Title, clock.UtcNow);
        if (request.IsDone)
        {
            subtask.Update(subtask.Title, true);
        }

        await unitOfWork.SaveChangesAsync(ct);
        return subtask.ToDto();
    }

    public async Task<SubtaskDto> UpdateAsync(Guid userId, Guid taskId, Guid subtaskId, SubtaskRequest request,
        CancellationToken ct = default)
    {
        await validator.ValidateAndThrowAsync(request, ct);

        var task = await LoadTaskAsync(userId, taskId, ct);
        var subtask = FindSubtask(task, subtaskId);
        subtask.Update(request.Title, request.IsDone);
        task.Touch(clock.UtcNow);

        await unitOfWork.SaveChangesAsync(ct);
        return subtask.ToDto();
    }

    public async Task DeleteAsync(Guid userId, Guid taskId, Guid subtaskId, CancellationToken ct = default)
    {
        var task = await LoadTaskAsync(userId, taskId, ct);
        task.RemoveSubtask(FindSubtask(task, subtaskId), clock.UtcNow);
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<SubtaskDto>> ReorderAsync(Guid userId, Guid taskId,
        ReorderSubtasksRequest request, CancellationToken ct = default)
    {
        await reorderValidator.ValidateAndThrowAsync(request, ct);

        var task = await LoadTaskAsync(userId, taskId, ct);
        task.ReorderSubtasks(request.OrderedIds, clock.UtcNow);

        await unitOfWork.SaveChangesAsync(ct);
        return task.Subtasks.OrderBy(s => s.Position).Select(s => s.ToDto()).ToList();
    }

    private async Task<TaskItem> LoadTaskAsync(Guid userId, Guid taskId, CancellationToken ct) =>
        await tasks.GetAsync(userId, taskId, ct) ?? throw new NotFoundException("Task not found.");

    private static Subtask FindSubtask(TaskItem task, Guid subtaskId) =>
        task.Subtasks.FirstOrDefault(s => s.Id == subtaskId) ?? throw new NotFoundException("Subtask not found.");
}
