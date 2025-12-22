// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

namespace EasyAppDev.Blazor.Store.Security;

/// <summary>
/// Provides security configuration guidance for SignalR hubs.
/// </summary>
/// <remarks>
/// <para><b>SECURITY HEADERS FOR SIGNALR</b></para>
///
/// <para><b>1. CORS Configuration</b></para>
/// <para>
/// For production SignalR hubs, configure CORS to only allow your application's origin:
/// </para>
/// <code>
/// // In Program.cs or Startup.cs
/// builder.Services.AddCors(options =>
/// {
///     options.AddPolicy("SignalRPolicy", policy =>
///     {
///         policy.WithOrigins("https://yourdomain.com")
///               .AllowAnyHeader()
///               .AllowAnyMethod()
///               .AllowCredentials(); // Required for SignalR
///     });
/// });
///
/// // In Configure/app pipeline
/// app.UseCors("SignalRPolicy");
///
/// // For the hub endpoint
/// app.MapHub&lt;YourHub&gt;("/hubs/store")
///    .RequireCors("SignalRPolicy");
/// </code>
///
/// <para><b>2. Content Security Policy (CSP)</b></para>
/// <para>
/// Add CSP headers to allow SignalR WebSocket connections:
/// </para>
/// <code>
/// // Add to your security headers middleware
/// app.Use(async (context, next) =>
/// {
///     context.Response.Headers.Append("Content-Security-Policy",
///         "default-src 'self'; " +
///         "connect-src 'self' wss://yourdomain.com; " +  // WebSocket connections
///         "script-src 'self'; " +
///         "style-src 'self' 'unsafe-inline';");
///     await next();
/// });
/// </code>
///
/// <para><b>3. Transport Security</b></para>
/// <para>
/// Always use HTTPS/WSS in production. Configure SSL/TLS:
/// </para>
/// <code>
/// // Configure HTTPS redirection
/// app.UseHttpsRedirection();
/// app.UseHsts();
///
/// // SignalR transport configuration
/// app.MapHub&lt;YourHub&gt;("/hubs/store", options =>
/// {
///     options.Transports = HttpTransportType.WebSockets; // Prefer WebSockets
/// });
/// </code>
///
/// <para><b>4. Authentication Configuration</b></para>
/// <para>
/// For JWT authentication with SignalR:
/// </para>
/// <code>
/// builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
///     .AddJwtBearer(options =>
///     {
///         // Configure JWT validation
///         options.Events = new JwtBearerEvents
///         {
///             OnMessageReceived = context =>
///             {
///                 // Allow token in query string for WebSocket connections
///                 var accessToken = context.Request.Query["access_token"];
///                 var path = context.HttpContext.Request.Path;
///                 if (!string.IsNullOrEmpty(accessToken) &amp;&amp;
///                     path.StartsWithSegments("/hubs"))
///                 {
///                     context.Token = accessToken;
///                 }
///                 return Task.CompletedTask;
///             }
///         };
///     });
/// </code>
///
/// <para><b>5. Rate Limiting</b></para>
/// <para>
/// Apply rate limiting to SignalR endpoints:
/// </para>
/// <code>
/// // Use RateLimitingHubFilter or configure ASP.NET Core rate limiting
/// app.MapHub&lt;YourHub&gt;("/hubs/store")
///    .AddEndpointFilter&lt;RateLimitingEndpointFilter&gt;();
/// </code>
///
/// <para><b>6. Complete Secure Configuration Example</b></para>
/// <code>
/// // Program.cs
/// var builder = WebApplication.CreateBuilder(args);
///
/// // Add authentication
/// builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
///     .AddJwtBearer(options => { /* configure */ });
///
/// // Add authorization
/// builder.Services.AddAuthorization();
///
/// // Add CORS
/// builder.Services.AddCors(options =>
/// {
///     options.AddPolicy("SignalR", policy =>
///         policy.WithOrigins("https://yourdomain.com")
///               .AllowAnyHeader()
///               .AllowAnyMethod()
///               .AllowCredentials());
/// });
///
/// // Add SignalR
/// builder.Services.AddSignalR(options =>
/// {
///     options.EnableDetailedErrors = false; // Don't expose errors in production
///     options.MaximumReceiveMessageSize = 1024 * 1024; // 1MB limit
///     options.StreamBufferCapacity = 10;
///     options.KeepAliveInterval = TimeSpan.FromSeconds(15);
///     options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
/// });
///
/// var app = builder.Build();
///
/// // Security middleware
/// app.UseHttpsRedirection();
/// app.UseHsts();
/// app.UseCors("SignalR");
/// app.UseAuthentication();
/// app.UseAuthorization();
///
/// // Map hub with security
/// app.MapHub&lt;YourSecureHub&gt;("/hubs/store")
///    .RequireAuthorization()
///    .RequireCors("SignalR");
///
/// app.Run();
/// </code>
/// </remarks>
public static class SignalRSecurityGuidance
{
    /// <summary>
    /// Default maximum message size in bytes (1MB).
    /// </summary>
    public const int DefaultMaxMessageSize = 1_048_576;

    /// <summary>
    /// Default rate limit (messages per second per connection).
    /// </summary>
    public const int DefaultRateLimitPerSecond = 10;

    /// <summary>
    /// Recommended keep-alive interval.
    /// </summary>
    public static readonly TimeSpan RecommendedKeepAliveInterval = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Recommended client timeout interval.
    /// </summary>
    public static readonly TimeSpan RecommendedClientTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets recommended CSP directives for SignalR applications.
    /// </summary>
    /// <param name="allowedOrigins">The allowed WebSocket origins (e.g., "wss://yourdomain.com").</param>
    /// <returns>A CSP header value string.</returns>
    public static string GetRecommendedCspHeader(params string[] allowedOrigins)
    {
        var connectSrc = allowedOrigins.Length > 0
            ? $"connect-src 'self' {string.Join(" ", allowedOrigins)}"
            : "connect-src 'self'";

        return $"default-src 'self'; {connectSrc}; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self';";
    }

    /// <summary>
    /// Gets recommended security headers for SignalR applications.
    /// </summary>
    /// <returns>Dictionary of header name to value pairs.</returns>
    public static Dictionary<string, string> GetRecommendedSecurityHeaders()
    {
        return new Dictionary<string, string>
        {
            ["X-Content-Type-Options"] = "nosniff",
            ["X-Frame-Options"] = "DENY",
            ["X-XSS-Protection"] = "1; mode=block",
            ["Referrer-Policy"] = "strict-origin-when-cross-origin",
            ["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()"
        };
    }

    /// <summary>
    /// Validates that a hub URL is secure (uses WSS in production).
    /// </summary>
    /// <param name="hubUrl">The hub URL to validate.</param>
    /// <param name="isProduction">Whether this is a production environment.</param>
    /// <returns>True if the URL is considered secure for the environment.</returns>
    public static bool IsSecureHubUrl(string hubUrl, bool isProduction)
    {
        if (string.IsNullOrEmpty(hubUrl))
            return false;

        var uri = new Uri(hubUrl, UriKind.RelativeOrAbsolute);

        // Relative URLs are considered secure (will use page's protocol)
        if (!uri.IsAbsoluteUri)
            return true;

        // In production, require HTTPS/WSS
        if (isProduction)
        {
            return uri.Scheme == Uri.UriSchemeHttps ||
                   uri.Scheme == "wss";
        }

        // In development, allow HTTP for localhost
        if (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == "ws")
        {
            return uri.Host == "localhost" || uri.Host == "127.0.0.1";
        }

        return true;
    }
}
