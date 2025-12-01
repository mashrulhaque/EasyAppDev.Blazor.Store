// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

using System.Security.Cryptography;
using System.Text;

namespace EasyAppDev.Blazor.Store.Security;

/// <summary>
/// Provides HMAC-based message signing for cross-tab and cross-origin communication.
/// </summary>
public sealed class MessageSigner : IDisposable
{
    private readonly HMACSHA256 _hmac;
    private bool _disposed;

    /// <summary>
    /// Creates a message signer with a random key.
    /// The key is unique per browser session.
    /// </summary>
    public MessageSigner()
    {
        _hmac = new HMACSHA256();
    }

    /// <summary>
    /// Creates a message signer with a specified key.
    /// Use this when you need consistent signing across instances.
    /// </summary>
    /// <param name="key">The HMAC key (must be at least 32 bytes).</param>
    public MessageSigner(byte[] key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key.Length < 32)
            throw new ArgumentException("Key must be at least 32 bytes", nameof(key));

        _hmac = new HMACSHA256(key);
    }

    /// <summary>
    /// Gets the current signing key.
    /// </summary>
    public byte[] Key => _hmac.Key;

    /// <summary>
    /// Signs a message and returns the signature as a base64 string.
    /// </summary>
    /// <param name="message">The message to sign.</param>
    /// <returns>The base64-encoded signature.</returns>
    public string Sign(string message)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(message);

        var messageBytes = Encoding.UTF8.GetBytes(message);
        var hash = _hmac.ComputeHash(messageBytes);
        return Convert.ToBase64String(hash);
    }

    /// <summary>
    /// Verifies a message signature.
    /// </summary>
    /// <param name="message">The original message.</param>
    /// <param name="signature">The base64-encoded signature to verify.</param>
    /// <returns>True if the signature is valid.</returns>
    public bool Verify(string message, string signature)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(signature);

        try
        {
            var expectedSignature = Sign(message);
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expectedSignature),
                Encoding.UTF8.GetBytes(signature));
        }
        catch
        {
            return false;
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(MessageSigner));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _hmac.Dispose();
    }
}

/// <summary>
/// Options for message security.
/// </summary>
public sealed class MessageSecurityOptions
{
    /// <summary>
    /// Gets or sets whether to sign messages. Default is false for backward compatibility.
    /// </summary>
    public bool EnableSigning { get; set; }

    /// <summary>
    /// Gets or sets whether to verify signatures on received messages.
    /// Only applies when EnableSigning is true.
    /// </summary>
    public bool RequireValidSignature { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum age of a message in seconds before it's rejected.
    /// Helps prevent replay attacks. Default is 30 seconds.
    /// </summary>
    public int MaxMessageAgeSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets whether to validate message timestamps.
    /// </summary>
    public bool ValidateTimestamp { get; set; } = true;
}
