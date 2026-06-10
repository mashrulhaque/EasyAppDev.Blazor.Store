using EasyAppDev.Blazor.Store.Blazor.UrlSync;
using FluentAssertions;
using Xunit;

namespace EasyAppDev.Blazor.Store.Tests.UrlSync;

public class DefaultUrlValueConverterTests
{
    #region Integer Types

    [Theory]
    [InlineData("42", 42)]
    [InlineData("0", 0)]
    [InlineData("-100", -100)]
    [InlineData("2147483647", int.MaxValue)]
    [InlineData("-2147483648", int.MinValue)]
    public void FromUrl_Int_ConvertsCorrectly(string input, int expected)
    {
        var converter = new DefaultUrlValueConverter<int>();
        var result = converter.FromUrl(input);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("12.5")]
    [InlineData("999999999999999")]
    public void FromUrl_Int_InvalidInput_ReturnsDefault(string? input)
    {
        var converter = new DefaultUrlValueConverter<int>();
        var result = converter.FromUrl(input);
        result.Should().Be(0);
    }

    [Theory]
    [InlineData(42, "42")]
    [InlineData(0, "0")]
    [InlineData(-100, "-100")]
    [InlineData(int.MaxValue, "2147483647")]
    public void ToUrl_Int_ConvertsCorrectly(int input, string expected)
    {
        var converter = new DefaultUrlValueConverter<int>();
        var result = converter.ToUrl(input);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("42", 42L)]
    [InlineData("9223372036854775807", long.MaxValue)]
    [InlineData("-9223372036854775808", long.MinValue)]
    public void FromUrl_Long_ConvertsCorrectly(string input, long expected)
    {
        var converter = new DefaultUrlValueConverter<long>();
        var result = converter.FromUrl(input);
        result.Should().Be(expected);
    }

    #endregion

    #region Nullable Types

    [Fact]
    public void FromUrl_NullableInt_Null_ReturnsNull()
    {
        var converter = new DefaultUrlValueConverter<int?>();
        var result = converter.FromUrl(null);
        result.Should().BeNull();
    }

    [Fact]
    public void FromUrl_NullableInt_ValidValue_ReturnsValue()
    {
        var converter = new DefaultUrlValueConverter<int?>();
        var result = converter.FromUrl("42");
        result.Should().Be(42);
    }

    [Fact]
    public void ToUrl_NullableInt_Null_ReturnsNull()
    {
        var converter = new DefaultUrlValueConverter<int?>();
        var result = converter.ToUrl(null);
        result.Should().BeNull();
    }

    [Fact]
    public void ToUrl_NullableInt_Value_ReturnsString()
    {
        var converter = new DefaultUrlValueConverter<int?>();
        var result = converter.ToUrl(42);
        result.Should().Be("42");
    }

    #endregion

    #region Boolean

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("True", true)]
    [InlineData("False", false)]
    [InlineData("TRUE", true)]
    [InlineData("FALSE", false)]
    public void FromUrl_Bool_ConvertsCorrectly(string input, bool expected)
    {
        var converter = new DefaultUrlValueConverter<bool>();
        var result = converter.FromUrl(input);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1")]
    [InlineData("0")]
    [InlineData("yes")]
    [InlineData("no")]
    public void FromUrl_Bool_InvalidInput_ReturnsDefault(string? input)
    {
        var converter = new DefaultUrlValueConverter<bool>();
        var result = converter.FromUrl(input);
        result.Should().Be(false);
    }

    [Theory]
    [InlineData(true, "true")]
    [InlineData(false, "false")]
    public void ToUrl_Bool_ConvertsCorrectly(bool input, string expected)
    {
        var converter = new DefaultUrlValueConverter<bool>();
        var result = converter.ToUrl(input);
        result.Should().Be(expected);
    }

    #endregion

    #region String

    [Theory]
    [InlineData("hello", "hello")]
    [InlineData("  spaces  ", "  spaces  ")]
    [InlineData("special!@#$%", "special!@#$%")]
    public void FromUrl_String_ReturnsInput(string input, string expected)
    {
        var converter = new DefaultUrlValueConverter<string>();
        var result = converter.FromUrl(input);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void FromUrl_String_NullOrEmpty_ReturnsNull(string? input)
    {
        var converter = new DefaultUrlValueConverter<string>();
        var result = converter.FromUrl(input);
        result.Should().BeNull();
    }

    [Fact]
    public void ToUrl_String_ValidValue_ReturnsInput()
    {
        var converter = new DefaultUrlValueConverter<string>();
        var result = converter.ToUrl("hello");
        result.Should().Be("hello");
    }

    [Fact]
    public void ToUrl_String_Null_ReturnsNull()
    {
        var converter = new DefaultUrlValueConverter<string>();
        var result = converter.ToUrl(null);
        result.Should().BeNull();
    }

    #endregion

    #region Guid

    [Fact]
    public void FromUrl_Guid_ValidGuid_ConvertsCorrectly()
    {
        var guid = Guid.NewGuid();
        var converter = new DefaultUrlValueConverter<Guid>();

        var result = converter.FromUrl(guid.ToString());

        result.Should().Be(guid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-guid")]
    [InlineData("12345")]
    public void FromUrl_Guid_InvalidInput_ReturnsEmpty(string? input)
    {
        var converter = new DefaultUrlValueConverter<Guid>();
        var result = converter.FromUrl(input);
        result.Should().Be(Guid.Empty);
    }

    [Fact]
    public void ToUrl_Guid_ConvertsCorrectly()
    {
        var guid = Guid.Parse("12345678-1234-1234-1234-123456789abc");
        var converter = new DefaultUrlValueConverter<Guid>();

        var result = converter.ToUrl(guid);

        result.Should().Be("12345678-1234-1234-1234-123456789abc");
    }

    #endregion

    #region DateTime

    [Fact]
    public void FromUrl_DateTime_ISO8601_ConvertsCorrectly()
    {
        var dateTime = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);
        var converter = new DefaultUrlValueConverter<DateTime>();

        var result = converter.FromUrl(dateTime.ToString("O"));

        result.Should().Be(dateTime);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-date")]
    public void FromUrl_DateTime_InvalidInput_ReturnsDefault(string? input)
    {
        var converter = new DefaultUrlValueConverter<DateTime>();
        var result = converter.FromUrl(input);
        result.Should().Be(default(DateTime));
    }

    [Fact]
    public void ToUrl_DateTime_UsesRoundtripFormat()
    {
        var dateTime = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);
        var converter = new DefaultUrlValueConverter<DateTime>();

        var result = converter.ToUrl(dateTime);

        result.Should().Contain("2024-01-15");
        result.Should().Contain("T");
    }

    #endregion

    #region Enum

    public enum TestEnum
    {
        None = 0,
        Active = 1,
        Inactive = 2
    }

    [Theory]
    [InlineData("Active", TestEnum.Active)]
    [InlineData("Inactive", TestEnum.Inactive)]
    [InlineData("None", TestEnum.None)]
    [InlineData("active", TestEnum.Active)]  // Case insensitive
    [InlineData("INACTIVE", TestEnum.Inactive)]
    public void FromUrl_Enum_ConvertsCorrectly(string input, TestEnum expected)
    {
        var converter = new DefaultUrlValueConverter<TestEnum>();
        var result = converter.FromUrl(input);
        result.Should().Be(expected);
    }

    [Fact]
    public void FromUrl_Enum_Null_ReturnsDefault()
    {
        var converter = new DefaultUrlValueConverter<TestEnum>();
        var result = converter.FromUrl(null);
        result.Should().Be(TestEnum.None);
    }

    [Theory]
    [InlineData("InvalidValue")]
    public void FromUrl_Enum_InvalidInput_ReturnsDefault(string? input)
    {
        var converter = new DefaultUrlValueConverter<TestEnum>();
        var result = converter.FromUrl(input);
        result.Should().Be(TestEnum.None);
    }

    [Fact]
    public void FromUrl_Enum_NumericString_ParsesAsInt()
    {
        // Enum.Parse allows numeric strings even if not defined
        var converter = new DefaultUrlValueConverter<TestEnum>();
        var result = converter.FromUrl("999");
        result.Should().Be((TestEnum)999);
    }

    [Theory]
    [InlineData(TestEnum.Active, "Active")]
    [InlineData(TestEnum.Inactive, "Inactive")]
    [InlineData(TestEnum.None, "None")]
    public void ToUrl_Enum_ConvertsCorrectly(TestEnum input, string expected)
    {
        var converter = new DefaultUrlValueConverter<TestEnum>();
        var result = converter.ToUrl(input);
        result.Should().Be(expected);
    }

    #endregion

    #region Floating Point

    [Theory]
    [InlineData("3.14", 3.14)]
    [InlineData("0.0", 0.0)]
    [InlineData("-1.5", -1.5)]
    public void FromUrl_Double_ConvertsCorrectly(string input, double expected)
    {
        var converter = new DefaultUrlValueConverter<double>();
        var result = converter.FromUrl(input);
        result.Should().BeApproximately(expected, 0.0001);
    }

    [Theory]
    [InlineData("3.14", 3.14f)]
    [InlineData("0.0", 0.0f)]
    [InlineData("-1.5", -1.5f)]
    public void FromUrl_Float_ConvertsCorrectly(string input, float expected)
    {
        var converter = new DefaultUrlValueConverter<float>();
        var result = converter.FromUrl(input);
        result.Should().BeApproximately(expected, 0.0001f);
    }

    #endregion

    #region Thousands Separators (must fail cleanly, not silently parse "1,5" as 15)

    [Fact]
    public void FromUrl_Double_WithThousandsSeparator_FailsCleanlyAndInvokesErrorCallback()
    {
        string? capturedParam = null;
        var converter = new DefaultUrlValueConverter<double>((param, _) => capturedParam = param);

        var result = converter.FromUrl("1,5");

        result.Should().Be(0.0, "\"1,5\" must not silently parse as 15");
        capturedParam.Should().Be("1,5");
    }

    [Fact]
    public void FromUrl_Float_WithThousandsSeparator_FailsCleanly()
    {
        var converter = new DefaultUrlValueConverter<float>();
        converter.FromUrl("1,5").Should().Be(0.0f);
    }

    [Fact]
    public void FromUrl_Decimal_WithThousandsSeparator_FailsCleanly()
    {
        var converter = new DefaultUrlValueConverter<decimal>();
        converter.FromUrl("1,5").Should().Be(0m);
    }

    [Fact]
    public void FromUrl_Double_PlainDecimalValue_StillParses()
    {
        var converter = new DefaultUrlValueConverter<double>();
        converter.FromUrl("1.5").Should().Be(1.5);
        converter.FromUrl("-2.75e2").Should().Be(-275.0);
    }

    #endregion

    #region Error Handling

    [Fact]
    public void FromUrl_ErrorCallback_InvokedOnConversionFailure()
    {
        string? capturedParam = null;
        Exception? capturedException = null;

        var converter = new DefaultUrlValueConverter<int>((param, ex) =>
        {
            capturedParam = param;
            capturedException = ex;
        });

        var result = converter.FromUrl("not-a-number");

        result.Should().Be(0);
        capturedParam.Should().Be("not-a-number");
        capturedException.Should().NotBeNull();
    }

    [Fact]
    public void FromUrl_ErrorCallbackProvider_IsReadAtConversionTime()
    {
        // The provider must be consulted at CONVERSION time, not captured at
        // construction time, so handlers registered later are honored.
        Action<string, Exception>? handler = null;
        var converter = new DefaultUrlValueConverter<int>(() => handler);

        // No handler registered yet - conversion fails silently
        converter.FromUrl("bad").Should().Be(0);

        // Handler registered AFTER the converter was created
        string? capturedParam = null;
        handler = (param, _) => capturedParam = param;

        converter.FromUrl("still-bad").Should().Be(0);
        capturedParam.Should().Be("still-bad");
    }

    #endregion
}
