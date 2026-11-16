using FluentValidation;
using TaskTracker.Application.Abstractions;
using TaskTracker.Application.Common;
using TaskTracker.Application.Dtos;
using TaskTracker.Domain.Entities;

namespace TaskTracker.Application.Services;

public class CustomFieldService(
    ICustomFieldRepository fields, IUnitOfWork unitOfWork, IValidator<CustomFieldRequest> validator)
{
    public async Task<IReadOnlyList<CustomFieldDto>> ListAsync(Guid userId, CancellationToken ct = default) =>
        (await fields.GetAllAsync(userId, ct)).OrderBy(f => f.Position).ThenBy(f => f.Name)
        .Select(f => f.ToDto()).ToList();

    public async Task<CustomFieldDto> CreateAsync(Guid userId, CustomFieldRequest request,
        CancellationToken ct = default)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        await EnsureNameIsFreeAsync(userId, request.Name, null, ct);

        var all = await fields.GetAllAsync(userId, ct);
        var position = request.Position ?? (all.Count == 0 ? 0 : all.Max(f => f.Position) + 1);

        var field = CustomFieldDefinition.Create(userId, request.Name, request.Type, position);
        fields.Add(field);
        await unitOfWork.SaveChangesAsync(ct);
        return field.ToDto();
    }

    public async Task<CustomFieldDto> UpdateAsync(Guid userId, Guid id, CustomFieldRequest request,
        CancellationToken ct = default)
    {
        await validator.ValidateAndThrowAsync(request, ct);

        var field = await fields.GetAsync(userId, id, ct) ?? throw new NotFoundException("Custom field not found.");
        if (request.Type != field.Type)
        {
            throw Fail.Validation(nameof(request.Type), "The type of a custom field cannot be changed.");
        }

        await EnsureNameIsFreeAsync(userId, request.Name, id, ct);

        field.Update(request.Name, request.Position ?? field.Position);
        await unitOfWork.SaveChangesAsync(ct);
        return field.ToDto();
    }

    /// <summary>Removes the field together with all its values (cascade in the database).</summary>
    public async Task DeleteAsync(Guid userId, Guid id, CancellationToken ct = default)
    {
        var field = await fields.GetAsync(userId, id, ct) ?? throw new NotFoundException("Custom field not found.");
        fields.Remove(field);
        await unitOfWork.SaveChangesAsync(ct);
    }

    private async Task EnsureNameIsFreeAsync(Guid userId, string name, Guid? exceptId, CancellationToken ct)
    {
        if (await fields.NameExistsAsync(userId, CustomFieldDefinition.Normalize(name), exceptId, ct))
        {
            throw new ConflictException("A custom field with this name already exists.");
        }
    }
}
