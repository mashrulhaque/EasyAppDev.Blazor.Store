namespace EasyAppDev.Blazor.Store.Middleware;

/// <summary>
/// Configuration options for middleware pipeline execution.
/// </summary>
public sealed class MiddlewarePipelineOptions
{
    /// <summary>
    /// Stop pipeline execution when a middleware throws an exception. Default is false.
    /// </summary>
    public bool StopOnError { get; set; } = false;

    /// <summary>
    /// Log middleware errors. Default is true.
    /// </summary>
    public bool LogErrors { get; set; } = true;

    /// <summary>
    /// Maximum retries for failed middleware operations. Default is 0 (no retries).
    /// </summary>
    public int MaxRetries { get; set; } = 0;

    /// <summary>
    /// Gets the default middleware pipeline options.
    /// </summary>
    public static MiddlewarePipelineOptions Default => new()
    {
        StopOnError = false,
        LogErrors = true,
        MaxRetries = 0
    };
}
