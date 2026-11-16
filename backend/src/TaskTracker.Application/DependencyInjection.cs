using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using TaskTracker.Application.Validators;

namespace TaskTracker.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();

        // Every class named "*Service" in the Services namespace is a use case and is registered as scoped.
        // A new service therefore needs no change in this file.
        var serviceTypes = typeof(DependencyInjection).Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false, Namespace: "TaskTracker.Application.Services" }
                           && type.Name.EndsWith("Service", StringComparison.Ordinal));
        foreach (var serviceType in serviceTypes)
        {
            services.AddScoped(serviceType);
        }

        return services;
    }
}
