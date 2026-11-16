using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TaskTracker.Application.Abstractions;
using TaskTracker.Infrastructure.Persistence;
using TaskTracker.Infrastructure.Repositories;
using TaskTracker.Infrastructure.Security;
using TaskTracker.Infrastructure.Seed;

namespace TaskTracker.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        // The connection string is read lazily, when the first DbContext is created.
        services.AddDbContext<AppDbContext>((provider, options) =>
        {
            var connectionString = provider.GetRequiredService<IConfiguration>().GetConnectionString("Default")
                                   ?? throw new InvalidOperationException(
                                       "The connection string 'ConnectionStrings__Default' is not configured.");
            options.UseNpgsql(connectionString);
        });

        services.AddOptions<JwtOptions>()
            .Bind(config.GetSection("Jwt"))
            .Validate(o => o.Secret.Length >= JwtOptions.MinSecretLength,
                $"Jwt:Secret (JWT__Secret) must be at least {JwtOptions.MinSecretLength} characters long.")
            .Validate(o => o.ExpiresMinutes > 0, "Jwt:ExpiresMinutes must be positive.")
            .ValidateOnStart();

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();

        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ITaskRepository, TaskRepository>();
        services.AddScoped<ITagRepository, TagRepository>();
        services.AddScoped<ICustomFieldRepository, CustomFieldRepository>();
        services.AddScoped<DemoDataSeeder>();
        return services;
    }
}
