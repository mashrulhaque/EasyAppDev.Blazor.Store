using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace EasyAppDev.Blazor.Store.Sample.State;

/// <summary>
/// State for the form validation demo.
/// Demonstrates field-level validation, form-level validation, and async validation.
/// </summary>
public record FormValidationState(
    string Username = "",
    string Email = "",
    string Password = "",
    string ConfirmPassword = "",
    bool AgreeToTerms = false,
    ImmutableDictionary<string, string>? Errors = null,
    bool IsValidating = false,
    bool IsUsernameAvailable = true,
    bool IsCheckingUsername = false,
    bool IsSubmitting = false,
    string? SubmitMessage = null,
    bool SubmitSuccess = false)
{
    /// <summary>
    /// Gets whether the form is valid (no errors and all required fields filled).
    /// </summary>
    public bool IsValid =>
        !string.IsNullOrWhiteSpace(Username) &&
        !string.IsNullOrWhiteSpace(Email) &&
        !string.IsNullOrWhiteSpace(Password) &&
        !string.IsNullOrWhiteSpace(ConfirmPassword) &&
        AgreeToTerms &&
        (Errors == null || Errors.IsEmpty) &&
        IsUsernameAvailable;

    /// <summary>
    /// Updates the username field.
    /// </summary>
    public FormValidationState SetUsername(string username) =>
        this with { Username = username, SubmitMessage = null, SubmitSuccess = false };

    /// <summary>
    /// Updates the email field.
    /// </summary>
    public FormValidationState SetEmail(string email) =>
        this with { Email = email, SubmitMessage = null, SubmitSuccess = false };

    /// <summary>
    /// Updates the password field.
    /// </summary>
    public FormValidationState SetPassword(string password) =>
        this with { Password = password, SubmitMessage = null, SubmitSuccess = false };

    /// <summary>
    /// Updates the confirm password field.
    /// </summary>
    public FormValidationState SetConfirmPassword(string confirmPassword) =>
        this with { ConfirmPassword = confirmPassword, SubmitMessage = null, SubmitSuccess = false };

    /// <summary>
    /// Toggles the agree to terms checkbox.
    /// </summary>
    public FormValidationState ToggleAgreeToTerms() =>
        this with { AgreeToTerms = !AgreeToTerms, SubmitMessage = null, SubmitSuccess = false };

    /// <summary>
    /// Sets an error for a specific field.
    /// </summary>
    public FormValidationState SetError(string field, string message)
    {
        var errors = Errors ?? ImmutableDictionary<string, string>.Empty;
        return this with { Errors = errors.SetItem(field, message) };
    }

    /// <summary>
    /// Clears an error for a specific field.
    /// </summary>
    public FormValidationState ClearError(string field)
    {
        if (Errors == null || !Errors.ContainsKey(field))
            return this;

        return this with { Errors = Errors.Remove(field) };
    }

    /// <summary>
    /// Clears all errors.
    /// </summary>
    public FormValidationState ClearAllErrors() =>
        this with { Errors = ImmutableDictionary<string, string>.Empty };

    /// <summary>
    /// Validates the username field.
    /// </summary>
    public FormValidationState ValidateUsername()
    {
        if (string.IsNullOrWhiteSpace(Username))
            return SetError("Username", "Username is required");

        if (Username.Length < 3)
            return SetError("Username", "Username must be at least 3 characters");

        if (Username.Length > 20)
            return SetError("Username", "Username must be at most 20 characters");

        if (!Regex.IsMatch(Username, "^[a-zA-Z0-9_]+$"))
            return SetError("Username", "Username can only contain letters, numbers, and underscores");

        return ClearError("Username");
    }

    /// <summary>
    /// Validates the email field.
    /// </summary>
    public FormValidationState ValidateEmail()
    {
        if (string.IsNullOrWhiteSpace(Email))
            return SetError("Email", "Email is required");

        // Simple email regex
        if (!Regex.IsMatch(Email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            return SetError("Email", "Invalid email format");

        return ClearError("Email");
    }

    /// <summary>
    /// Validates the password field.
    /// </summary>
    public FormValidationState ValidatePassword()
    {
        if (string.IsNullOrWhiteSpace(Password))
            return SetError("Password", "Password is required");

        if (Password.Length < 8)
            return SetError("Password", "Password must be at least 8 characters");

        if (!Regex.IsMatch(Password, "[A-Z]"))
            return SetError("Password", "Password must contain at least one uppercase letter");

        if (!Regex.IsMatch(Password, "[a-z]"))
            return SetError("Password", "Password must contain at least one lowercase letter");

        if (!Regex.IsMatch(Password, "[0-9]"))
            return SetError("Password", "Password must contain at least one number");

        return ClearError("Password");
    }

    /// <summary>
    /// Validates the confirm password field.
    /// </summary>
    public FormValidationState ValidateConfirmPassword()
    {
        if (string.IsNullOrWhiteSpace(ConfirmPassword))
            return SetError("ConfirmPassword", "Please confirm your password");

        if (Password != ConfirmPassword)
            return SetError("ConfirmPassword", "Passwords do not match");

        return ClearError("ConfirmPassword");
    }

    /// <summary>
    /// Validates the agree to terms checkbox.
    /// </summary>
    public FormValidationState ValidateAgreeToTerms()
    {
        if (!AgreeToTerms)
            return SetError("AgreeToTerms", "You must agree to the terms and conditions");

        return ClearError("AgreeToTerms");
    }

    /// <summary>
    /// Validates all fields.
    /// </summary>
    public FormValidationState ValidateAll()
    {
        return this
            .ValidateUsername()
            .ValidateEmail()
            .ValidatePassword()
            .ValidateConfirmPassword()
            .ValidateAgreeToTerms();
    }

    /// <summary>
    /// Starts the username availability check.
    /// </summary>
    public FormValidationState StartCheckingUsername() =>
        this with { IsCheckingUsername = true };

    /// <summary>
    /// Completes the username availability check.
    /// </summary>
    public FormValidationState CompleteUsernameCheck(bool isAvailable)
    {
        var newState = this with
        {
            IsCheckingUsername = false,
            IsUsernameAvailable = isAvailable
        };

        if (!isAvailable)
            newState = newState.SetError("Username", "Username is already taken");
        else
            newState = newState.ClearError("Username");

        return newState;
    }

    /// <summary>
    /// Starts form submission.
    /// </summary>
    public FormValidationState StartSubmit() =>
        this with { IsSubmitting = true, SubmitMessage = null, SubmitSuccess = false };

    /// <summary>
    /// Completes form submission successfully.
    /// </summary>
    public FormValidationState CompleteSubmitSuccess(string message) =>
        this with
        {
            IsSubmitting = false,
            SubmitSuccess = true,
            SubmitMessage = message
        };

    /// <summary>
    /// Completes form submission with an error.
    /// </summary>
    public FormValidationState CompleteSubmitFailure(string message) =>
        this with
        {
            IsSubmitting = false,
            SubmitSuccess = false,
            SubmitMessage = message
        };

    /// <summary>
    /// Resets the form to initial state.
    /// </summary>
    public FormValidationState Reset() =>
        new FormValidationState();

    /// <summary>
    /// Simulates checking username availability (async operation).
    /// </summary>
    public static async Task<bool> CheckUsernameAvailability(string username)
    {
        // Simulate API call delay
        await Task.Delay(500);

        // Simulate some taken usernames
        var takenUsernames = new[] { "admin", "user", "test", "demo", "john", "jane" };
        return !takenUsernames.Contains(username.ToLowerInvariant());
    }

    /// <summary>
    /// Simulates form submission (async operation).
    /// </summary>
    public static async Task<(bool Success, string Message)> SubmitForm(FormValidationState state)
    {
        // Simulate API call delay
        await Task.Delay(1500);

        // Simulate random failure (20% chance)
        if (new Random().Next(100) < 20)
        {
            return (false, "Server error: Unable to create account. Please try again.");
        }

        return (true, $"Account created successfully! Welcome, {state.Username}!");
    }
}
