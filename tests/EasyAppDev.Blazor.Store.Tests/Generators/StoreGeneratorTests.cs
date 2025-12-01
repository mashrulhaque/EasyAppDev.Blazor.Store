using System.Collections.Immutable;
using EasyAppDev.Blazor.Store.Generators;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace EasyAppDev.Blazor.Store.Tests.Generators;

public class StoreGeneratorTests
{
    [Fact]
    public void Generator_WithSimpleRecord_GeneratesSetAndUpdateMethods()
    {
        // Arrange
        var source = """
            using EasyAppDev.Blazor.Store.Generators;

            namespace TestNamespace;

            [Store]
            public partial record CounterState(int Count, string? Name);
            """;

        // Act
        var (diagnostics, output) = RunGenerator(source);

        // Assert
        diagnostics.Should().BeEmpty();
        output.Should().Contain("public CounterState SetCount(int value)");
        output.Should().Contain("=> this with { Count = value };");
        output.Should().Contain("public CounterState UpdateCount(System.Func<int, int> updater)");
        output.Should().Contain("=> this with { Count = updater(Count) };");
        output.Should().Contain("public CounterState SetName(string? value)");
        output.Should().Contain("public CounterState UpdateName(System.Func<string?, string?> updater)");
    }

    [Fact]
    public void Generator_WithGenerateWithMethods_GeneratesWithAliases()
    {
        // Arrange
        var source = """
            using EasyAppDev.Blazor.Store.Generators;

            namespace TestNamespace;

            [Store(GenerateWithMethods = true)]
            public partial record AppState(bool IsLoading);
            """;

        // Act
        var (diagnostics, output) = RunGenerator(source);

        // Assert
        diagnostics.Should().BeEmpty();
        output.Should().Contain("public AppState SetIsLoading(bool value)");
        output.Should().Contain("public AppState WithIsLoading(bool value)");
    }

    [Fact]
    public void Generator_WithGenerateActions_GeneratesActionRecords()
    {
        // Arrange
        var source = """
            using EasyAppDev.Blazor.Store.Generators;

            namespace TestNamespace;

            [Store(GenerateActions = true)]
            public partial record CounterState(int Count);
            """;

        // Act
        var (diagnostics, output) = RunGenerator(source);

        // Assert
        diagnostics.Should().BeEmpty();
        output.Should().Contain("public static partial class CounterStateActions");
        output.Should().Contain("public record SetCount(int Value) : EasyAppDev.Blazor.Store.Actions.IAction;");
        output.Should().Contain("public static partial class CounterStateReducerExtensions");
        output.Should().Contain("WithGeneratedReducers");
    }

    [Fact]
    public void Generator_WithImmutableCollection_GeneratesCollectionMethods()
    {
        // Arrange
        var source = """
            using System.Collections.Immutable;
            using EasyAppDev.Blazor.Store.Generators;

            namespace TestNamespace;

            [Store]
            public partial record TodoState(
                [property: ImmutableCollection] ImmutableList<string> Items);
            """;

        // Act
        var (diagnostics, output) = RunGenerator(source);

        // Assert
        diagnostics.Should().BeEmpty();
        output.Should().Contain("public TodoState AddItem(string item)");
        output.Should().Contain("=> this with { Items = Items.Add(item) };");
        output.Should().Contain("public TodoState RemoveItem(string item)");
        output.Should().Contain("=> this with { Items = Items.Remove(item) };");
        output.Should().Contain("public TodoState ClearItems()");
        output.Should().Contain("=> this with { Items = Items.Clear() };");
    }

    [Fact]
    public void Generator_WithComputedProperty_SkipsSetterGeneration()
    {
        // Arrange
        var source = """
            using EasyAppDev.Blazor.Store.Generators;

            namespace TestNamespace;

            [Store]
            public partial record StatsState(
                int Total,
                [property: Computed] int Average);
            """;

        // Act
        var (diagnostics, output) = RunGenerator(source);

        // Assert
        diagnostics.Should().BeEmpty();
        output.Should().Contain("public StatsState SetTotal(int value)");
        output.Should().NotContain("SetAverage");
        output.Should().NotContain("UpdateAverage");
    }

    [Fact]
    public void Generator_WithNamespace_PreservesNamespace()
    {
        // Arrange
        var source = """
            using EasyAppDev.Blazor.Store.Generators;

            namespace MyApp.Features.Counter;

            [Store]
            public partial record CounterState(int Count);
            """;

        // Act
        var (diagnostics, output) = RunGenerator(source);

        // Assert
        diagnostics.Should().BeEmpty();
        output.Should().Contain("namespace MyApp.Features.Counter;");
    }

    [Fact]
    public void Generator_WithoutNamespace_OmitsNamespaceDeclaration()
    {
        // Arrange
        var source = """
            using EasyAppDev.Blazor.Store.Generators;

            [Store]
            public partial record GlobalState(string Value);
            """;

        // Act
        var (diagnostics, output) = RunGenerator(source);

        // Assert
        diagnostics.Should().BeEmpty();
        output.Should().NotContain("namespace ");
        output.Should().Contain("public partial record GlobalState");
    }

    [Fact]
    public void Generator_WithNullableProperty_PreservesNullability()
    {
        // Arrange
        var source = """
            using EasyAppDev.Blazor.Store.Generators;

            namespace TestNamespace;

            [Store]
            public partial record UserState(string? Email, int Age);
            """;

        // Act
        var (diagnostics, output) = RunGenerator(source);

        // Assert
        diagnostics.Should().BeEmpty();
        output.Should().Contain("public UserState SetEmail(string? value)");
        output.Should().Contain("public UserState SetAge(int value)");
    }

    [Fact]
    public void Generator_WithPluralCollectionName_GeneratesSingularMethods()
    {
        // Arrange
        var source = """
            using System.Collections.Immutable;
            using EasyAppDev.Blazor.Store.Generators;

            namespace TestNamespace;

            [Store]
            public partial record LibraryState(
                [property: ImmutableCollection] ImmutableList<string> Books,
                [property: ImmutableCollection] ImmutableList<string> Categories,
                [property: ImmutableCollection] ImmutableList<string> Boxes);
            """;

        // Act
        var (diagnostics, output) = RunGenerator(source);

        // Assert
        diagnostics.Should().BeEmpty();
        // "Books" -> "Book"
        output.Should().Contain("public LibraryState AddBook(string item)");
        output.Should().Contain("public LibraryState RemoveBook(string item)");
        // "Categories" -> "Category" (ies -> y)
        output.Should().Contain("public LibraryState AddCategory(string item)");
        // "Boxes" -> "Box" (es removed)
        output.Should().Contain("public LibraryState AddBox(string item)");
    }

    [Fact]
    public void Generator_WithMultipleProperties_GeneratesAllMethods()
    {
        // Arrange
        var source = """
            using EasyAppDev.Blazor.Store.Generators;

            namespace TestNamespace;

            [Store]
            public partial record FormState(
                string FirstName,
                string LastName,
                int Age,
                bool IsValid);
            """;

        // Act
        var (diagnostics, output) = RunGenerator(source);

        // Assert
        diagnostics.Should().BeEmpty();
        output.Should().Contain("SetFirstName");
        output.Should().Contain("SetLastName");
        output.Should().Contain("SetAge");
        output.Should().Contain("SetIsValid");
        output.Should().Contain("UpdateFirstName");
        output.Should().Contain("UpdateLastName");
        output.Should().Contain("UpdateAge");
        output.Should().Contain("UpdateIsValid");
    }

    [Fact]
    public void Generator_GeneratesXmlDocumentation()
    {
        // Arrange
        var source = """
            using EasyAppDev.Blazor.Store.Generators;

            namespace TestNamespace;

            [Store]
            public partial record DocState(string Content);
            """;

        // Act
        var (diagnostics, output) = RunGenerator(source);

        // Assert
        diagnostics.Should().BeEmpty();
        output.Should().Contain("/// <summary>Sets the Content property.</summary>");
        output.Should().Contain("/// <summary>Updates the Content property using a transform function.</summary>");
    }

    [Fact]
    public void Generator_WithComplexType_HandlesTypeCorrectly()
    {
        // Arrange
        var source = """
            using System;
            using System.Collections.Generic;
            using EasyAppDev.Blazor.Store.Generators;

            namespace TestNamespace;

            [Store]
            public partial record ComplexState(
                DateTime CreatedAt,
                Dictionary<string, int> Counts,
                List<string> Tags);
            """;

        // Act
        var (diagnostics, output) = RunGenerator(source);

        // Assert
        diagnostics.Should().BeEmpty();
        output.Should().Contain("public ComplexState SetCreatedAt(System.DateTime value)");
        output.Should().Contain("public ComplexState SetCounts(System.Collections.Generic.Dictionary<string, int> value)");
        output.Should().Contain("public ComplexState SetTags(System.Collections.Generic.List<string> value)");
    }

    [Fact]
    public void Generator_OutputContainsAutoGeneratedHeader()
    {
        // Arrange
        var source = """
            using EasyAppDev.Blazor.Store.Generators;

            namespace TestNamespace;

            [Store]
            public partial record SimpleState(int Value);
            """;

        // Act
        var (diagnostics, output) = RunGenerator(source);

        // Assert
        diagnostics.Should().BeEmpty();
        output.Should().Contain("// <auto-generated/>");
        output.Should().Contain("#nullable enable");
    }

    [Fact]
    public void Generator_WithBothOptions_GeneratesEverything()
    {
        // Arrange
        var source = """
            using EasyAppDev.Blazor.Store.Generators;

            namespace TestNamespace;

            [Store(GenerateActions = true, GenerateWithMethods = true)]
            public partial record FullState(int Count, string Name);
            """;

        // Act
        var (diagnostics, output) = RunGenerator(source);

        // Assert
        diagnostics.Should().BeEmpty();
        // Set methods
        output.Should().Contain("public FullState SetCount(int value)");
        output.Should().Contain("public FullState SetName(string value)");
        // With methods
        output.Should().Contain("public FullState WithCount(int value)");
        output.Should().Contain("public FullState WithName(string value)");
        // Update methods
        output.Should().Contain("public FullState UpdateCount");
        output.Should().Contain("public FullState UpdateName");
        // Actions
        output.Should().Contain("public static partial class FullStateActions");
        output.Should().Contain("public record SetCount(int Value)");
        output.Should().Contain("public record SetName(string Value)");
        // Reducer extension
        output.Should().Contain("WithGeneratedReducers");
    }

    private static (ImmutableArray<Diagnostic> Diagnostics, string Output) RunGenerator(string source)
    {
        // Create the attribute source that would normally come from the main library
        var attributeSource = """
            namespace EasyAppDev.Blazor.Store.Generators
            {
                [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
                public sealed class StoreAttribute : System.Attribute
                {
                    public bool GenerateActions { get; set; }
                    public bool GenerateWithMethods { get; set; }
                }

                [System.AttributeUsage(System.AttributeTargets.Property | System.AttributeTargets.Parameter, Inherited = false, AllowMultiple = false)]
                public sealed class ImmutableCollectionAttribute : System.Attribute { }

                [System.AttributeUsage(System.AttributeTargets.Property | System.AttributeTargets.Parameter, Inherited = false, AllowMultiple = false)]
                public sealed class ComputedAttribute : System.Attribute { }

                [System.AttributeUsage(System.AttributeTargets.Property | System.AttributeTargets.Parameter, Inherited = false, AllowMultiple = false)]
                public sealed class TransientAttribute : System.Attribute { }
            }

            namespace EasyAppDev.Blazor.Store.Actions
            {
                public interface IAction { }
            }
            """;

        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var attributeTree = CSharpSyntaxTree.ParseText(attributeSource);

        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(ImmutableList<>).Assembly.Location),
        };

        // Add reference to System.Runtime
        var runtimeAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "System.Runtime");
        if (runtimeAssembly != null)
        {
            references = references.Append(MetadataReference.CreateFromFile(runtimeAssembly.Location)).ToArray();
        }

        // Add netstandard reference
        var netstandardAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "netstandard");
        if (netstandardAssembly != null)
        {
            references = references.Append(MetadataReference.CreateFromFile(netstandardAssembly.Location)).ToArray();
        }

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree, attributeTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new StoreGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out var diagnostics);

        var runResult = driver.GetRunResult();

        var generatedOutput = runResult.GeneratedTrees
            .Where(t => t.FilePath.EndsWith(".g.cs"))
            .Select(t => t.GetText().ToString())
            .FirstOrDefault() ?? string.Empty;

        return (diagnostics, generatedOutput);
    }
}
