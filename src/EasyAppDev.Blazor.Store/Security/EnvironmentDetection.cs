// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

namespace EasyAppDev.Blazor.Store.Security;

/// <summary>
/// Utilities for detecting the runtime environment and configuring security accordingly.
/// </summary>
public static class EnvironmentDetection
{
    private const string AspNetCoreEnvironment = "ASPNETCORE_ENVIRONMENT";
    private const string DotNetEnvironment = "DOTNET_ENVIRONMENT";

    /// <summary>
    /// Determines if the application is running in a production environment.
    /// </summary>
    /// <param name="serviceProvider">Optional service provider (reserved for future use with IHostEnvironment).</param>
    /// <returns>True if running in production, false otherwise.</returns>
    public static bool IsProduction(IServiceProvider? serviceProvider = null)
    {
        // Check environment variables
        var env = Environment.GetEnvironmentVariable(AspNetCoreEnvironment)
            ?? Environment.GetEnvironmentVariable(DotNetEnvironment);

        // If no environment is set, assume production for security
        if (string.IsNullOrEmpty(env))
        {
            return true;
        }

        return !env.Equals("Development", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determines if the application is running in a development environment.
    /// </summary>
    /// <param name="serviceProvider">Optional service provider (reserved for future use).</param>
    /// <returns>True if running in development, false otherwise.</returns>
    public static bool IsDevelopment(IServiceProvider? serviceProvider = null)
    {
        return !IsProduction(serviceProvider);
    }

    /// <summary>
    /// Gets the appropriate security profile based on the current environment.
    /// </summary>
    /// <param name="serviceProvider">Optional service provider (reserved for future use).</param>
    /// <returns>Development profile for development environment, Production otherwise.</returns>
    public static SecurityProfile GetDefaultProfile(IServiceProvider? serviceProvider = null)
    {
        return IsDevelopment(serviceProvider)
            ? SecurityProfile.Development
            : SecurityProfile.Production;
    }

    /// <summary>
    /// Gets the environment name for logging purposes.
    /// </summary>
    /// <param name="serviceProvider">Optional service provider (reserved for future use).</param>
    /// <returns>The environment name.</returns>
    public static string GetEnvironmentName(IServiceProvider? serviceProvider = null)
    {
        return Environment.GetEnvironmentVariable(AspNetCoreEnvironment)
            ?? Environment.GetEnvironmentVariable(DotNetEnvironment)
            ?? "Production";
    }
}
