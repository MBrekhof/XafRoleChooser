using Microsoft.Extensions.DependencyInjection;
using RoleChooser.Services;

namespace RoleChooser;

/// <summary>
/// Extension methods for registering RoleChooser services.
/// Call <c>services.AddRoleChooser()</c> in your application's Startup.cs / ConfigureServices.
/// </summary>
public static class RoleChooserServiceExtensions
{
    public static IServiceCollection AddRoleChooser(this IServiceCollection services)
    {
        services.AddScoped<IActiveRoleFilter, ActiveRoleFilter>();
        return services;
    }
}
