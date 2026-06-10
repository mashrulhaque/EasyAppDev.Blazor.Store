// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

using EasyAppDev.Blazor.Store.Core;

namespace EasyAppDev.Blazor.Store.Middleware;

/// <summary>
/// A middleware that requires a reference to the store it is attached to.
/// </summary>
/// <typeparam name="TState">The type of state.</typeparam>
/// <remarks>
/// <see cref="StoreBuilder{TState}.Build"/> calls <see cref="AttachStore"/> on every
/// registered middleware implementing this interface immediately after the store is
/// constructed. Implementations must be idempotent: <see cref="AttachStore"/> may be
/// called more than once (e.g. when registration helpers also attach explicitly).
/// </remarks>
public interface IStoreAwareMiddleware<TState> : IMiddleware<TState> where TState : notnull
{
    /// <summary>
    /// Attaches the store this middleware belongs to. Must be idempotent.
    /// </summary>
    /// <param name="store">The store instance that owns this middleware.</param>
    void AttachStore(IStore<TState> store);
}
