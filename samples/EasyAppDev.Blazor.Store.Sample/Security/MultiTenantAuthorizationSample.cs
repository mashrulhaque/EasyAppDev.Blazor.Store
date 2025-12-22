// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

using EasyAppDev.Blazor.Store.Security;
using EasyAppDev.Blazor.Store.ServerSync;

namespace EasyAppDev.Blazor.Store.Sample.Security;

/// <summary>
/// Demonstrates multi-tenant authorization patterns for ServerSync.
/// NOTE: This file contains documentation examples. The actual SignalR Hub
/// implementation requires ASP.NET Core server project, not WebAssembly.
/// </summary>
/// <remarks>
/// To implement a secure multi-tenant hub:
/// 1. Create a new ASP.NET Core server project
/// 2. Add: <FrameworkReference Include="Microsoft.AspNetCore.App" />
/// 3. Copy the SecureStoreHubBase.cs from the library source
/// 4. Implement your hub inheriting from SecureStoreHubBase
///
/// Example server-side implementation:
/// <code>
/// [Authorize]
/// public class SecureDocumentHub : SecureStoreHubBase&lt;DocumentState&gt;
/// {
///     private readonly IDocumentAuthorizationService _authService;
///
///     public SecureDocumentHub(IDocumentAuthorizationService authService)
///     {
///         _authService = authService;
///     }
///
///     protected override async Task&lt;bool&gt; CanAccessDocumentAsync(
///         string documentId, ClaimsPrincipal user)
///     {
///         var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
///         var tenantId = user.FindFirstValue("tenant_id");
///         return await _authService.CanAccessAsync(documentId, tenantId, userId);
///     }
///
///     protected override async Task&lt;bool&gt; CanEditDocumentAsync(
///         string documentId, ClaimsPrincipal user)
///     {
///         var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
///         var tenantId = user.FindFirstValue("tenant_id");
///         return await _authService.CanEditAsync(documentId, tenantId, userId);
///     }
/// }
/// </code>
/// </remarks>
public static class MultiTenantAuthorizationSample
{
    /// <summary>
    /// Document state for multi-tenant collaborative editing.
    /// </summary>
    public record DocumentState(
        string DocumentId,
        string TenantId,
        string Title,
        string Content,
        List<Collaborator> Collaborators,
        DateTimeOffset LastModified,
        int Version);

    public record Collaborator(
        string UserId,
        string DisplayName,
        string Color,
        bool CanEdit);

    /// <summary>
    /// Document authorization service interface.
    /// Implement this for your specific authorization logic.
    /// </summary>
    public interface IDocumentAuthorizationService
    {
        Task<bool> CanAccessDocumentAsync(string documentId, string tenantId, string userId);
        Task<bool> CanEditDocumentAsync(string documentId, string tenantId, string userId);
        Task<bool> HasDocumentAccessCachedAsync(string documentId, string tenantId, string userId);
        Task<int> GetDocumentVersionAsync(string documentId);
    }

    /// <summary>
    /// Document metadata for authorization decisions.
    /// </summary>
    public record DocumentMetadata(
        string DocumentId,
        string TenantId,
        string OwnerId,
        List<CollaboratorInfo> Collaborators,
        int Version);

    public record CollaboratorInfo(
        string UserId,
        bool CanEdit);

    /// <summary>
    /// Rate limiter interface for protecting hub endpoints.
    /// </summary>
    public interface IRateLimiter
    {
        Task<bool> TryAcquireAsync(string userId, string operation);
    }

    /// <summary>
    /// Sample rate limiter implementation using sliding window.
    /// Use this as a starting point for your server-side implementation.
    /// </summary>
    public class SlidingWindowRateLimiter : IRateLimiter
    {
        private readonly Dictionary<string, Queue<DateTimeOffset>> _requests = new();
        private readonly int _maxRequests;
        private readonly TimeSpan _window;
        private readonly object _lock = new();

        public SlidingWindowRateLimiter(int maxRequests = 10, int windowSeconds = 1)
        {
            _maxRequests = maxRequests;
            _window = TimeSpan.FromSeconds(windowSeconds);
        }

        public Task<bool> TryAcquireAsync(string userId, string operation)
        {
            var key = $"{userId}:{operation}";
            var now = DateTimeOffset.UtcNow;

            lock (_lock)
            {
                if (!_requests.ContainsKey(key))
                    _requests[key] = new Queue<DateTimeOffset>();

                var queue = _requests[key];

                // Remove old entries
                while (queue.Count > 0 && now - queue.Peek() > _window)
                    queue.Dequeue();

                // Check limit
                if (queue.Count >= _maxRequests)
                    return Task.FromResult(false);

                // Add current request
                queue.Enqueue(now);
                return Task.FromResult(true);
            }
        }
    }

    /// <summary>
    /// Demonstrates how to configure client-side ServerSync with security options.
    /// </summary>
    public static void ConfigureSecureClientSync(IServiceProvider sp, Core.StoreBuilder<DocumentState> builder)
    {
        builder.WithServerSync(sp, opts =>
        {
            // Secure connection - always use HTTPS/WSS in production
            opts.HubUrl = "https://your-server.com/hubs/documents";

            // Enable session validation
            opts.RequireSessionValidation = true;
            opts.SessionTimeoutMinutes = 30;

            // State validation
            opts.StateValidator = new DocumentStateValidator();
            opts.RequireValidation = true;
            opts.RejectInvalidState = true;

            // Rate limiting protection
            opts.RateLimitPerSecond = 10;

            // Version integrity
            opts.MaxVersionJump = 1000;
            opts.RejectSuspiciousVersions = true;

            // Security callbacks
            opts.OnSessionExpired = () =>
                Console.WriteLine("Session expired, redirecting to login...");

            opts.OnSessionValidationFailed = reason =>
                Console.WriteLine($"Session validation failed: {reason}");

            opts.OnSuspiciousActivity = msg =>
                Console.WriteLine($"Security alert: {msg}");

            opts.OnValidationFailed = result =>
                Console.WriteLine($"State validation failed: {string.Join(", ", result.Errors)}");
        });
    }

    /// <summary>
    /// Document state validator.
    /// </summary>
    public class DocumentStateValidator : IStateValidator<DocumentState>
    {
        public StateValidationResult Validate(DocumentState state)
        {
            var errors = new List<string>();

            if (state == null)
                return StateValidationResult.Failure("State cannot be null");

            if (string.IsNullOrWhiteSpace(state.DocumentId))
                errors.Add("Document ID is required");

            if (string.IsNullOrWhiteSpace(state.TenantId))
                errors.Add("Tenant ID is required");

            if (state.Content?.Length > 10_000_000) // 10MB limit
                errors.Add("Content exceeds maximum size");

            if (state.Collaborators?.Count > 100)
                errors.Add("Too many collaborators");

            if (state.Version < 0)
                errors.Add("Version cannot be negative");

            return errors.Count > 0
                ? StateValidationResult.Failure(errors)
                : StateValidationResult.Success();
        }
    }
}
