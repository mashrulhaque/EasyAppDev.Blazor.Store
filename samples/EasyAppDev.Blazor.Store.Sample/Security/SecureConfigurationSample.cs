// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

using EasyAppDev.Blazor.Store.Security;

namespace EasyAppDev.Blazor.Store.Sample.Security;

/// <summary>
/// Demonstrates secure store configuration patterns for production deployments.
/// This file contains documentation examples and patterns.
/// For working examples, see the main Program.cs file.
/// </summary>
/// <remarks>
/// <para><b>Security Configuration Checklist:</b></para>
/// <para>
/// 1. Disable DevTools in production using #if DEBUG
/// 2. Enable state validation for all external data sources
/// 3. Use [SensitiveData] attribute on sensitive properties
/// 4. Enable message signing for TabSync
/// 5. Use TransformOnSave to exclude secrets from persistence
/// 6. Configure size limits to prevent DoS
/// </para>
/// <example>
/// <code>
/// // Secure store registration example
/// builder.Services.AddStore(
///     SecureAppState.Initial,
///     (store, sp) => store
/// #if DEBUG
///         .WithDefaults(sp, "SecureAppStore")  // DevTools in DEBUG only
/// #else
///         .WithLogging()  // No DevTools in production
/// #endif
///         .WithPersistence(sp, opts => opts
///             .Key("secure-state")
///             .WithIntegrityCheck()
///             .WithSensitiveDataFiltering())
///         .WithTabSync(sp, opts => opts
///             .Channel("secure-channel")
///             .EnableMessageSigning()
///             .RequireValidSignature(true)));
/// </code>
/// </example>
/// </remarks>
public static class SecureConfigurationSample
{
    /// <summary>
    /// Example state with proper security attributes.
    /// </summary>
    public record SecureAppState(
        int UserId,
        string Username,
        string Email,
        [property: SensitiveData] string? AuthToken,
        [property: SensitiveData] string? RefreshToken,
        [property: SensitiveData(Reason = "API credentials")] string? ApiKey,
        CartInfo Cart,
        bool IsAuthenticated)
    {
        public static SecureAppState Initial => new(
            0, "", "", null, null, null,
            new CartInfo(new List<CartItem>(), 0, null), false);
    }

    public record CartInfo(
        List<CartItem> Items,
        decimal Total,
        [property: SensitiveData] string? PaymentMethodToken);

    public record CartItem(
        string ProductId,
        string Name,
        int Quantity,
        decimal Price);

    /// <summary>
    /// State validator for SecureAppState.
    /// Validates all state transitions and data from external sources.
    /// </summary>
    public class SecureAppStateValidator : IStateValidator<SecureAppState>
    {
        public StateValidationResult Validate(SecureAppState state)
        {
            var errors = new List<string>();

            if (state == null)
                return StateValidationResult.Failure("State cannot be null");

            // Range validations
            if (state.UserId < 0)
                errors.Add("UserId cannot be negative");

            // String length validations
            if (state.Username?.Length > 100)
                errors.Add("Username exceeds maximum length");

            if (state.Email?.Length > 254)
                errors.Add("Email exceeds maximum length");

            // Cart validations
            if (state.Cart?.Items?.Count > 100)
                errors.Add("Cart cannot contain more than 100 items");

            if (state.Cart?.Total < 0)
                errors.Add("Cart total cannot be negative");

            // Business rule validations
            if (state.IsAuthenticated && state.UserId == 0)
                errors.Add("Authenticated state requires a valid UserId");

            return errors.Count > 0
                ? StateValidationResult.Failure(errors)
                : StateValidationResult.Success();
        }
    }
}
