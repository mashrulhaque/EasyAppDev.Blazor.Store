// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

using System.Security.Cryptography;
using System.Text;

namespace EasyAppDev.Blazor.Store.Security;

/// <summary>
/// Provides HMAC-based message signing for cross-tab and cross-origin communication.
/// Includes support for key rotation and replay protection via timestamps.
/// </summary>
public sealed class MessageSigner : IDisposable
{
    private HMACSHA256 _hmac;
    private HMACSHA256? _previousHmac;
    private bool _disposed;
    private readonly object _lock = new();

    /// <summary>
    /// Creates a message signer with a random key.
    /// </summary>
    /// <remarks>
    /// WARNING: Using a random key means each tab instance will have a different key,
    /// making cross-tab message verification impossible. This constructor is only suitable
    /// for scenarios where you don't need to verify messages from other instances.
    /// For cross-tab synchronization, use <see cref="MessageSigner(byte[])"/> with a
    /// shared key derived from <see cref="DeriveKeyFromSeed"/> or <see cref="DeriveKeyFromPassphrase"/>.
    /// </remarks>
    public MessageSigner()
    {
        _hmac = new HMACSHA256();
    }

    /// <summary>
    /// Creates a message signer with a specified key.
    /// Use this when you need consistent signing across instances (e.g., cross-tab sync).
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
    /// Made internal to discourage direct access - use key rotation methods instead.
    /// </summary>
    internal byte[] Key
    {
        get
        {
            lock (_lock)
            {
                return _hmac.Key;
            }
        }
    }

    /// <summary>
    /// Creates a message signer with a key derived from a passphrase.
    /// Uses PBKDF2 to derive a cryptographically strong key.
    /// </summary>
    /// <param name="passphrase">The passphrase to derive the key from.</param>
    /// <param name="salt">Optional salt for key derivation. If null, a default salt is used.</param>
    /// <param name="iterations">Number of PBKDF2 iterations. Default is 100,000.</param>
    /// <returns>A new MessageSigner with a derived key.</returns>
    public static MessageSigner CreateWithDerivedKey(
        string passphrase,
        byte[]? salt = null,
        int iterations = 100_000)
    {
        ArgumentNullException.ThrowIfNull(passphrase);

        salt ??= Encoding.UTF8.GetBytes("EasyAppDev.Blazor.Store.DefaultSalt");

        if (iterations < 10_000)
            throw new ArgumentException("Iterations must be at least 10,000 for security", nameof(iterations));

#if NET9_0_OR_GREATER
        var key = Rfc2898DeriveBytes.Pbkdf2(passphrase, salt, iterations, HashAlgorithmName.SHA256, 32);
#else
        using var pbkdf2 = new Rfc2898DeriveBytes(passphrase, salt, iterations, HashAlgorithmName.SHA256);
        var key = pbkdf2.GetBytes(32);
#endif

        return new MessageSigner(key);
    }

    /// <summary>
    /// Rotates the signing key to a new key.
    /// The previous key is retained temporarily to allow verification of messages
    /// signed with the old key during the transition period.
    /// </summary>
    /// <param name="newKey">The new HMAC key (must be at least 32 bytes).</param>
    /// <remarks>
    /// After rotation, both the new key and previous key can verify signatures.
    /// Only the new key is used for signing. Call this method again with a new key
    /// to discard the oldest key.
    /// </remarks>
    public void RotateKey(byte[] newKey)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(newKey);
        if (newKey.Length < 32)
            throw new ArgumentException("Key must be at least 32 bytes", nameof(newKey));

        lock (_lock)
        {
            _previousHmac?.Dispose();
            _previousHmac = _hmac;
            _hmac = new HMACSHA256(newKey);
        }
    }

    /// <summary>
    /// Signs a message and returns the signature as a base64 string.
    /// For replay protection, the caller should include a timestamp in the message.
    /// </summary>
    /// <param name="message">The message to sign.</param>
    /// <returns>The base64-encoded signature.</returns>
    /// <remarks>
    /// This method signs the message content only. For automatic replay protection,
    /// use <see cref="SignWithTimestamp"/> which embeds a timestamp.
    /// </remarks>
    public string Sign(string message)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(message);

        lock (_lock)
        {
            var messageBytes = Encoding.UTF8.GetBytes(message);
            var hash = _hmac.ComputeHash(messageBytes);
            return Convert.ToBase64String(hash);
        }
    }

    /// <summary>
    /// Signs a message with an embedded timestamp for replay protection.
    /// Returns both the signature and the timestamp used.
    /// </summary>
    /// <param name="message">The message to sign.</param>
    /// <param name="timestamp">The timestamp that was embedded in the signature.</param>
    /// <returns>The base64-encoded signature that includes the timestamp.</returns>
    /// <remarks>
    /// The timestamp is embedded in the signed content to prevent replay attacks.
    /// Use <see cref="VerifyWithTimestamp"/> to verify signatures created with this method.
    /// </remarks>
    public string SignWithTimestamp(string message, out long timestamp)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(message);

        timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signedContent = $"{message}|{timestamp}";

        lock (_lock)
        {
            var messageBytes = Encoding.UTF8.GetBytes(signedContent);
            var hash = _hmac.ComputeHash(messageBytes);
            return Convert.ToBase64String(hash);
        }
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
            // Try current key
            lock (_lock)
            {
                if (TryVerify(message, signature, _hmac))
                {
                    return true;
                }

                // Try previous key (key rotation support)
                if (_previousHmac != null && TryVerify(message, signature, _previousHmac))
                {
                    return true;
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Verifies a message signature with timestamp validation for replay protection.
    /// </summary>
    /// <param name="message">The original message.</param>
    /// <param name="signature">The base64-encoded signature to verify.</param>
    /// <param name="timestamp">The timestamp that was embedded when signing.</param>
    /// <param name="maxAgeSeconds">Maximum age of the message in seconds. Default is 30.</param>
    /// <returns>True if the signature is valid and not expired.</returns>
    public bool VerifyWithTimestamp(string message, string signature, long timestamp, int maxAgeSeconds = 30)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(signature);

        try
        {
            var currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            // Check timestamp freshness first
            var age = currentTime - timestamp;
            if (age < 0 || age > maxAgeSeconds)
            {
                return false;
            }

            // Verify signature with embedded timestamp
            var signedContent = $"{message}|{timestamp}";
            lock (_lock)
            {
                if (TryVerify(signedContent, signature, _hmac))
                {
                    return true;
                }

                // Try previous key (key rotation support)
                if (_previousHmac != null && TryVerify(signedContent, signature, _previousHmac))
                {
                    return true;
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private bool TryVerify(string message, string signature, HMACSHA256 hmac)
    {
        var messageBytes = Encoding.UTF8.GetBytes(message);
        var hash = hmac.ComputeHash(messageBytes);
        var expectedSignature = Convert.ToBase64String(hash);

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expectedSignature),
            Encoding.UTF8.GetBytes(signature));
    }

    /// <summary>
    /// Derives a consistent HMAC key from a seed string using PBKDF2.
    /// </summary>
    /// <param name="seed">The seed string to derive the key from (e.g., window.location.origin).</param>
    /// <param name="iterations">Number of PBKDF2 iterations (default: 10000).</param>
    /// <returns>A 32-byte key suitable for HMAC-SHA256.</returns>
    /// <remarks>
    /// This method uses PBKDF2 with a static salt derived from the seed itself.
    /// While not cryptographically ideal, it provides consistency across tab instances
    /// while maintaining reasonable security for cross-tab message verification.
    /// For production applications with high security requirements, consider using
    /// a server-provided session key instead.
    /// </remarks>
    public static byte[] DeriveKeyFromSeed(string seed, int iterations = 10000)
    {
        ArgumentNullException.ThrowIfNull(seed);
        if (iterations < 1000)
            throw new ArgumentException("Iterations must be at least 1000 for security", nameof(iterations));

        // Use SHA256 of seed as salt for deterministic behavior
        var saltBytes = SHA256.HashData(Encoding.UTF8.GetBytes(seed));

#if NET9_0_OR_GREATER
        return Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(seed),
            saltBytes,
            iterations,
            HashAlgorithmName.SHA256,
            32);
#else
        using var pbkdf2 = new Rfc2898DeriveBytes(
            Encoding.UTF8.GetBytes(seed),
            saltBytes,
            iterations,
            HashAlgorithmName.SHA256);

        return pbkdf2.GetBytes(32);
#endif
    }

    /// <summary>
    /// Derives a consistent HMAC key from a passphrase using PBKDF2 with a custom salt.
    /// </summary>
    /// <param name="passphrase">The passphrase to derive the key from.</param>
    /// <param name="salt">The salt bytes (should be at least 16 bytes).</param>
    /// <param name="iterations">Number of PBKDF2 iterations (default: 100000).</param>
    /// <returns>A 32-byte key suitable for HMAC-SHA256.</returns>
    /// <remarks>
    /// This method provides stronger key derivation than <see cref="DeriveKeyFromSeed"/>
    /// and should be used when you can securely share a salt between instances.
    /// The salt does not need to be secret but should be consistent across instances.
    /// </remarks>
    public static byte[] DeriveKeyFromPassphrase(string passphrase, byte[] salt, int iterations = 100000)
    {
        ArgumentNullException.ThrowIfNull(passphrase);
        ArgumentNullException.ThrowIfNull(salt);
        if (salt.Length < 16)
            throw new ArgumentException("Salt must be at least 16 bytes", nameof(salt));
        if (iterations < 10000)
            throw new ArgumentException("Iterations must be at least 10000 for security", nameof(iterations));

#if NET9_0_OR_GREATER
        return Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(passphrase),
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            32);
#else
        using var pbkdf2 = new Rfc2898DeriveBytes(
            Encoding.UTF8.GetBytes(passphrase),
            salt,
            iterations,
            HashAlgorithmName.SHA256);

        return pbkdf2.GetBytes(32);
#endif
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

        lock (_lock)
        {
            _disposed = true;
            _hmac.Dispose();
            _previousHmac?.Dispose();
        }
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
