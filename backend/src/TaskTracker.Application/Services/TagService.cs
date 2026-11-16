using FluentValidation;
using TaskTracker.Application.Abstractions;
using TaskTracker.Application.Common;
using TaskTracker.Application.Dtos;
using TaskTracker.Domain.Entities;

namespace TaskTracker.Application.Services;

public class TagService(ITagRepository tags, IUnitOfWork unitOfWork, IValidator<TagRequest> validator)
{
    public async Task<IReadOnlyList<TagDto>> ListAsync(Guid userId, CancellationToken ct = default) =>
        (await tags.GetAllAsync(userId, ct)).OrderBy(t => t.Name).Select(t => t.ToDto()).ToList();

    public async Task<TagDto> CreateAsync(Guid userId, TagRequest request, CancellationToken ct = default)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        await EnsureNameIsFreeAsync(userId, request.Name, null, ct);

        var tag = Tag.Create(userId, request.Name, request.Color);
        tags.Add(tag);
        await unitOfWork.SaveChangesAsync(ct);
        return tag.ToDto();
    }

    public async Task<TagDto> UpdateAsync(Guid userId, Guid id, TagRequest request, CancellationToken ct = default)
    {
        await validator.ValidateAndThrowAsync(request, ct);

        var tag = await tags.GetAsync(userId, id, ct) ?? throw new NotFoundException("Tag not found.");
        await EnsureNameIsFreeAsync(userId, request.Name, id, ct);

        tag.Update(request.Name, request.Color);
        await unitOfWork.SaveChangesAsync(ct);
        return tag.ToDto();
    }

    /// <summary>Removes the tag; tasks keep existing, only their links to the tag disappear.</summary>
    public async Task DeleteAsync(Guid userId, Guid id, CancellationToken ct = default)
    {
        var tag = await tags.GetAsync(userId, id, ct) ?? throw new NotFoundException("Tag not found.");
        tags.Remove(tag);
        await unitOfWork.SaveChangesAsync(ct);
    }

    private async Task EnsureNameIsFreeAsync(Guid userId, string name, Guid? exceptId, CancellationToken ct)
    {
        if (await tags.NameExistsAsync(userId, Tag.Normalize(name), exceptId, ct))
        {
            throw new ConflictException("A tag with this name already exists.");
        }
    }
}
