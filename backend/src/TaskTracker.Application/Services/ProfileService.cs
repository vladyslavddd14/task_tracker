using FluentValidation;
using TaskTracker.Application.Abstractions;
using TaskTracker.Application.Common;
using TaskTracker.Application.Dtos;
using TaskTracker.Domain.Entities;

namespace TaskTracker.Application.Services;

public class ProfileService(
    IUserRepository users,
    IPasswordHasher hasher,
    IUnitOfWork unitOfWork,
    ProfileTemplateService templates,
    IValidator<UpdateProfileRequest> updateValidator,
    IValidator<ChangePasswordRequest> passwordValidator,
    IValidator<ApplyTemplateRequest> templateValidator)
{
    public async Task<UserDto> GetAsync(Guid userId, CancellationToken ct = default) =>
        (await GetUserAsync(userId, ct)).ToDto();

    public async Task<UserDto> UpdateAsync(Guid userId, UpdateProfileRequest request, CancellationToken ct = default)
    {
        await updateValidator.ValidateAndThrowAsync(request, ct);

        var user = await GetUserAsync(userId, ct);
        user.UpdateProfile(request.DisplayName, request.TimeZoneId);
        await unitOfWork.SaveChangesAsync(ct);
        return user.ToDto();
    }

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken ct = default)
    {
        await passwordValidator.ValidateAndThrowAsync(request, ct);

        var user = await GetUserAsync(userId, ct);
        if (!hasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            throw Fail.Validation(nameof(request.CurrentPassword), "The current password is incorrect.");
        }

        user.ChangePasswordHash(hasher.Hash(request.NewPassword));
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<UserDto> ApplyTemplateAsync(Guid userId, ApplyTemplateRequest request,
        CancellationToken ct = default)
    {
        await templateValidator.ValidateAndThrowAsync(request, ct);

        var user = await GetUserAsync(userId, ct);
        user.SetProfileTemplate(request.Template);
        await templates.ApplyAsync(user, request.Template, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return user.ToDto();
    }

    private async Task<User> GetUserAsync(Guid userId, CancellationToken ct) =>
        await users.GetByIdAsync(userId, ct) ?? throw new UnauthorizedException("The user no longer exists.");
}
