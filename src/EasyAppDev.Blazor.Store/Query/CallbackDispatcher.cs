// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

namespace EasyAppDev.Blazor.Store.Query;

/// <summary>
/// Invokes user callbacks safely, marshalling to a captured synchronization
/// context (e.g. the Blazor dispatcher) when one is available.
/// Callback exceptions are swallowed so they can never corrupt query/mutation
/// state or crash background timers.
/// </summary>
internal static class CallbackDispatcher
{
    /// <summary>
    /// Invokes the callback on the captured context if it differs from the
    /// current one; otherwise invokes inline. Exceptions are swallowed.
    /// </summary>
    public static void Invoke(SynchronizationContext? context, Action callback)
    {
        if (context is not null && !ReferenceEquals(context, SynchronizationContext.Current))
        {
            try
            {
                context.Post(static state =>
                {
                    try
                    {
                        ((Action)state!)();
                    }
                    catch
                    {
                        // User callback exceptions must not escape onto the context.
                    }
                }, callback);
            }
            catch
            {
                // Posting can fail if the context is torn down (e.g. circuit gone).
            }
        }
        else
        {
            try
            {
                callback();
            }
            catch
            {
                // User callback exceptions must not corrupt query/mutation state.
            }
        }
    }
}
