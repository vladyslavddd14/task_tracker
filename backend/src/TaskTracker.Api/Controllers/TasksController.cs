using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskTracker.Application.Common;
using TaskTracker.Application.Dtos;
using TaskTracker.Application.Filtering;
using TaskTracker.Application.Services;

namespace TaskTracker.Api.Controllers;

[ApiController]
[Route("api/tasks")]
[Authorize]
public class TasksController(TaskService tasks) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<TaskListItemDto>>> List([FromQuery] TaskQuery query,
        CancellationToken ct) =>
        Ok(await tasks.ListAsync(User.GetUserId(), query, ct));

    [HttpGet("today")]
    public async Task<ActionResult<IReadOnlyList<TaskListItemDto>>> Today(CancellationToken ct) =>
        Ok(await tasks.TodayAsync(User.GetUserId(), ct));

    [HttpGet("calendar")]
    public async Task<ActionResult<IReadOnlyList<TaskListItemDto>>> Calendar([FromQuery] CalendarQuery query,
        CancellationToken ct) =>
        Ok(await tasks.CalendarAsync(User.GetUserId(), query, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TaskDetailsDto>> Get(Guid id, CancellationToken ct) =>
        Ok(await tasks.GetAsync(User.GetUserId(), id, ct));

    [HttpPost]
    public async Task<ActionResult<TaskDetailsDto>> Create(CreateTaskRequest request, CancellationToken ct)
    {
        var task = await tasks.CreateAsync(User.GetUserId(), request, ct);
        return CreatedAtAction(nameof(Get), new { id = task.Id }, task);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TaskDetailsDto>> Update(Guid id, UpdateTaskRequest request, CancellationToken ct) =>
        Ok(await tasks.UpdateAsync(User.GetUserId(), id, request, ct));

    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<TaskDetailsDto>> ChangeStatus(Guid id, ChangeStatusRequest request,
        CancellationToken ct) =>
        Ok(await tasks.ChangeStatusAsync(User.GetUserId(), id, request, ct));

    [HttpPatch("{id:guid}/move")]
    public async Task<ActionResult<TaskDetailsDto>> Move(Guid id, MoveTaskRequest request, CancellationToken ct) =>
        Ok(await tasks.MoveAsync(User.GetUserId(), id, request, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await tasks.DeleteAsync(User.GetUserId(), id, ct);
        return NoContent();
    }
}
