using EasyAppDev.Blazor.Store.Blazor;
using EasyAppDev.Blazor.Store.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Moq;

namespace EasyAppDev.Blazor.Store.Tests.Blazor;

public class StoreBuilderExtensionsTests
{
    private record TestState(int Value);

    [Fact]
    public void WithPersistence_WhenIJSRuntimeAvailable_ConfiguresPersistence()
    {
        // Arrange
        var services = new ServiceCollection();
        var jsRuntimeMock = new Mock<IJSRuntime>();
        services.AddScoped<IJSRuntime>(_ => jsRuntimeMock.Object);

        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var builder = StoreBuilder<TestState>.Create(new TestState(0));

        // Act - should not throw
        var result = builder.WithPersistence(scope.ServiceProvider, "test-key");

        // Assert
        result.Should().NotBeNull();
        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void WithPersistence_WhenIJSRuntimeNotAvailable_ThrowsInvalidOperationException()
    {
        // Arrange
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();
        var builder = StoreBuilder<TestState>.Create(new TestState(0));

        // Act & Assert
        var act = () => builder.WithPersistence(provider, "test-key");
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*IJSRuntime could not be resolved*")
            .WithMessage("*AddScopedStore*");
    }

    [Fact]
    public void WithPersistence_WhenServiceProviderNull_ThrowsArgumentNullException()
    {
        // Arrange
        var builder = StoreBuilder<TestState>.Create(new TestState(0));

        // Act & Assert
        var act = () => builder.WithPersistence(null!, "test-key");
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void WithPersistence_WhenKeyNull_ThrowsArgumentException()
    {
        // Arrange
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();
        var builder = StoreBuilder<TestState>.Create(new TestState(0));

        // Act & Assert
        var act = () => builder.WithPersistence(provider, null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void WithPersistence_WhenKeyEmpty_ThrowsArgumentException()
    {
        // Arrange
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();
        var builder = StoreBuilder<TestState>.Create(new TestState(0));

        // Act & Assert
        var act = () => builder.WithPersistence(provider, "");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void WithPersistence_WhenKeyWhitespace_ThrowsArgumentException()
    {
        // Arrange
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();
        var builder = StoreBuilder<TestState>.Create(new TestState(0));

        // Act & Assert
        var act = () => builder.WithPersistence(provider, "   ");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void WithPersistence_ErrorMessage_GuidesToScopedStore()
    {
        // Arrange - no IJSRuntime registered
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();
        var builder = StoreBuilder<TestState>.Create(new TestState(0));

        // Act
        InvalidOperationException? capturedException = null;
        try
        {
            builder.WithPersistence(provider, "test-key");
        }
        catch (InvalidOperationException ex)
        {
            capturedException = ex;
        }

        // Assert - error message should guide users to the solution
        capturedException.Should().NotBeNull();
        capturedException!.Message.Should().Contain("AddScopedStore");
        capturedException.Message.Should().Contain("AddScopedStoreWithUtilities");
        capturedException.Message.Should().Contain(nameof(TestState));
    }

    [Fact]
    public void WithPersistence_WithScopedStore_IntegrationTest()
    {
        // Arrange - simulate real scoped store scenario
        var services = new ServiceCollection();
        var jsRuntimeMock = new Mock<IJSRuntime>();
        services.AddScoped<IJSRuntime>(_ => jsRuntimeMock.Object);

        // Register scoped store with persistence
        services.AddScopedStore(
            new TestState(42),
            (builder, sp) => builder.WithPersistence(sp, "test-state"));

        var provider = services.BuildServiceProvider();

        // Act
        using var scope = provider.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IStore<TestState>>();

        // Assert
        store.Should().NotBeNull();
        store.GetState().Value.Should().Be(42);
    }
}
