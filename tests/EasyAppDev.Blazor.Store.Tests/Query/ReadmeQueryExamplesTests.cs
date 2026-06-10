// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

using Bunit;
using EasyAppDev.Blazor.Store.Query;
using Microsoft.Extensions.DependencyInjection;

namespace EasyAppDev.Blazor.Store.Tests.Query;

/// <summary>
/// Verifies the README "Query System" examples compile and run against the public API.
/// Guards against docs drifting from the package surface (issue #12).
/// </summary>
public class ReadmeQueryExamplesTests : TestContext
{
    public record User(string Name);

    public record UpdateUserRequest(string Name);

    private sealed class FakeApi
    {
        public int FetchCount;

        public Task<User?> GetUserAsync(int id, CancellationToken ct)
        {
            Interlocked.Increment(ref FetchCount);
            return Task.FromResult<User?>(new User($"user-{id}"));
        }

        public Task<User?> UpdateUserAsync(UpdateUserRequest req, CancellationToken ct) =>
            Task.FromResult<User?>(new User(req.Name));
    }

    /// <summary>
    /// Mirrors the README "Queries" and "Mutations with Auto-Invalidation" examples.
    /// </summary>
    private sealed class ReadmeExampleComponent : QueryComponent
    {
        public FakeApi Api { get; set; } = new();

        public Query<User> UserQuery { get; private set; } = null!;

        public Mutation<User, UpdateUserRequest> Mutation { get; private set; } = null!;

        protected override void OnInitialized()
        {
            UserQuery = UseQuery(new QueryOptions<User>
            {
                Key = "user-123",
                QueryFn = async ct => await Api.GetUserAsync(123, ct),
                StaleTime = TimeSpan.FromMinutes(5),
                CacheTime = TimeSpan.FromHours(1),
                Retry = 3,
                RefetchOnWindowFocus = true,
                RefetchOnReconnect = true
            });

            Mutation = UseMutation(new MutationOptions<User, UpdateUserRequest>
            {
                MutationFn = async (req, ct) => await Api.UpdateUserAsync(req, ct),
                OnSuccess = (_, _) => QueryClient.InvalidateQueries(
                    key => key.StartsWith("user-"))
            });
        }
    }

    private sealed class SimpleOverloadComponent : QueryComponent
    {
        public FakeApi Api { get; set; } = new();

        public Query<User> UserQuery { get; private set; } = null!;

        protected override void OnInitialized()
        {
            UserQuery = UseQuery("user-123", async ct => await Api.GetUserAsync(123, ct));
        }
    }

    [Fact]
    public void ReadmeQueryExample_FetchesAndExposesState()
    {
        Services.AddQueryClient();

        var cut = RenderComponent<ReadmeExampleComponent>();

        cut.WaitForAssertion(() =>
        {
            cut.Instance.UserQuery.IsSuccess.Should().BeTrue();
            cut.Instance.UserQuery.Data.Should().Be(new User("user-123"));
        });
        cut.Instance.UserQuery.IsLoading.Should().BeFalse();
        cut.Instance.UserQuery.IsError.Should().BeFalse();
        cut.Instance.UserQuery.Error.Should().BeNull();
    }

    [Fact]
    public async Task ReadmeMutationExample_InvalidatesMatchingQueries()
    {
        Services.AddQueryClient();

        var cut = RenderComponent<ReadmeExampleComponent>();
        cut.WaitForAssertion(() => cut.Instance.UserQuery.IsSuccess.Should().BeTrue());
        var fetchesBeforeMutation = cut.Instance.Api.FetchCount;

        await cut.Instance.Mutation.MutateAsync(new UpdateUserRequest("John"));

        cut.Instance.Mutation.IsSuccess.Should().BeTrue();
        cut.WaitForAssertion(() =>
            cut.Instance.Api.FetchCount.Should().BeGreaterThan(fetchesBeforeMutation));
    }

    [Fact]
    public void ReadmeSimpleOverloadExample_Fetches()
    {
        Services.AddQueryClient();

        var cut = RenderComponent<SimpleOverloadComponent>();

        cut.WaitForAssertion(() => cut.Instance.UserQuery.IsSuccess.Should().BeTrue());
    }
}
