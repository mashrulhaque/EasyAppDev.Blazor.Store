// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

using EasyAppDev.Blazor.Store.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EasyAppDev.Blazor.Store.ServerSync;

/// <summary>
/// Extension methods for adding server sync to stores.
/// </summary>
public static class ServerSyncExtensions
{
    /// <summary>
    /// Enables server-side state synchronization via SignalR.
    /// </summary>
    /// <typeparam name="TState">The type of state.</typeparam>
    /// <param name="builder">The store builder.</param>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="configure">Action to configure server sync options.</param>
    /// <returns>The builder for chaining.</returns>
    /// <example>
    /// <code>
    /// builder.WithServerSync(sp, options =>
    /// {
    ///     options.HubUrl = "/hubs/state";
    ///     options.DocumentId = documentId;
    ///     options.EnablePresence = true;
    ///     options.OnUserJoined = user => Console.WriteLine($"{user.DisplayName} joined");
    /// });
    /// </code>
    /// </example>
    public static StoreBuilder<TState> WithServerSync<TState>(
        this StoreBuilder<TState> builder,
        IServiceProvider serviceProvider,
        Action<ServerSyncOptions<TState>> configure)
        where TState : notnull
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new ServerSyncOptions<TState> { HubUrl = "" };
        configure(options);

        if (string.IsNullOrEmpty(options.HubUrl))
        {
            throw new ArgumentException("HubUrl must be specified in server sync options");
        }

        var logger = serviceProvider.GetService<ILogger<ServerSyncMiddleware<TState>>>();
        var middleware = new ServerSyncMiddleware<TState>(options, logger);

        return builder.WithMiddleware(middleware);
    }

    /// <summary>
    /// Enables server-side state synchronization with minimal configuration.
    /// </summary>
    /// <typeparam name="TState">The type of state.</typeparam>
    /// <param name="builder">The store builder.</param>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="hubUrl">The SignalR hub URL.</param>
    /// <param name="documentId">Optional document ID for multi-tenant scenarios.</param>
    /// <returns>The builder for chaining.</returns>
    public static StoreBuilder<TState> WithServerSync<TState>(
        this StoreBuilder<TState> builder,
        IServiceProvider serviceProvider,
        string hubUrl,
        string? documentId = null)
        where TState : notnull
    {
        return builder.WithServerSync(serviceProvider, options =>
        {
            options.HubUrl = hubUrl;
            options.DocumentId = documentId;
        });
    }
}
