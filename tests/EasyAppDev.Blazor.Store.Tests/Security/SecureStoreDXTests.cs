using EasyAppDev.Blazor.Store.Blazor;
using EasyAppDev.Blazor.Store.Core;
using EasyAppDev.Blazor.Store.Security;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EasyAppDev.Blazor.Store.Tests.Security;

/// <summary>
/// Tests for the secure store developer experience features.
/// </summary>
public class SecureStoreDXTests
{
    #region Test State and Validators

    public record TestState(int Count, string Name)
    {
        public static TestState Default => new(0, "Test");
    }

    public class TestStateValidator : IStateValidator<TestState>
    {
        public StateValidationResult Validate(TestState state)
        {
            var errors = new List<string>();
            if (state.Count < 0)
                errors.Add("Count cannot be negative");
            if (string.IsNullOrEmpty(state.Name))
                errors.Add("Name is required");

            return errors.Count > 0
                ? StateValidationResult.Failure(errors)
                : StateValidationResult.Success();
        }
    }

    #endregion

    #region SecurityProfile Tests

    [Fact]
    public void SecurityProfile_Development_ShouldBeDefaultInDevelopment()
    {
        // Arrange
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");

        try
        {
            // Act
            var profile = EnvironmentDetection.GetDefaultProfile();

            // Assert
            profile.Should().Be(SecurityProfile.Development);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", null);
        }
    }

    [Fact]
    public void SecurityProfile_Production_ShouldBeDefaultWhenNoEnvironmentSet()
    {
        // Arrange
        var originalEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", null);
        Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", null);

        try
        {
            // Act
            var profile = EnvironmentDetection.GetDefaultProfile();

            // Assert - defaults to Production for security
            profile.Should().Be(SecurityProfile.Production);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", originalEnv);
        }
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("QA")]
    public void SecurityProfile_Production_ShouldBeDefaultForNonDevelopment(string environment)
    {
        // Arrange
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", environment);

        try
        {
            // Act
            var profile = EnvironmentDetection.GetDefaultProfile();

            // Assert
            profile.Should().Be(SecurityProfile.Production);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", null);
        }
    }

    #endregion

    #region Environment Detection Tests

    [Fact]
    public void EnvironmentDetection_IsDevelopment_ShouldReturnTrueForDevelopment()
    {
        // Arrange
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");

        try
        {
            // Act & Assert
            EnvironmentDetection.IsDevelopment().Should().BeTrue();
            EnvironmentDetection.IsProduction().Should().BeFalse();
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", null);
        }
    }

    [Fact]
    public void EnvironmentDetection_ShouldUseDotNetEnvironmentAsFallback()
    {
        // Arrange
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", null);
        Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Development");

        try
        {
            // Act & Assert
            EnvironmentDetection.IsDevelopment().Should().BeTrue();
        }
        finally
        {
            Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", null);
        }
    }

    #endregion

    #region Validator Auto-Discovery Tests

    [Fact]
    public void AddStateValidator_WithType_ShouldRegisterValidator()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddStateValidator<TestState, TestStateValidator>();
        var provider = services.BuildServiceProvider();

        // Assert
        var validator = provider.GetService<IStateValidator<TestState>>();
        validator.Should().NotBeNull();
        validator.Should().BeOfType<TestStateValidator>();
    }

    [Fact]
    public void AddStateValidator_WithInstance_ShouldRegisterValidator()
    {
        // Arrange
        var services = new ServiceCollection();
        var validator = new TestStateValidator();

        // Act
        services.AddStateValidator(validator);
        var provider = services.BuildServiceProvider();

        // Assert
        var resolved = provider.GetService<IStateValidator<TestState>>();
        resolved.Should().BeSameAs(validator);
    }

    [Fact]
    public void AddStateValidator_WithFunc_ShouldRegisterFuncValidator()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddStateValidator<TestState>(state =>
        {
            var errors = new List<string>();
            if (state.Count < 0) errors.Add("Count cannot be negative");
            return errors;
        });
        var provider = services.BuildServiceProvider();

        // Assert
        var validator = provider.GetService<IStateValidator<TestState>>();
        validator.Should().NotBeNull();
        validator.Should().BeOfType<FuncStateValidator<TestState>>();
    }

    [Fact]
    public void FuncStateValidator_ShouldValidateCorrectly()
    {
        // Arrange
        var validator = new FuncStateValidator<TestState>(state =>
        {
            var errors = new List<string>();
            if (state.Count < 0) errors.Add("Count cannot be negative");
            if (string.IsNullOrEmpty(state.Name)) errors.Add("Name is required");
            return errors;
        });

        // Act & Assert - Valid state
        var validResult = validator.Validate(new TestState(5, "Test"));
        validResult.IsValid.Should().BeTrue();

        // Act & Assert - Invalid state
        var invalidResult = validator.Validate(new TestState(-1, ""));
        invalidResult.IsValid.Should().BeFalse();
        invalidResult.Errors.Should().HaveCount(2);
    }

    [Fact]
    public void HasStateValidator_ShouldReturnTrueWhenRegistered()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddStateValidator<TestState, TestStateValidator>();

        // Act & Assert
        services.HasStateValidator<TestState>().Should().BeTrue();
    }

    [Fact]
    public void HasStateValidator_ShouldReturnFalseWhenNotRegistered()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        services.HasStateValidator<TestState>().Should().BeFalse();
    }

    [Fact]
    public void RequireStateValidator_ShouldThrowWhenNotRegistered()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        var act = () => services.RequireStateValidator<TestState>();
        act.Should().Throw<SecurityConfigurationException>()
            .Which.Component.Should().Be("SecureStore");
    }

    [Fact]
    public void RequireStateValidator_ShouldNotThrowWhenRegistered()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddStateValidator<TestState, TestStateValidator>();

        // Act & Assert
        var act = () => services.RequireStateValidator<TestState>();
        act.Should().NotThrow();
    }

    [Fact]
    public void AddStateValidatorsFromAssembly_ShouldDiscoverValidators()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddStateValidatorsFromAssembly(typeof(SecureStoreDXTests).Assembly);
        var provider = services.BuildServiceProvider();

        // Assert - TestStateValidator should be discovered
        var validator = provider.GetService<IStateValidator<TestState>>();
        validator.Should().NotBeNull();
    }

    [Fact]
    public void AddCompositeValidator_ShouldCombineValidators()
    {
        // Arrange
        var services = new ServiceCollection();
        var validator1 = new FuncStateValidator<TestState>(s =>
            s.Count < 0 ? new[] { "Count negative" } : Array.Empty<string>());
        var validator2 = new FuncStateValidator<TestState>(s =>
            string.IsNullOrEmpty(s.Name) ? new[] { "Name empty" } : Array.Empty<string>());

        // Act
        services.AddCompositeValidator(validator1, validator2);
        var provider = services.BuildServiceProvider();
        var composite = provider.GetRequiredService<IStateValidator<TestState>>();

        // Assert - Both errors should be collected
        var result = composite.Validate(new TestState(-1, ""));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Count negative");
        result.Errors.Should().Contain("Name empty");
    }

    #endregion

    #region SecurityConfigurationException Tests

    [Fact]
    public void SecurityConfigurationException_MissingValidator_ShouldHaveCorrectMessage()
    {
        // Act
        var ex = SecurityConfigurationException.MissingValidator("TestState", SecurityProfile.Production);

        // Assert
        ex.Component.Should().Be("SecureStore");
        ex.Profile.Should().Be(SecurityProfile.Production);
        ex.Message.Should().Contain("TestState");
        ex.Message.Should().Contain("validator");
    }

    [Fact]
    public void SecurityConfigurationException_MissingSigningKey_ShouldHaveCorrectMessage()
    {
        // Act
        var ex = SecurityConfigurationException.MissingSigningKey("TabSync", SecurityProfile.Production);

        // Assert
        ex.Component.Should().Be("TabSync");
        ex.Profile.Should().Be(SecurityProfile.Production);
        ex.Message.Should().Contain("signing");
    }

    #endregion

    #region SecureStoreOptions Tests

    [Fact]
    public void SecureStoreOptions_ShouldHaveSensibleDefaults()
    {
        // Act
        var options = new SecureStoreOptions();

        // Assert
        options.Profile.Should().BeNull(); // Auto-detect
        options.EnableTabSync.Should().BeFalse();
        options.EnableHistory.Should().BeFalse();
        options.MaxHistoryEntries.Should().Be(50);
        options.MaxHistoryMemoryMB.Should().Be(10);
        options.UseScoped.Should().BeFalse();
        options.FilterSensitiveData.Should().BeTrue();
        options.PersistenceDebounceMs.Should().Be(100);
    }

    #endregion

    #region AddSecureStore Integration Tests

    [Fact]
    public void AddSecureStore_ShouldRegisterStoreWithUtilities()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddSecureStore(TestState.Default, "Test");
        var provider = services.BuildServiceProvider();

        // Assert
        var store = provider.GetService<IStore<TestState>>();
        store.Should().NotBeNull();
        store!.GetState().Should().Be(TestState.Default);
    }

    [Fact]
    public void AddSecureStore_ShouldUseStateTypeNameAsDefault()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddSecureStore(TestState.Default);
        var provider = services.BuildServiceProvider();

        // Assert
        var store = provider.GetService<IStore<TestState>>();
        store.Should().NotBeNull();
    }

    [Fact]
    public void AddSecureStore_WithValidator_ShouldResolveFromDI()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddStateValidator<TestState, TestStateValidator>();

        // Act
        services.AddSecureStore(TestState.Default, "Test");
        var provider = services.BuildServiceProvider();

        // Assert
        var store = provider.GetService<IStore<TestState>>();
        store.Should().NotBeNull();
    }

    [Fact]
    public void AddSecureStore_WithPersistence_ShouldConfigurePersistence()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddSecureStore(TestState.Default, "Test", opts =>
        {
            opts.PersistenceKey = "test-state";
        });
        var provider = services.BuildServiceProvider();

        // Assert
        var store = provider.GetService<IStore<TestState>>();
        store.Should().NotBeNull();
    }

    [Fact]
    public void AddSecureStore_WithHistory_ShouldEnableHistory()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddSecureStore(TestState.Default, "Test", opts =>
        {
            opts.EnableHistory = true;
            opts.MaxHistoryEntries = 100;
        });
        var provider = services.BuildServiceProvider();

        // Assert
        var store = provider.GetService<IStore<TestState>>();
        store.Should().NotBeNull();
    }

    [Fact]
    public void AddSecureStore_Strict_WithoutValidator_ShouldThrow()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddSecureStore(TestState.Default, "Test", opts =>
        {
            opts.Profile = SecurityProfile.Strict;
        });

        // Assert - Should throw when building provider (during store creation)
        var act = () => services.BuildServiceProvider().GetService<IStore<TestState>>();
        act.Should().Throw<SecurityConfigurationException>();
    }

    [Fact]
    public void AddSecureStore_Strict_WithValidator_ShouldSucceed()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddStateValidator<TestState, TestStateValidator>();

        // Act
        services.AddSecureStore(TestState.Default, "Test", opts =>
        {
            opts.Profile = SecurityProfile.Strict;
        });

        // Assert
        var provider = services.BuildServiceProvider();
        var store = provider.GetService<IStore<TestState>>();
        store.Should().NotBeNull();
    }

    [Fact]
    public void AddSecureStore_Development_ShouldBePermissive()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act - No validator, should still work in Development
        services.AddSecureStore(TestState.Default, "Test", opts =>
        {
            opts.Profile = SecurityProfile.Development;
        });

        // Assert
        var provider = services.BuildServiceProvider();
        var store = provider.GetService<IStore<TestState>>();
        store.Should().NotBeNull();
    }

    [Fact]
    public void AddSecureStore_Scoped_ShouldRegisterAsScopedService()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddSecureStore(TestState.Default, "Test", opts =>
        {
            opts.UseScoped = true;
        });

        // Assert
        var descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(IStore<TestState>));
        descriptor.Should().NotBeNull();
        descriptor!.Lifetime.Should().Be(ServiceLifetime.Scoped);
    }

    #endregion

    #region WithSecurityProfile Builder Tests

    [Fact]
    public void WithSecurityProfile_Development_ShouldEnableDevToolsInDebug()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddStore(TestState.Default, (builder, sp) =>
            builder.WithSecurityProfile(sp, SecurityProfile.Development, "Test"));
        var provider = services.BuildServiceProvider();

        // Assert
        var store = provider.GetService<IStore<TestState>>();
        store.Should().NotBeNull();
    }

    [Fact]
    public void WithSecurityProfile_Production_ShouldNotEnableDevTools()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddStore(TestState.Default, (builder, sp) =>
            builder.WithSecurityProfile(sp, SecurityProfile.Production, "Test"));
        var provider = services.BuildServiceProvider();

        // Assert
        var store = provider.GetService<IStore<TestState>>();
        store.Should().NotBeNull();
    }

    [Fact]
    public void WithSecurityProfile_Strict_WithoutValidator_ShouldThrow()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act & Assert
        services.AddStore(TestState.Default, (builder, sp) =>
            builder.WithSecurityProfile(sp, SecurityProfile.Strict, "Test"));

        var act = () => services.BuildServiceProvider().GetService<IStore<TestState>>();
        act.Should().Throw<SecurityConfigurationException>();
    }

    [Fact]
    public void WithEnvironmentDefaults_ShouldAutoDetectProfile()
    {
        // Arrange
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        var services = new ServiceCollection();
        services.AddLogging();

        try
        {
            // Act
            services.AddStore(TestState.Default, (builder, sp) =>
                builder.WithEnvironmentDefaults(sp, "Test"));
            var provider = services.BuildServiceProvider();

            // Assert
            var store = provider.GetService<IStore<TestState>>();
            store.Should().NotBeNull();
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", null);
        }
    }

    #endregion
}
