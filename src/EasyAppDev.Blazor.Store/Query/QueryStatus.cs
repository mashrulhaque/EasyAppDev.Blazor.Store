// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

namespace EasyAppDev.Blazor.Store.Query;

/// <summary>
/// Represents the current status of a query operation.
/// </summary>
public enum QueryStatus
{
    /// <summary>
    /// The query has not been executed yet.
    /// </summary>
    Idle,

    /// <summary>
    /// The query is currently fetching data.
    /// </summary>
    Loading,

    /// <summary>
    /// The query failed with an error.
    /// </summary>
    Error,

    /// <summary>
    /// The query completed successfully.
    /// </summary>
    Success
}
