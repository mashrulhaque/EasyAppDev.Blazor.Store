// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

using EasyAppDev.Blazor.Store.AsyncActions;
using EasyAppDev.Blazor.Store.Blazor;
using EasyAppDev.Blazor.Store.Core;
using EasyAppDev.Blazor.Store.History;
using EasyAppDev.Blazor.Store.Middleware;
using EasyAppDev.Blazor.Store.Persistence;
using EasyAppDev.Blazor.Store.TabSync;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace EasyAppDev.Blazor.Store.Security;

/// <summary>
/// Extension methods for registering secure stores with sensible defaults.
/// Provides a streamlined API for configuring stores with appropriate security settings.
/// </summary>
public static class SecureStoreServiceExtensions
{
    /// <summary>
    /// Adds a secure store with automatic security configuration based on environment.
    /// This is the recommended way to register stores for most applications.
    /// </summary>
    /// <typeparam name="TState">The type of state managed by the store.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="initialState">The initial state.</param>
    /// <param name="storeName">The store name for DevTools and logging.</param>
    /// <param name="configure">Optional configuration for additional options.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="SecurityConfigurationException">
    /// Thrown when security configuration is invalid for the selected profile.
    /// </exception>
    public static IServiceCollection AddSecureStore<TState>(
        this IServiceCollection services,
        TState initialState,
        string storeName,
        Action<SecureStoreOptions>? configure = null)
        where TState : notnull
    {
        var options = new SecureStoreOptions { StoreName = storeName };
        configure?.Invoke(options);
        options.AdditionalConfiguration?.Invoke(options);

        return AddSecureStoreInternal(services, initialState, options);
    }

    /// <summary>
    /// Adds a secure store with automatic security configuration based on environment.
    /// Uses the state type name as the store name.
    /// </summary>
    /// <typeparam name="TState">The type of state managed by the store.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="initialState">The initial state.</param>
    /// <param name="configure">Optional configuration for additional options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSecureStore<TState>(
        this IServiceCollection services,
        TState initialState,
        Action<SecureStoreOptions>? configure = null)
        where TState : notnull
    {
        var options = new SecureStoreOptions();
        configure?.Invoke(options);
        options.AdditionalConfiguration?.Invoke(options);

        options.StoreName ??= typeof(TState).Name.Replace("State", "");

        return AddSecureStoreInternal(services, initialState, options);
    }

    private static IServiceCollection AddSecureStoreInternal<TState>(
        IServiceCollection services,
        TState initialState,
        SecureStoreOptions options)
        where TState : notnull
    {
        // Register utilities
        services.AddStoreUtilities();
        services.AddAsyncActionExecutor<TState>();

        // Build the store configuration
        StoreBuilder<TState> ConfigureStore(StoreBuilder<TState> builder, IServiceProvider sp)
        {
            return ApplySecureConfiguration(builder, sp, options);
        }

        // Register as singleton or scoped based on options
        if (options.UseScoped)
        {
            services.AddScoped<IStore<TState>>(sp =>
            {
                var builder = StoreBuilder<TState>.Create(initialState);
                builder = ApplySecureConfiguration(builder, sp, options);
                return builder.Build();
            });
            services.AddScoped<IStateReader<TState>>(sp => sp.GetRequiredService<IStore<TState>>());
            services.AddScoped<IStateWriter<TState>>(sp => sp.GetRequiredService<IStore<TState>>());
            services.AddScoped<IStateObservable<TState>>(sp => sp.GetRequiredService<IStore<TState>>());
        }
        else
        {
            services.AddStore(initialState, ConfigureStore);
        }

        return services;
    }

    private static StoreBuilder<TState> ApplySecureConfiguration<TState>(
        StoreBuilder<TState> builder,
        IServiceProvider sp,
        SecureStoreOptions options)
        where TState : notnull
    {
        var logger = sp.GetService<ILogger<IStore<TState>>>();
        var isProduction = EnvironmentDetection.IsProduction(sp);
        var profile = options.Profile ?? EnvironmentDetection.GetDefaultProfile(sp);
        var storeName = options.StoreName ?? typeof(TState).Name.Replace("State", "");

        // Validate configuration based on profile
        ValidateConfiguration<TState>(sp, options, profile, isProduction);

        // Apply profile-specific configuration
        builder = ApplyProfileConfiguration(builder, sp, options, profile, storeName, isProduction, logger);

        return builder;
    }

    private static void ValidateConfiguration<TState>(
        IServiceProvider sp,
        SecureStoreOptions options,
        SecurityProfile profile,
        bool isProduction)
        where TState : notnull
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        // Check validator requirement
        var requireValidator = options.RequireValidator
            ?? (profile == SecurityProfile.Production || profile == SecurityProfile.Strict);

        if (requireValidator)
        {
            var validator = sp.GetService<IStateValidator<TState>>();
            if (validator == null)
            {
                var message = $"No IStateValidator<{typeof(TState).Name}> registered. " +
                    $"Profile '{profile}' requires state validation.";

                if (profile == SecurityProfile.Strict)
                {
                    errors.Add(message);
                }
                else
                {
                    warnings.Add(message + " Consider registering a validator with AddStateValidator<TState, TValidator>().");
                }
            }
        }

        // Check signing key for TabSync
        if (options.EnableTabSync && isProduction)
        {
            if (options.SigningKey == null && profile != SecurityProfile.Development)
            {
                warnings.Add("TabSync enabled without explicit signing key. " +
                    "Key will be derived from origin. For maximum security, provide an explicit SigningKey.");
            }
        }

        // Check persistence key for production
        if (options.PersistenceKey != null && options.SigningKey == null && isProduction)
        {
            warnings.Add("Persistence enabled without permanent signing key. " +
                "State integrity cannot be verified after app restart. Consider providing a SigningKey.");
        }

        // Throw on errors
        if (errors.Count > 0)
        {
            throw new SecurityConfigurationException(
                "SecureStore",
                $"Security configuration errors for {typeof(TState).Name}",
                profile,
                errors,
                warnings);
        }

        // Throw on warnings if configured to do so
        var throwOnWarnings = options.ThrowOnSecurityWarnings
            ?? (profile == SecurityProfile.Strict);

        if (throwOnWarnings && warnings.Count > 0)
        {
            throw new SecurityConfigurationException(
                "SecureStore",
                $"Security configuration warnings for {typeof(TState).Name} (ThrowOnSecurityWarnings=true)",
                profile,
                null,
                warnings);
        }

        // Log warnings
        var warnLogger = sp.GetService<ILogger<SecureStoreOptions>>();
        foreach (var warning in warnings)
        {
            warnLogger?.LogWarning("[SecureStore] {Warning}", warning);
        }
    }

    private static StoreBuilder<TState> ApplyProfileConfiguration<TState>(
        StoreBuilder<TState> builder,
        IServiceProvider sp,
        SecureStoreOptions options,
        SecurityProfile profile,
        string storeName,
        bool isProduction,
        ILogger? logger)
        where TState : notnull
    {
        // Get validator from DI if available
        var validator = sp.GetService<IStateValidator<TState>>();
        if (validator != null)
        {
            builder = builder.WithStateValidator(validator);
        }

        // Apply logging based on profile
        switch (profile)
        {
            case SecurityProfile.Development:
                builder = builder.WithLogging();
#if DEBUG
                builder = builder.WithDevTools(sp, storeName);
#endif
                break;

            case SecurityProfile.Production:
            case SecurityProfile.Strict:
                builder = builder.WithLogging();
                break;

            case SecurityProfile.Custom:
                // No automatic configuration
                break;
        }

        // Apply persistence if configured
        if (options.PersistenceKey != null)
        {
            var jsRuntime = sp.GetService<IJSRuntime>();
            if (jsRuntime != null)
            {
                var persistenceProvider = new LocalStorageProvider(jsRuntime);
                var persistenceOptions = new PersistenceOptions<TState>
                {
                    Key = options.PersistenceKey,
                    DebounceMs = options.PersistenceDebounceMs,
                    EnableIntegrityCheck = true,
                    SigningKey = options.SigningKey,
                    FilterSensitiveData = options.FilterSensitiveData,
                    StateValidator = validator,
                    RejectInvalidState = profile != SecurityProfile.Development
                };

                builder = builder.WithPersistence(persistenceProvider, persistenceOptions);
            }
        }

        // Apply TabSync if configured
        if (options.EnableTabSync)
        {
            builder = builder.WithTabSync(sp, tabSyncOpts =>
            {
                tabSyncOpts.ChannelName = options.TabSyncChannel ?? $"{storeName}-sync";
                tabSyncOpts.EnableMessageSigning = profile != SecurityProfile.Development || options.SigningKey != null;
                tabSyncOpts.DeriveKeyFromOrigin = options.SigningKey == null;
                tabSyncOpts.SigningKey = options.SigningKey;
                tabSyncOpts.RequireValidSignature = profile != SecurityProfile.Development;
                tabSyncOpts.ValidateTimestamp = true;
                tabSyncOpts.MaxMessageAgeSeconds = profile == SecurityProfile.Strict ? 10 : 30;
                tabSyncOpts.RejectInvalidState = profile != SecurityProfile.Development;
                tabSyncOpts.FailFastOnInsecureConfiguration = profile == SecurityProfile.Strict;
            });
        }

        // Apply history if configured
        if (options.EnableHistory)
        {
            builder = builder.WithMiddleware(new StoreHistory<TState>(
                new HistoryOptions()
                    .WithMaxSize(options.MaxHistoryEntries)
                    .WithMaxMemoryMB(options.MaxHistoryMemoryMB)));
        }

        return builder;
    }
}

/// <summary>
/// Extension methods for StoreBuilder to apply security profiles.
/// </summary>
public static class SecureStoreBuilderExtensions
{
    /// <summary>
    /// Applies a security profile to the store builder.
    /// </summary>
    /// <typeparam name="TState">The type of state managed by the store.</typeparam>
    /// <param name="builder">The store builder.</param>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="profile">The security profile to apply.</param>
    /// <param name="storeName">The store name for DevTools and logging.</param>
    /// <returns>The builder instance for chaining.</returns>
    public static StoreBuilder<TState> WithSecurityProfile<TState>(
        this StoreBuilder<TState> builder,
        IServiceProvider serviceProvider,
        SecurityProfile profile,
        string? storeName = null)
        where TState : notnull
    {
        storeName ??= typeof(TState).Name.Replace("State", "");
        var logger = serviceProvider.GetService<ILogger<IStore<TState>>>();
        var validator = serviceProvider.GetService<IStateValidator<TState>>();

        // Apply validator if available
        if (validator != null)
        {
            builder = builder.WithStateValidator(validator);
        }

        switch (profile)
        {
            case SecurityProfile.Development:
                builder = builder.WithLogging();
#if DEBUG
                builder = builder.WithDevTools(serviceProvider, storeName);
#endif
                break;

            case SecurityProfile.Production:
                if (validator == null)
                {
                    logger?.LogWarning(
                        "[{StoreName}] Production profile without state validator. " +
                        "Consider registering IStateValidator<{StateType}>.",
                        storeName,
                        typeof(TState).Name);
                }
                builder = builder.WithLogging();
                break;

            case SecurityProfile.Strict:
                if (validator == null)
                {
                    throw SecurityConfigurationException.MissingValidator(
                        typeof(TState).Name,
                        profile);
                }
                builder = builder.WithLogging();
                break;

            case SecurityProfile.Custom:
                // No automatic configuration
                break;
        }

        return builder;
    }

    /// <summary>
    /// Applies environment-appropriate defaults to the store builder.
    /// Automatically detects Development vs Production environment.
    /// </summary>
    /// <typeparam name="TState">The type of state managed by the store.</typeparam>
    /// <param name="builder">The store builder.</param>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="storeName">The store name for DevTools and logging.</param>
    /// <returns>The builder instance for chaining.</returns>
    public static StoreBuilder<TState> WithEnvironmentDefaults<TState>(
        this StoreBuilder<TState> builder,
        IServiceProvider serviceProvider,
        string? storeName = null)
        where TState : notnull
    {
        var profile = EnvironmentDetection.GetDefaultProfile(serviceProvider);
        return builder.WithSecurityProfile(serviceProvider, profile, storeName);
    }
}
