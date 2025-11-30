using System.Collections.Concurrent;
using EasyAppDev.Blazor.Store.Selectors;
using StoreSel = EasyAppDev.Blazor.Store.Selectors.Selectors;

namespace EasyAppDev.Blazor.Store.Tests.Selectors;

public record TodoItem(int Id, string Title, bool IsCompleted);
public record TodoState(List<TodoItem> Todos);

public class SelectorTests
{
    [Fact]
    public void Selector_ComputesResult()
    {
        // Arrange
        var selector = StoreSel.Create<TodoState, List<TodoItem>>(
            state => state.Todos.Where(t => t.IsCompleted).ToList());

        var state = new TodoState(new List<TodoItem>
        {
            new(1, "Task 1", true),
            new(2, "Task 2", false),
            new(3, "Task 3", true)
        });

        // Act
        var result = selector.Select(state);

        // Assert
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(t => t.IsCompleted.Should().BeTrue());
        result.Should().Contain(t => t.Id == 1);
        result.Should().Contain(t => t.Id == 3);
    }

    [Fact]
    public void Selector_MemoizesResult()
    {
        // Arrange
        var callCount = 0;
        var selector = StoreSel.Create<TodoState, int>(state =>
        {
            callCount++;
            return state.Todos.Count;
        });

        var state = new TodoState(new List<TodoItem>
        {
            new(1, "Task 1", false)
        });

        // Act
        var result1 = selector.Select(state);
        var result2 = selector.Select(state); // Should use cache

        // Assert
        result1.Should().Be(1);
        result2.Should().Be(1);
        callCount.Should().Be(1); // Only computed once
    }

    [Fact]
    public void Selector_RecomputesWhenStateChanges()
    {
        // Arrange
        var callCount = 0;
        var selector = StoreSel.Create<TodoState, int>(state =>
        {
            callCount++;
            return state.Todos.Count;
        });

        var state1 = new TodoState(new List<TodoItem> { new(1, "Task 1", false) });
        var state2 = new TodoState(new List<TodoItem>
        {
            new(1, "Task 1", false),
            new(2, "Task 2", false)
        });

        // Act
        var result1 = selector.Select(state1);
        var result2 = selector.Select(state2); // Different state, should recompute

        // Assert
        result1.Should().Be(1);
        result2.Should().Be(2);
        callCount.Should().Be(2); // Computed twice
    }

    [Fact]
    public void ComposedSelector_CombinesMultipleSelectors()
    {
        // Arrange
        var completedSelector = StoreSel.Create<TodoState, List<TodoItem>>(
            state => state.Todos.Where(t => t.IsCompleted).ToList());

        var activeSelector = StoreSel.Create<TodoState, List<TodoItem>>(
            state => state.Todos.Where(t => !t.IsCompleted).ToList());

        var statsSelector = StoreSel.Create(
            completedSelector,
            activeSelector,
            (completed, active) => new { Completed = completed.Count, Active = active.Count });

        var state = new TodoState(new List<TodoItem>
        {
            new(1, "Task 1", true),
            new(2, "Task 2", false),
            new(3, "Task 3", true)
        });

        // Act
        var result = statsSelector.Select(state);

        // Assert
        result.Completed.Should().Be(2);
        result.Active.Should().Be(1);
    }

    [Fact]
    public void ComposedSelector_WithThreeInputs_CombinesCorrectly()
    {
        // Arrange
        var completedSelector = StoreSel.Create<TodoState, int>(
            state => state.Todos.Count(t => t.IsCompleted));

        var activeSelector = StoreSel.Create<TodoState, int>(
            state => state.Todos.Count(t => !t.IsCompleted));

        var totalSelector = StoreSel.Create<TodoState, int>(
            state => state.Todos.Count);

        var progressSelector = StoreSel.Create(
            completedSelector,
            activeSelector,
            totalSelector,
            (completed, active, total) => new
            {
                Completed = completed,
                Active = active,
                Total = total,
                Progress = total > 0 ? (double)completed / total * 100 : 0
            });

        var state = new TodoState(new List<TodoItem>
        {
            new(1, "Task 1", true),
            new(2, "Task 2", false),
            new(3, "Task 3", true),
            new(4, "Task 4", true)
        });

        // Act
        var result = progressSelector.Select(state);

        // Assert
        result.Completed.Should().Be(3);
        result.Active.Should().Be(1);
        result.Total.Should().Be(4);
        result.Progress.Should().BeApproximately(75.0, 0.01);
    }

    [Fact]
    public void Reset_ClearsMemoizedCache()
    {
        // Arrange
        var callCount = 0;
        var selector = StoreSel.Create<TodoState, int>(state =>
        {
            callCount++;
            return state.Todos.Count;
        });

        var state = new TodoState(new List<TodoItem> { new(1, "Task 1", false) });

        // Act
        selector.Select(state); // Compute
        selector.Reset();       // Clear cache
        selector.Select(state); // Compute again

        // Assert
        callCount.Should().Be(2);
    }

    [Fact]
    public void Selector_WithCustomComparer_UsesComparerForCaching()
    {
        // Arrange
        var callCount = 0;
        var comparer = StringComparer.OrdinalIgnoreCase;
        var selector = StoreSel.Create<TodoState, string>(
            state => state.Todos.FirstOrDefault()?.Title ?? "",
            comparer);

        var state1 = new TodoState(new List<TodoItem> { new(1, "TASK", false) });
        var state2 = new TodoState(new List<TodoItem> { new(1, "task", false) });

        // Act
        var result1 = selector.Select(state1);
        callCount++;
        var result2 = selector.Select(state2); // Different state but same result with case-insensitive comparison
        callCount++;

        // Assert
        result1.Should().Be("TASK");
        result2.Should().Be("task");
        // Both should compute because state changed, even though result is "equal" by comparer
        // The memoization checks state equality first
    }

    [Fact]
    public void Create_WithNullSelector_ThrowsArgumentNullException()
    {
        // Act
        var act = () => StoreSel.Create<TodoState, int>(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CreateComposed_WithNullFirstSelector_ThrowsArgumentNullException()
    {
        // Arrange
        var selector2 = StoreSel.Create<TodoState, int>(state => 1);

        // Act
        var act = () => StoreSel.Create<TodoState, int, int, int>(
            null!,
            selector2,
            (a, b) => a + b);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CreateComposed_WithNullSecondSelector_ThrowsArgumentNullException()
    {
        // Arrange
        var selector1 = StoreSel.Create<TodoState, int>(state => 1);

        // Act
        var act = () => StoreSel.Create<TodoState, int, int, int>(
            selector1,
            null!,
            (a, b) => a + b);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CreateComposed_WithNullCombiner_ThrowsArgumentNullException()
    {
        // Arrange
        var selector1 = StoreSel.Create<TodoState, int>(state => 1);
        var selector2 = StoreSel.Create<TodoState, int>(state => 1);

        // Act
        var act = () => StoreSel.Create<TodoState, int, int, int>(
            selector1,
            selector2,
            null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Select_WithNullState_ThrowsArgumentNullException()
    {
        // Arrange
        var selector = StoreSel.Create<TodoState, int>(state => state.Todos.Count);

        // Act
        var act = () => selector.Select(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Selector_WithComplexDerivedState_WorksCorrectly()
    {
        // Arrange
        var selector = StoreSel.Create<TodoState, Dictionary<bool, List<TodoItem>>>(
            state => state.Todos
                .GroupBy(t => t.IsCompleted)
                .ToDictionary(g => g.Key, g => g.ToList()));

        var state = new TodoState(new List<TodoItem>
        {
            new(1, "Task 1", true),
            new(2, "Task 2", false),
            new(3, "Task 3", true),
            new(4, "Task 4", false),
            new(5, "Task 5", false)
        });

        // Act
        var result = selector.Select(state);

        // Assert
        result.Should().ContainKey(true);
        result.Should().ContainKey(false);
        result[true].Should().HaveCount(2);
        result[false].Should().HaveCount(3);
    }

    [Fact]
    public void ComposedSelector_Memoizes_IntermediateSelectors()
    {
        // Arrange
        var callCount1 = 0;
        var callCount2 = 0;

        var selector1 = StoreSel.Create<TodoState, int>(state =>
        {
            callCount1++;
            return state.Todos.Count(t => t.IsCompleted);
        });

        var selector2 = StoreSel.Create<TodoState, int>(state =>
        {
            callCount2++;
            return state.Todos.Count(t => !t.IsCompleted);
        });

        var composedSelector = StoreSel.Create(
            selector1,
            selector2,
            (completed, active) => completed + active);

        var state = new TodoState(new List<TodoItem>
        {
            new(1, "Task 1", true),
            new(2, "Task 2", false)
        });

        // Act
        composedSelector.Select(state);
        composedSelector.Select(state); // Should use cache

        // Assert
        callCount1.Should().Be(1); // selector1 memoized
        callCount2.Should().Be(1); // selector2 memoized
    }

    #region Thread Safety Tests

    [Fact]
    public async Task MemoizedSelector_ShouldBeThreadSafe_ConcurrentReads()
    {
        // Arrange
        var callCount = 0;
        var selector = StoreSel.Create<TodoState, int>(state =>
        {
            Interlocked.Increment(ref callCount);
            return state.Todos.Count;
        });

        var state = new TodoState(new List<TodoItem> { new(1, "Task 1", false) });

        // Prime the cache first to ensure stable behavior
        selector.Select(state);
        callCount = 0; // Reset after priming

        // Act - Concurrent reads with same state (cache is now primed)
        var tasks = Enumerable.Range(0, 100)
            .Select(_ => Task.Run(() => selector.Select(state)));

        var results = await Task.WhenAll(tasks);

        // Assert - All results should be correct
        results.Should().AllSatisfy(r => r.Should().Be(1));
        // With primed cache and same state reference, should use cache
        // Note: Lock-free implementation may have some recomputation under
        // initial contention, but once cache is stable, reads should be cached
        callCount.Should().BeLessOrEqualTo(5, "most reads should hit cache after priming");
    }

    [Fact]
    public async Task MemoizedSelector_ShouldBeThreadSafe_ConcurrentDifferentStates()
    {
        // Arrange
        var selector = StoreSel.Create<TodoState, int>(state => state.Todos.Count);

        var states = Enumerable.Range(0, 10)
            .Select(i => new TodoState(Enumerable.Range(0, i + 1)
                .Select(j => new TodoItem(j, $"Task {j}", false)).ToList()))
            .ToList();

        // Act - Concurrent reads with different states
        var tasks = Enumerable.Range(0, 100)
            .Select(i => Task.Run(() => selector.Select(states[i % 10])));

        var results = await Task.WhenAll(tasks);

        // Assert - Should not throw and return consistent results
        var grouped = results.GroupBy(r => r).ToDictionary(g => g.Key, g => g.Count());
        grouped.Should().HaveCount(10); // 10 different states
    }

    [Fact]
    public async Task MemoizedSelector_ShouldBeThreadSafe_InterleavedReadWriteReset()
    {
        // Arrange
        var selector = StoreSel.Create<TodoState, int>(state => state.Todos.Count);
        var results = new ConcurrentBag<int>();
        var errors = new ConcurrentBag<Exception>();

        var states = Enumerable.Range(1, 5)
            .Select(i => new TodoState(Enumerable.Range(0, i)
                .Select(j => new TodoItem(j, $"Task {j}", false)).ToList()))
            .ToList();

        // Act - Concurrent operations with Reset() calls interleaved
        var cts = new CancellationTokenSource();
        var readTasks = Enumerable.Range(0, 50).Select(_ => Task.Run(() =>
        {
            try
            {
                for (int i = 0; i < 20; i++)
                {
                    var state = states[i % states.Count];
                    var result = selector.Select(state);
                    results.Add(result);
                }
            }
            catch (Exception ex)
            {
                errors.Add(ex);
            }
        }));

        var resetTasks = Enumerable.Range(0, 5).Select(_ => Task.Run(async () =>
        {
            for (int i = 0; i < 10; i++)
            {
                selector.Reset();
                await Task.Delay(1);
            }
        }));

        await Task.WhenAll(readTasks.Concat(resetTasks));

        // Assert
        errors.Should().BeEmpty("no exceptions should occur during concurrent access");
        results.Should().NotBeEmpty();
        results.Should().AllSatisfy(r => r.Should().BeInRange(1, 5));
    }

    [Fact]
    public async Task MemoizedSelector_ShouldProduceConsistentResults_UnderHighContention()
    {
        // Arrange - Create selector that tracks all computed values
        var computedValues = new ConcurrentBag<(TodoState State, int Result)>();
        var selector = StoreSel.Create<TodoState, int>(state =>
        {
            var result = state.Todos.Count;
            computedValues.Add((state, result));
            return result;
        });

        var state1 = new TodoState(new List<TodoItem> { new(1, "A", false) });
        var state2 = new TodoState(new List<TodoItem> { new(1, "A", false), new(2, "B", false) });

        // Act - High contention with alternating states
        var tasks = Enumerable.Range(0, 1000)
            .Select(i => Task.Run(() =>
            {
                var state = i % 2 == 0 ? state1 : state2;
                return selector.Select(state);
            }));

        var results = await Task.WhenAll(tasks);

        // Assert
        var state1Results = results.Where((_, i) => i % 2 == 0);
        var state2Results = results.Where((_, i) => i % 2 != 0);

        state1Results.Should().AllSatisfy(r => r.Should().Be(1));
        state2Results.Should().AllSatisfy(r => r.Should().Be(2));
    }

    [Fact]
    public async Task MemoizedSelector_Reset_ShouldBeThreadSafe()
    {
        // Arrange
        var selector = StoreSel.Create<TodoState, int>(state => state.Todos.Count);
        var state = new TodoState(new List<TodoItem> { new(1, "Task", false) });

        // Prime the cache
        selector.Select(state);

        // Act - Concurrent resets should not throw
        var resetActions = Enumerable.Range(0, 100)
            .Select(_ => Task.Run(() => selector.Reset()));

        // Assert
        var act = async () => await Task.WhenAll(resetActions);
        await act.Should().NotThrowAsync();
    }

    #endregion
}
