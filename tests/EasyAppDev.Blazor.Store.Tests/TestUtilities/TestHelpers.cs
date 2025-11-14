using System.Diagnostics;
using EasyAppDev.Blazor.Store.Core;

namespace EasyAppDev.Blazor.Store.Tests.TestUtilities;

/// <summary>
/// Utility methods and helpers for testing the store library.
/// </summary>
public static class TestHelpers
{
    /// <summary>
    /// Creates a store with test defaults.
    /// </summary>
    /// <typeparam name="TState">The type of state to manage.</typeparam>
    /// <param name="initialState">The initial state.</param>
    /// <param name="configure">Optional configuration action.</param>
    /// <returns>A configured store instance.</returns>
    public static IStore<TState> CreateTestStore<TState>(
        TState initialState,
        Action<StoreBuilder<TState>>? configure = null)
        where TState : notnull
    {
        var builder = StoreBuilder<TState>.Create(initialState);
        configure?.Invoke(builder);
        return builder.Build();
    }

    /// <summary>
    /// Waits for a condition to be true within a timeout.
    /// </summary>
    /// <param name="condition">The condition to check.</param>
    /// <param name="timeout">The maximum time to wait.</param>
    /// <returns>True if the condition became true, false if timeout occurred.</returns>
    public static async Task<bool> WaitForConditionAsync(
        Func<bool> condition,
        TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            if (condition())
                return true;

            await Task.Delay(10);
        }
        return false;
    }

    /// <summary>
    /// Waits for a condition to be true within a timeout.
    /// </summary>
    /// <param name="condition">The async condition to check.</param>
    /// <param name="timeout">The maximum time to wait.</param>
    /// <returns>True if the condition became true, false if timeout occurred.</returns>
    public static async Task<bool> WaitForConditionAsync(
        Func<Task<bool>> condition,
        TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            if (await condition())
                return true;

            await Task.Delay(10);
        }
        return false;
    }

    /// <summary>
    /// Collects all state changes for testing.
    /// </summary>
    /// <typeparam name="TState">The type of state to collect.</typeparam>
    /// <param name="store">The store to collect changes from.</param>
    /// <returns>A state change collector instance.</returns>
    public static StateChangeCollector<TState> CollectStateChanges<TState>(
        IStore<TState> store)
        where TState : notnull
    {
        return new StateChangeCollector<TState>(store);
    }

    /// <summary>
    /// Creates a mock updater function that tracks its invocations.
    /// </summary>
    /// <typeparam name="TState">The type of state.</typeparam>
    /// <param name="updater">The actual updater function.</param>
    /// <returns>A tracked updater function and invocation counter.</returns>
    public static (Func<TState, TState> Updater, Func<int> GetInvocationCount) CreateTrackedUpdater<TState>(
        Func<TState, TState> updater)
        where TState : notnull
    {
        var count = 0;
        return (state =>
        {
            count++;
            return updater(state);
        }, () => count);
    }

    /// <summary>
    /// Measures the execution time of an action.
    /// </summary>
    /// <param name="action">The action to measure.</param>
    /// <returns>The elapsed time in milliseconds.</returns>
    public static long MeasureExecutionTime(Action action)
    {
        var sw = Stopwatch.StartNew();
        action();
        sw.Stop();
        return sw.ElapsedMilliseconds;
    }

    /// <summary>
    /// Measures the execution time of an async action.
    /// </summary>
    /// <param name="action">The async action to measure.</param>
    /// <returns>The elapsed time in milliseconds.</returns>
    public static async Task<long> MeasureExecutionTimeAsync(Func<Task> action)
    {
        var sw = Stopwatch.StartNew();
        await action();
        sw.Stop();
        return sw.ElapsedMilliseconds;
    }

    /// <summary>
    /// Creates a deterministic delay for testing timing-sensitive code.
    /// </summary>
    /// <param name="milliseconds">The delay duration in milliseconds.</param>
    /// <returns>A task that completes after the specified delay.</returns>
    public static Task DelayAsync(int milliseconds)
    {
        return Task.Delay(milliseconds);
    }

    /// <summary>
    /// Executes an action multiple times and returns all results.
    /// </summary>
    /// <typeparam name="T">The type of result.</typeparam>
    /// <param name="action">The action to execute.</param>
    /// <param name="count">The number of times to execute.</param>
    /// <returns>A list of all results.</returns>
    public static List<T> ExecuteMultiple<T>(Func<T> action, int count)
    {
        var results = new List<T>(count);
        for (int i = 0; i < count; i++)
        {
            results.Add(action());
        }
        return results;
    }

    /// <summary>
    /// Executes an async action multiple times and returns all results.
    /// </summary>
    /// <typeparam name="T">The type of result.</typeparam>
    /// <param name="action">The async action to execute.</param>
    /// <param name="count">The number of times to execute.</param>
    /// <returns>A list of all results.</returns>
    public static async Task<List<T>> ExecuteMultipleAsync<T>(Func<Task<T>> action, int count)
    {
        var results = new List<T>(count);
        for (int i = 0; i < count; i++)
        {
            results.Add(await action());
        }
        return results;
    }
}

/// <summary>
/// Collects state changes from a store for testing purposes.
/// </summary>
/// <typeparam name="TState">The type of state to collect.</typeparam>
public class StateChangeCollector<TState> : IDisposable where TState : notnull
{
    private readonly List<TState> _states = new();
    private readonly IDisposable _subscription;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="StateChangeCollector{TState}"/> class.
    /// </summary>
    /// <param name="store">The store to collect changes from.</param>
    public StateChangeCollector(IStore<TState> store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _subscription = store.Subscribe(state => _states.Add(state));
    }

    /// <summary>
    /// Gets the collected states.
    /// </summary>
    public IReadOnlyList<TState> States => _states;

    /// <summary>
    /// Gets the number of collected state changes.
    /// </summary>
    public int Count => _states.Count;

    /// <summary>
    /// Gets the most recent state.
    /// </summary>
    public TState? Latest => _states.Count > 0 ? _states[^1] : default;

    /// <summary>
    /// Gets the first state.
    /// </summary>
    public TState? First => _states.Count > 0 ? _states[0] : default;

    /// <summary>
    /// Clears all collected states.
    /// </summary>
    public void Clear()
    {
        _states.Clear();
    }

    /// <summary>
    /// Waits for a specific number of state changes.
    /// </summary>
    /// <param name="expectedCount">The expected number of changes.</param>
    /// <param name="timeout">The maximum time to wait.</param>
    /// <returns>True if the expected count was reached, false if timeout occurred.</returns>
    public async Task<bool> WaitForCountAsync(int expectedCount, TimeSpan timeout)
    {
        return await TestHelpers.WaitForConditionAsync(
            () => _states.Count >= expectedCount,
            timeout);
    }

    /// <summary>
    /// Disposes the collector and unsubscribes from the store.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _subscription.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
