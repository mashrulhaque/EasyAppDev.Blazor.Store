using System.Collections.Immutable;

namespace EasyAppDev.Blazor.Store.Sample.State;

/// <summary>
/// Represents a single todo item.
/// </summary>
/// <param name="Id">Unique identifier for the todo.</param>
/// <param name="Title">The todo title/description.</param>
/// <param name="IsCompleted">Whether the todo is completed.</param>
/// <param name="CreatedAt">When the todo was created.</param>
public record TodoItem(
    Guid Id,
    string Title,
    bool IsCompleted = false,
    DateTime? CreatedAt = null);

/// <summary>
/// State for the todo list example - demonstrates working with immutable collections.
/// </summary>
/// <param name="Todos">The immutable list of todos.</param>
/// <param name="Filter">Current filter (All, Active, Completed).</param>
public record TodoState(
    ImmutableList<TodoItem> Todos,
    string Filter = "All")
{
    /// <summary>
    /// Creates an empty todo state.
    /// </summary>
    public static TodoState Empty => new(ImmutableList<TodoItem>.Empty);

    /// <summary>
    /// Adds a new todo item.
    /// </summary>
    public TodoState AddTodo(string title) => this with
    {
        Todos = Todos.Add(new TodoItem(
            Id: Guid.NewGuid(),
            Title: title,
            IsCompleted: false,
            CreatedAt: DateTime.Now))
    };

    /// <summary>
    /// Toggles the completion status of a todo.
    /// </summary>
    public TodoState ToggleTodo(Guid id)
    {
        var index = Todos.FindIndex(t => t.Id == id);
        if (index < 0) return this;

        var todo = Todos[index];
        return this with
        {
            Todos = Todos.SetItem(index, todo with { IsCompleted = !todo.IsCompleted })
        };
    }

    /// <summary>
    /// Removes a todo item.
    /// </summary>
    public TodoState RemoveTodo(Guid id) => this with
    {
        Todos = Todos.RemoveAll(t => t.Id == id)
    };

    /// <summary>
    /// Updates a todo's title.
    /// </summary>
    public TodoState UpdateTodo(Guid id, string newTitle)
    {
        var index = Todos.FindIndex(t => t.Id == id);
        if (index < 0) return this;

        var todo = Todos[index];
        return this with
        {
            Todos = Todos.SetItem(index, todo with { Title = newTitle })
        };
    }

    /// <summary>
    /// Clears all completed todos.
    /// </summary>
    public TodoState ClearCompleted() => this with
    {
        Todos = Todos.RemoveAll(t => t.IsCompleted)
    };

    /// <summary>
    /// Sets the filter for displaying todos.
    /// </summary>
    public TodoState SetFilter(string filter) => this with
    {
        Filter = filter
    };

    /// <summary>
    /// Marks all todos as completed.
    /// </summary>
    public TodoState CompleteAll()
    {
        var updatedTodos = Todos.Select(t => t with { IsCompleted = true }).ToImmutableList();
        return this with { Todos = updatedTodos };
    }

    /// <summary>
    /// Gets filtered todos based on current filter.
    /// </summary>
    public ImmutableList<TodoItem> GetFilteredTodos() => Filter switch
    {
        "Active" => Todos.Where(t => !t.IsCompleted).ToImmutableList(),
        "Completed" => Todos.Where(t => t.IsCompleted).ToImmutableList(),
        _ => Todos // "All"
    };

    /// <summary>
    /// Gets the count of active (incomplete) todos.
    /// </summary>
    public int ActiveCount => Todos.Count(t => !t.IsCompleted);

    /// <summary>
    /// Gets the count of completed todos.
    /// </summary>
    public int CompletedCount => Todos.Count(t => t.IsCompleted);
}
