using EasyAppDev.Blazor.Store.Core;
using EasyAppDev.Blazor.Store.Security;

namespace EasyAppDev.Blazor.Store.TabSync;

/// <summary>
/// Extension methods for adding cross-tab state synchronization.
/// </summary>
public static class TabSyncExtensions
{
    /// <summary>
    /// Adds cross-tab state synchronization using BroadcastChannel API.
    /// </summary>
    /// <typeparam name="TState">The type of state managed by the store.</typeparam>
    /// <param name="builder">The store builder.</param>
    /// <param name="serviceProvider">Service provider for IJSRuntime access.</param>
    /// <param name="configure">Optional action to configure sync options.</param>
    /// <returns>The builder instance for chaining.</returns>
    /// <remarks>
    /// Tab sync only works in WebAssembly. In Blazor Server, each user
    /// already has their own state isolated by SignalR circuit.
    /// </remarks>
    /// <example>
    /// <code>
    /// builder.Services.AddStore(
    ///     CartState.Empty,
    ///     (store, sp) => store
    ///         .WithDevTools(sp, "Cart")
    ///         .WithTabSync(sp, opts => opts
    ///             .Channel("cart-sync")
    ///             .ExcludeActions("UI_STATE")
    ///             .OnReceived(action => Console.WriteLine($"Received: {action}"))
    ///         )
    /// );
    /// </code>
    /// </example>
    public static StoreBuilder<TState> WithTabSync<TState>(
        this StoreBuilder<TState> builder,
        IServiceProvider serviceProvider,
        Action<TabSyncOptions>? configure = null)
        where TState : notnull
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        var options = new TabSyncOptions();
        configure?.Invoke(options);

        // Propagate validator from builder if not explicitly set in options
        if (options.StateValidator == null && builder.StateValidator != null)
        {
            // Wrap the typed validator for the untyped TabSyncOptions.StateValidator
            options.StateValidator = new StateValidatorWrapper<TState>(builder.StateValidator);
        }

        var middleware = new TabSyncMiddleware<TState>(serviceProvider, options);
        return builder.WithMiddleware(middleware);
    }

    /// <summary>
    /// Adds cross-tab state synchronization with a specific channel name.
    /// </summary>
    /// <typeparam name="TState">The type of state managed by the store.</typeparam>
    /// <param name="builder">The store builder.</param>
    /// <param name="serviceProvider">Service provider for IJSRuntime access.</param>
    /// <param name="channelName">The BroadcastChannel name to use.</param>
    /// <returns>The builder instance for chaining.</returns>
    public static StoreBuilder<TState> WithTabSync<TState>(
        this StoreBuilder<TState> builder,
        IServiceProvider serviceProvider,
        string channelName)
        where TState : notnull
    {
        return builder.WithTabSync(serviceProvider, opts => opts.Channel(channelName));
    }
}

/// <summary>
/// Extension methods for configuring TabSync message signing.
/// </summary>
public static class TabSyncSecurityExtensions
{
    /// <summary>
    /// Enables message signing with a shared signing key for cross-tab verification.
    /// </summary>
    /// <param name="options">The TabSync options to configure.</param>
    /// <param name="signingKey">The shared signing key (must be at least 32 bytes).</param>
    /// <returns>The options instance for chaining.</returns>
    /// <remarks>
    /// Use <see cref="MessageSigner.DeriveKeyFromSeed"/> to generate a consistent key
    /// from a seed string (e.g., your application name).
    /// </remarks>
    /// <example>
    /// <code>
    /// builder.Services.AddStore(
    ///     new AppState(),
    ///     (store, sp) => store.WithTabSync(sp, opts => opts
    ///         .WithSharedSigningKey(MessageSigner.DeriveKeyFromSeed("MyApp"))
    ///     )
    /// );
    /// </code>
    /// </example>
    public static TabSyncOptions WithSharedSigningKey(this TabSyncOptions options, byte[] signingKey)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(signingKey);
        if (signingKey.Length < 32)
            throw new ArgumentException("Signing key must be at least 32 bytes", nameof(signingKey));

        options.EnableMessageSigning = true;
        options.SigningKey = signingKey;
        return options;
    }

    /// <summary>
    /// Enables message signing with automatic key derivation from window.location.origin.
    /// All tabs from the same origin will share the same signing key.
    /// </summary>
    /// <param name="options">The TabSync options to configure.</param>
    /// <returns>The options instance for chaining.</returns>
    /// <remarks>
    /// This provides a balance between security and convenience for same-origin tabs.
    /// The key is derived using PBKDF2 with the origin as both password and salt source.
    /// For higher security, use <see cref="WithSharedSigningKey"/> with a server-provided session key.
    /// </remarks>
    /// <example>
    /// <code>
    /// builder.Services.AddStore(
    ///     new AppState(),
    ///     (store, sp) => store.WithTabSync(sp, opts => opts
    ///         .WithOriginDerivedKey()
    ///     )
    /// );
    /// </code>
    /// </example>
    public static TabSyncOptions WithOriginDerivedKey(this TabSyncOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.EnableMessageSigning = true;
        options.DeriveKeyFromOrigin = true;
        return options;
    }

    /// <summary>
    /// Enables message signing with a key derived from a custom seed string.
    /// </summary>
    /// <param name="options">The TabSync options to configure.</param>
    /// <param name="seed">The seed string to derive the key from.</param>
    /// <param name="iterations">Number of PBKDF2 iterations (default: 10000).</param>
    /// <returns>The options instance for chaining.</returns>
    /// <remarks>
    /// This is a convenience method that derives the key immediately.
    /// The seed should be consistent across all instances that need to communicate.
    /// </remarks>
    /// <example>
    /// <code>
    /// builder.Services.AddStore(
    ///     new AppState(),
    ///     (store, sp) => store.WithTabSync(sp, opts => opts
    ///         .WithDerivedSigningKey("MyApp-ProductionEnvironment")
    ///     )
    /// );
    /// </code>
    /// </example>
    public static TabSyncOptions WithDerivedSigningKey(this TabSyncOptions options, string seed, int iterations = 10000)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(seed);

        var signingKey = MessageSigner.DeriveKeyFromSeed(seed, iterations);
        return options.WithSharedSigningKey(signingKey);
    }

    /// <summary>
    /// Disables message signing (default behavior).
    /// </summary>
    /// <param name="options">The TabSync options to configure.</param>
    /// <returns>The options instance for chaining.</returns>
    /// <remarks>
    /// Use this to explicitly disable signing if it was previously enabled.
    /// Without signing, messages can be tampered with by malicious scripts.
    /// </remarks>
    public static TabSyncOptions WithoutMessageSigning(this TabSyncOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.EnableMessageSigning = false;
        options.SigningKey = null;
        options.DeriveKeyFromOrigin = false;
        return options;
    }
}
