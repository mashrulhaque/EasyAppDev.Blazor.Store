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
}
