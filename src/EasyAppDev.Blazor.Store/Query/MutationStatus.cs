// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

namespace EasyAppDev.Blazor.Store.Query;

/// <summary>
/// Represents the current status of a mutation operation.
/// </summary>
public enum MutationStatus
{
    /// <summary>
    /// The mutation has not been executed yet or was reset.
    /// </summary>
    Idle,

    /// <summary>
    /// The mutation is currently executing.
    /// </summary>
    Loading,

    /// <summary>
    /// The mutation failed with an error.
    /// </summary>
    Error,

    /// <summary>
    /// The mutation completed successfully.
    /// </summary>
    Success
}
