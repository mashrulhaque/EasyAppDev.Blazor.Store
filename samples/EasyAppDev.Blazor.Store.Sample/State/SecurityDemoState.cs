using EasyAppDev.Blazor.Store.Security;

namespace EasyAppDev.Blazor.Store.Sample.State;

/// <summary>
/// State for demonstrating sensitive data filtering.
/// Properties marked with [SensitiveData] are filtered from DevTools.
/// </summary>
public record SecurityDemoState(
    string Username,
    [property: SensitiveData] string Password,
    [property: SensitiveData(Reason = "API authentication")] string ApiToken,
    string Email,
    CreditCardInfo? Card,
    bool IsLoggedIn)
{
    public static SecurityDemoState Initial => new(
        "demo_user",
        "super_secret_password",
        "sk-1234567890abcdef",
        "user@example.com",
        null,
        false);

    public SecurityDemoState SetCredentials(string username, string password) =>
        this with { Username = username, Password = password };

    public SecurityDemoState SetApiToken(string token) =>
        this with { ApiToken = token };

    public SecurityDemoState SetCard(CreditCardInfo card) =>
        this with { Card = card };

    public SecurityDemoState Login() =>
        this with { IsLoggedIn = true };

    public SecurityDemoState Logout() =>
        this with { IsLoggedIn = false, Card = null };
}

public record CreditCardInfo(
    string CardNumber,
    [property: SensitiveData] string Cvv,
    string ExpiryDate,
    string HolderName);
