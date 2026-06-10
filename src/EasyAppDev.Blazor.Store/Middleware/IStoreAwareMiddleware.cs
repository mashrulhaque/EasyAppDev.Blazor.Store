// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

using EasyAppDev.Blazor.Store.Core;

namespace EasyAppDev.Blazor.Store.Middleware;

/// <summary>
/// A middleware that needs a reference to the store it is attached to
/// (for example to apply externally received updates or to capture the
/// initial state). <see cref="StoreBuilder{TState}.Build"/> calls
/// <see cref="AttachStore"/> automatically after the store is constructed.
/// </summary>
/// <typeparam name="TState">The type of state managed by the store.</typeparam>
public interface IStoreAwareMiddleware<TState> where TState : notnull
{
    /// <summary>
    /// Called once after the store is constructed. Implementations must be
    /// idempotent: a second call with the same store must be a no-op.
    /// </summary>
    /// <param name="store">The store this middleware is attached to.</param>
    void AttachStore(IStore<TState> store);
}
