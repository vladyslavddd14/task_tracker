using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskTracker.Application.Dtos;
using TaskTracker.Application.Services;

namespace TaskTracker.Api.Controllers;

[ApiController]
[Route("api/tasks/{taskId:guid}/subtasks")]
[Authorize]
public class SubtasksController(SubtaskService subtasks) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<SubtaskDto>> Add(Guid taskId, SubtaskRequest request, CancellationToken ct)
    {
        var subtask = await subtasks.AddAsync(User.GetUserId(), taskId, request, ct);
        return StatusCode(StatusCodes.Status201Created, subtask);
    }

    [HttpPut("{subtaskId:guid}")]
    public async Task<ActionResult<SubtaskDto>> Update(Guid taskId, Guid subtaskId, SubtaskRequest request,
        CancellationToken ct) =>
        Ok(await subtasks.UpdateAsync(User.GetUserId(), taskId, subtaskId, request, ct));

    [HttpDelete("{subtaskId:guid}")]
    public async Task<IActionResult> Delete(Guid taskId, Guid subtaskId, CancellationToken ct)
    {
        await subtasks.DeleteAsync(User.GetUserId(), taskId, subtaskId, ct);
        return NoContent();
    }

    [HttpPatch("reorder")]
    public async Task<ActionResult<IReadOnlyList<SubtaskDto>>> Reorder(Guid taskId, ReorderSubtasksRequest request,
        CancellationToken ct) =>
        Ok(await subtasks.ReorderAsync(User.GetUserId(), taskId, request, ct));
}
