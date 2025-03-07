namespace Simple.Expressions.Sharp.Tests;

public class FluentTypeTests
{
    [Fact]
    public void FluentType_TFirst_TSecond_ShouldInitializeWithFirst()
    {
        var value = new FluentType<int, string>(42);
        Assert.True(value.IsFirst);
        Assert.False(value.IsSecond);
        Assert.Equal(42, value.First);
        Assert.Equal(default, value.Second);
    }

    [Fact]
    public void FluentType_TFirst_TSecond_ShouldInitializeWithSecond()
    {
        var value = new FluentType<int, string>("test");
        Assert.False(value.IsFirst);
        Assert.True(value.IsSecond);
        Assert.Equal(default, value.First);
        Assert.Equal("test", value.Second);
    }

    [Fact]
    public void FluentType_TFirst_TSecond_ShouldReturnCorrectCurrentType()
    {
        var value1 = new FluentType<int, string>(42);
        var value2 = new FluentType<int, string>("test");
        Assert.Equal(typeof(int), value1.CurrentType);
        Assert.Equal(typeof(string), value2.CurrentType);
    }

    [Fact]
    public void FluentType_TFirst_TSecond_ShouldEqualSameType()
    {
        var value1 = new FluentType<int, string>(42);
        var value2 = new FluentType<int, string>(42);
        Assert.Equal(value1, value2);
    }

    [Fact]
    public void FluentType_TFirst_TSecond_ShouldNotEqualDifferentType()
    {
        var value1 = new FluentType<int, string>(42);
        var value2 = new FluentType<int, string>("test");
        Assert.NotEqual(value1, value2);
    }

    [Fact]
    public void FluentType_TFirst_TSecond_ShouldImplicitlyConvertFromFirst()
    {
        FluentType<int, string> value = 42;
        Assert.True(value.IsFirst);
        Assert.Equal(42, value.First);
    }

    [Fact]
    public void FluentType_TFirst_TSecond_ShouldImplicitlyConvertFromSecond()
    {
        FluentType<int, string> value = "test";
        Assert.True(value.IsSecond);
        Assert.Equal("test", value.Second);
    }

    [Fact]
    public void FluentType_TFirst_TSecond_ShouldThrowInvalidCastExceptionForFirst()
    {
        FluentType<int, string> value = "test";
        Assert.Throws<InvalidCastException>(() => (int)value);
    }

    [Fact]
    public void FluentType_TFirst_TSecond_ShouldThrowInvalidCastExceptionForSecond()
    {
        FluentType<int, string> value = 42;
        Assert.Throws<InvalidCastException>(() => (string)value);
    }

    [Fact]
    public void FluentType_TFirst_TSecond_ShouldSwitchCaseCorrectly()
    {
        var value = new FluentType<int, string>(42);
        var result = value.SwitchCase(first => first.ToString(), second => second, () => "default");
        Assert.Equal("42", result);
    }

    [Fact]
    public void FluentType_TFirst_TSecond_ShouldSwitchCaseOrDefaultCorrectly()
    {
        var value = new FluentType<int, string>("test");
        var result = value.SwitchCaseOrDefault(first => first.ToString(), second => second, "default");
        Assert.Equal("test", result);
    }

    [Fact]
    public void FluentType_TFirst_TSecond_ShouldSwitchCaseWithOnMismatch()
    {
        var value = new FluentType<int, string>("test");
        var exception = new Exception("Mismatch");
        var result = Assert.Throws<Exception>(() => value.SwitchCase(first => first.ToString(), onMismatch: () => exception));
        Assert.Equal("Mismatch", result.Message);
    }

    [Fact]
    public void FluentType_TFirst_TSecond_ShouldWithFirstCorrectly()
    {
        var value = new FluentType<int, string>(42);
        var newValue = value.WithFirst(first => first + 1);
        Assert.Equal(43, newValue.First);
    }

    [Fact]
    public void FluentType_TFirst_TSecond_ShouldWithSecondCorrectly()
    {
        var value = new FluentType<int, string>("test");
        var newValue = value.WithSecond(second => second + "1");
        Assert.Equal("test1", newValue.Second);
    }

    [Fact]
    public void FluentType_TFirst_TSecond_ShouldToStringCorrectly()
    {
        var value = new FluentType<int, string>(42);
        Assert.Equal("42", value.ToString());
    }

    [Fact]
    public void FluentType_TFirst_TSecond_ShouldGetHashCodeCorrectly()
    {
        var value = new FluentType<int, string>(42);
        var hashCode = value.GetHashCode();
        Assert.NotEqual(0, hashCode);
    }

    [Fact]
    public void FluentType_TFirst_TSecond_TThird_ShouldInitializeWithFirst()
    {
        var value = new FluentType<int, string, double>(42);
        Assert.True(value.IsFirst);
        Assert.False(value.IsSecond);
        Assert.False(value.IsThird);
        Assert.Equal(42, value.First);
        Assert.Equal(default, value.Second);
        Assert.Equal(default, value.Third);
    }

    [Fact]
    public void FluentType_TFirst_TSecond_TThird_ShouldInitializeWithSecond()
    {
        var value = new FluentType<int, string, double>("test");
        Assert.False(value.IsFirst);
        Assert.True(value.IsSecond);
        Assert.False(value.IsThird);
        Assert.Equal(default, value.First);
        Assert.Equal("test", value.Second);
        Assert.Equal(default, value.Third);
    }

    [Fact]
    public void FluentType_TFirst_TSecond_TThird_ShouldInitializeWithThird()
    {
        var value = new FluentType<int, string, double>(42.0);
        Assert.False(value.IsFirst);
        Assert.False(value.IsSecond);
        Assert.True(value.IsThird);
        Assert.Equal(default, value.First);
        Assert.Equal(default, value.Second);
        Assert.Equal(42.0, value.Third);
    }

    [Fact]
    public void FluentType_TFirst_TSecond_TThird_ShouldReturnCorrectCurrentType()
    {
        var value1 = new FluentType<int, string, double>(42);
        var value2 = new FluentType<int, string, double>("test");
        var value3 = new FluentType<int, string, double>(42.0);
        Assert.Equal(typeof(int), value1.CurrentType);
        Assert.Equal(typeof(string), value2.CurrentType);
        Assert.Equal(typeof(double), value3.CurrentType);
    }

    [Fact]
    public void FluentType_TFirst_TSecond_TThird_ShouldEqualSameType()
    {
        var value1 = new FluentType<int, string, double>(42);
        var value2 = new FluentType<int, string, double>(42);
        Assert.Equal(value1, value2);
    }

    [Fact]
    public void FluentType_TFirst_TSecond_TThird_ShouldNotEqualDifferentType()
    {
        var value1 = new FluentType<int, string, double>(42);
        var value2 = new FluentType<int, string, double>("test");
        Assert.NotEqual(value1, value2);
    }

    [Fact]
    public void FluentType_TFirst_TSecond_TThird_ShouldImplicitlyConvertFromFirst()
    {
        FluentType<int, string, double> value = 42;
        Assert.True(value.IsFirst);
        Assert.Equal(42, value.First);
    }

    [Fact]
    public void FluentType_TFirst_TSecond_TThird_ShouldImplicitlyConvertFromSecond()
    {
        FluentType<int, string, double> value = "test";
        Assert.True(value.IsSecond);
        Assert.Equal("test", value.Second);
    }

    [Fact]
    public void FluentType_TFirst_TSecond_TThird_ShouldImplicitlyConvertFromThird()
    {
        FluentType<int, string, double> value = 42.0;
        Assert.True(value.IsThird);
        Assert.Equal(42.0, value.Third);
    }

    [Fact]
    public void FluentType_TFirst_TSecond_TThird_ShouldThrowInvalidCastExceptionForFirst()
    {
        FluentType<int, string, double> value = "test";
        Assert.Throws<InvalidCastException>(() => (int)value);
    }

    [Fact]
    public void FluentType_TFirst_TSecond_TThird_ShouldThrowInvalidCastExceptionForSecond()
    {
        FluentType<int, string, double> value = 42;
        Assert.Throws<InvalidCastException>(() => (string)value);
    }

    [Fact]
    public void FluentType_TFirst_TSecond_TThird_ShouldThrowInvalidCastExceptionForThird()
    {
        FluentType<int, string, double> value = 42;
        Assert.Throws<InvalidCastException>(() => (double)value);
    }

    [Fact]
    public void FluentType_TFirst_TSecond_TThird_ShouldSwitchCaseCorrectly()
    {
        var value = new FluentType<int, string, double>(42);
        var result = value.SwitchCase(first => first.ToString(), second => second, third => third.ToString(), () => "default");
        Assert.Equal("42", result);
    }

    [Fact]
    public void FluentType_TFirst_TSecond_TThird_ShouldSwitchCaseOrDefaultCorrectly()
    {
        var value = new FluentType<int, string, double>("test");
        var result = value.SwitchCaseOrDefault(first => first.ToString(), second => second, third => third.ToString(), "default");
        Assert.Equal("test", result);
    }

    [Fact]
    public void FluentType_TFirst_TSecond_TThird_ShouldSwitchCaseWithOnMismatch()
    {
        var value = new FluentType<int, string, double>(42.0);
        var exception = new Exception("Mismatch");
        var result = Assert.Throws<Exception>(() => value.SwitchCase(first => first.ToString(), second => second, onMismatch: () => exception));
        Assert.Equal("Mismatch", result.Message);
    }

    [Fact]
    public void FluentType_TFirst_TSecond_TThird_ShouldWithFirstCorrectly()
    {
        var value = new FluentType<int, string, double>(42);
        var newValue = value.WithFirst(first => first + 1);
        Assert.Equal(43, newValue.First);
    }

    [Fact]
    public void FluentType_TFirst_TSecond_TThird_ShouldWithSecondCorrectly()
    {
        var value = new FluentType<int, string, double>("test");
        var newValue = value.WithSecond(second => second + "1");
        Assert.Equal("test1", newValue.Second);
    }

    [Fact]
    public void FluentType_TFirst_TSecond_TThird_ShouldWithThirdCorrectly()
    {
        var value = new FluentType<int, string, double>(42.0);
        var newValue = value.WithThird(third => third + 1.0);
        Assert.Equal(43.0, newValue.Third);
    }

    [Fact]
    public void FluentType_TFirst_TSecond_TThird_ShouldToStringCorrectly()
    {
        var value = new FluentType<int, string, double>(42);
        Assert.Equal("42", value.ToString());
    }

    [Fact]
    public void FluentType_TFirst_TSecond_TThird_ShouldGetHashCodeCorrectly()
    {
        var value = new FluentType<int, string, double>(42);
        var hashCode = value.GetHashCode();
        Assert.NotEqual(0, hashCode);
    }
}
