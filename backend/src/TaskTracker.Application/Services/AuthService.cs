using FluentValidation;
using TaskTracker.Application.Abstractions;
using TaskTracker.Application.Common;
using TaskTracker.Application.Dtos;
using TaskTracker.Domain.Entities;
using TaskTracker.Domain.Enums;

namespace TaskTracker.Application.Services;

public class AuthService(
    IUserRepository users,
    IPasswordHasher hasher,
    ITokenService tokens,
    IClock clock,
    IUnitOfWork unitOfWork,
    ProfileTemplateService templates,
    IValidator<RegisterRequest> registerValidator,
    IValidator<LoginRequest> loginValidator)
{
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        await registerValidator.ValidateAndThrowAsync(request, ct);

        var email = User.NormalizeEmail(request.Email);
        if (await users.EmailExistsAsync(email, ct))
        {
            throw new ConflictException("A user with this email already exists.");
        }

        var template = request.ProfileTemplate ?? ProfileTemplate.Universal;
        var user = User.Create(email, hasher.Hash(request.Password), request.DisplayName,
            request.TimeZoneId ?? "UTC", template, clock.UtcNow);

        users.Add(user);
        await templates.ApplyAsync(user, template, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return CreateResponse(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        await loginValidator.ValidateAndThrowAsync(request, ct);

        var user = await users.GetByEmailAsync(User.NormalizeEmail(request.Email), ct);
        if (user is null || !hasher.Verify(request.Password, user.PasswordHash))
        {
            throw new UnauthorizedException("Invalid email or password.");
        }

        return CreateResponse(user);
    }

    private AuthResponse CreateResponse(User user)
    {
        var token = tokens.Create(user);
        return new AuthResponse(token.Token, token.ExpiresAt, user.ToDto());
    }
}
