using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskTracker.Application.Dtos;
using TaskTracker.Application.Services;

namespace TaskTracker.Api.Controllers;

[ApiController]
[Route("api/tags")]
[Authorize]
public class TagsController(TagService tags) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TagDto>>> List(CancellationToken ct) =>
        Ok(await tags.ListAsync(User.GetUserId(), ct));

    [HttpPost]
    public async Task<ActionResult<TagDto>> Create(TagRequest request, CancellationToken ct)
    {
        var tag = await tags.CreateAsync(User.GetUserId(), request, ct);
        return StatusCode(StatusCodes.Status201Created, tag);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TagDto>> Update(Guid id, TagRequest request, CancellationToken ct) =>
        Ok(await tags.UpdateAsync(User.GetUserId(), id, request, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await tags.DeleteAsync(User.GetUserId(), id, ct);
        return NoContent();
    }
}
