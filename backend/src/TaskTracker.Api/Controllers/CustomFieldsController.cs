using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskTracker.Application.Dtos;
using TaskTracker.Application.Services;

namespace TaskTracker.Api.Controllers;

[ApiController]
[Route("api/custom-fields")]
[Authorize]
public class CustomFieldsController(CustomFieldService fields) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CustomFieldDto>>> List(CancellationToken ct) =>
        Ok(await fields.ListAsync(User.GetUserId(), ct));

    [HttpPost]
    public async Task<ActionResult<CustomFieldDto>> Create(CustomFieldRequest request, CancellationToken ct)
    {
        var field = await fields.CreateAsync(User.GetUserId(), request, ct);
        return StatusCode(StatusCodes.Status201Created, field);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CustomFieldDto>> Update(Guid id, CustomFieldRequest request,
        CancellationToken ct) =>
        Ok(await fields.UpdateAsync(User.GetUserId(), id, request, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await fields.DeleteAsync(User.GetUserId(), id, ct);
        return NoContent();
    }
}
