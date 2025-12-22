using System.Collections.Immutable;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using EasyAppDev.Blazor.Store.AsyncActions;

namespace EasyAppDev.Blazor.Store.Sample.State;

// Dog CEO API Models
public record DogImageResponse(string Message, string Status);

// Cat Fact API Models
public record CatFact(string Fact, int Length);

// Chuck Norris API Models
public record ChuckNorrisJoke(
    [property: JsonPropertyName("icon_url")] string IconUrl,
    string Id,
    string Url,
    string Value);

// Open Trivia API Models
public record TriviaResponse(
    [property: JsonPropertyName("response_code")] int ResponseCode,
    List<TriviaQuestion> Results);

public record TriviaQuestion(
    string Category,
    string Type,
    string Difficulty,
    string Question,
    [property: JsonPropertyName("correct_answer")] string CorrectAnswer,
    [property: JsonPropertyName("incorrect_answers")] List<string> IncorrectAnswers);

// Advice Slip API Models (https://api.adviceslip.com/)
public record AdviceSlip(int Id, string Advice);
public record AdviceSlipResponse(AdviceSlip Slip);

/// <summary>
/// State for Real API Demo - showcases multiple free REST APIs.
/// </summary>
public record RealApiDemoState(
    AsyncData<string> DogImage,
    AsyncData<CatFact> CatFact,
    AsyncData<ChuckNorrisJoke> ChuckJoke,
    AsyncData<TriviaQuestion> Trivia,
    AsyncData<AdviceSlip> Advice,
    string? SelectedTriviaAnswer,
    bool? TriviaAnswerCorrect,
    int ApiCallCount)
{
    public static RealApiDemoState Initial => new(
        DogImage: AsyncData<string>.NotAsked(),
        CatFact: AsyncData<CatFact>.NotAsked(),
        ChuckJoke: AsyncData<ChuckNorrisJoke>.NotAsked(),
        Trivia: AsyncData<TriviaQuestion>.NotAsked(),
        Advice: AsyncData<AdviceSlip>.NotAsked(),
        SelectedTriviaAnswer: null,
        TriviaAnswerCorrect: null,
        ApiCallCount: 0
    );

    public RealApiDemoState IncrementCallCount() => this with { ApiCallCount = ApiCallCount + 1 };

    public RealApiDemoState SetTriviaAnswer(string answer, bool correct) => this with
    {
        SelectedTriviaAnswer = answer,
        TriviaAnswerCorrect = correct
    };

    public RealApiDemoState ClearTriviaAnswer() => this with
    {
        SelectedTriviaAnswer = null,
        TriviaAnswerCorrect = null
    };
}

/// <summary>
/// Service for calling various free public APIs.
/// </summary>
public class PublicApiService
{
    private readonly HttpClient _httpClient;

    public PublicApiService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Get a random dog image from Dog CEO API.
    /// https://dog.ceo/dog-api/
    /// </summary>
    public async Task<string> GetRandomDogImageAsync(CancellationToken ct = default)
    {
        var response = await _httpClient.GetFromJsonAsync<DogImageResponse>(
            "https://dog.ceo/api/breeds/image/random", ct);
        return response?.Message ?? throw new Exception("Failed to fetch dog image");
    }

    /// <summary>
    /// Get a random cat fact from Cat Fact API.
    /// https://catfact.ninja/
    /// </summary>
    public async Task<CatFact> GetRandomCatFactAsync(CancellationToken ct = default)
    {
        var response = await _httpClient.GetFromJsonAsync<CatFact>(
            "https://catfact.ninja/fact", ct);
        return response ?? throw new Exception("Failed to fetch cat fact");
    }

    /// <summary>
    /// Get a random Chuck Norris joke.
    /// https://api.chucknorris.io/
    /// </summary>
    public async Task<ChuckNorrisJoke> GetRandomChuckNorrisJokeAsync(CancellationToken ct = default)
    {
        var response = await _httpClient.GetFromJsonAsync<ChuckNorrisJoke>(
            "https://api.chucknorris.io/jokes/random", ct);
        return response ?? throw new Exception("Failed to fetch Chuck Norris joke");
    }

    /// <summary>
    /// Get a random trivia question from Open Trivia DB.
    /// https://opentdb.com/
    /// </summary>
    public async Task<TriviaQuestion> GetRandomTriviaAsync(CancellationToken ct = default)
    {
        var response = await _httpClient.GetFromJsonAsync<TriviaResponse>(
            "https://opentdb.com/api.php?amount=1&type=multiple", ct);

        if (response?.Results == null || response.Results.Count == 0)
            throw new Exception("Failed to fetch trivia question");

        return response.Results[0];
    }

    /// <summary>
    /// Get a random advice slip.
    /// https://api.adviceslip.com/
    /// </summary>
    public async Task<AdviceSlip> GetRandomAdviceAsync(CancellationToken ct = default)
    {
        var response = await _httpClient.GetFromJsonAsync<AdviceSlipResponse>(
            "https://api.adviceslip.com/advice", ct);
        return response?.Slip ?? throw new Exception("Failed to fetch advice");
    }
}
