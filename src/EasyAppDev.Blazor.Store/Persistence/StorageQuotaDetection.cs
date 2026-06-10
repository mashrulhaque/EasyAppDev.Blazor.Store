// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

using Microsoft.JSInterop;

namespace EasyAppDev.Blazor.Store.Persistence;

/// <summary>
/// Shared helper for detecting browser storage quota errors across providers.
/// Browsers report quota errors with different (and differently cased) names,
/// so detection must be case-insensitive and cover legacy error names.
/// </summary>
internal static class StorageQuotaDetection
{
    /// <summary>
    /// Determines if a JSException represents a storage quota exceeded error.
    /// Handles various browser-specific error messages case-insensitively.
    /// </summary>
    /// <param name="ex">The JavaScript interop exception to inspect.</param>
    /// <returns>True when the exception indicates the storage quota was exceeded.</returns>
    internal static bool IsQuotaExceededException(JSException ex)
    {
        var message = ex.Message;
        if (string.IsNullOrEmpty(message))
            return false;

        // Standard DOMException name (Chrome, Firefox, Safari, Edge)
        if (message.Contains("QuotaExceededError", StringComparison.OrdinalIgnoreCase))
            return true;

        // Legacy error name
        if (message.Contains("QUOTA_EXCEEDED_ERR", StringComparison.OrdinalIgnoreCase))
            return true;

        // Firefox legacy format
        if (message.Contains("NS_ERROR_DOM_QUOTA_REACHED", StringComparison.OrdinalIgnoreCase))
            return true;

        // Generic quota keyword as fallback
        if (message.Contains("quota", StringComparison.OrdinalIgnoreCase) &&
            message.Contains("exceed", StringComparison.OrdinalIgnoreCase))
            return true;

        // Check for storage full indicators
        if (message.Contains("storage", StringComparison.OrdinalIgnoreCase) &&
            message.Contains("full", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }
}
