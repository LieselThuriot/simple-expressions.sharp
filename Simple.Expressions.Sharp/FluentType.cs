namespace Simple.Expressions.Sharp;

internal enum FluentTypes
{
    Undefined,
    First,
    Second,
    Third
}

public readonly struct FluentType<TFirst, TSecond> : IEquatable<FluentType<TFirst, TSecond>>
{
    private readonly FluentTypes _currentType;

    public bool IsFirst => _currentType is FluentTypes.First;

    public bool IsSecond => _currentType is FluentTypes.Second;

    public TFirst First { get; }

    public TSecond Second { get; }

    public Type CurrentType => _currentType switch
    {
        FluentTypes.First => typeof(TFirst),
        FluentTypes.Second => typeof(TSecond),
        _ => null
    };

    public FluentType(TFirst value)
    {
        _currentType = FluentTypes.First;
        First = value;
        Second = default;
    }

    public FluentType(TSecond value)
    {
        _currentType = FluentTypes.Second;
        First = default;
        Second = value;
    }

    public override int GetHashCode() => HashCodeCalculator.GetHashCode(
    [
        2,
        _currentType,
        First,
        Second,
        typeof(TFirst),
        typeof(TSecond)
    ]);

    public bool Equals(FluentType<TFirst, TSecond> other) => _currentType switch
    {
        FluentTypes.Undefined => other._currentType is FluentTypes.Undefined,
        FluentTypes.First => other.IsFirst && EqualityComparer<TFirst>.Default.Equals(First, other.First),
        FluentTypes.Second => other.IsSecond && EqualityComparer<TSecond>.Default.Equals(Second, other.Second),
        _ => false,
    };

    public static bool operator ==(FluentType<TFirst, TSecond> obj1, FluentType<TFirst, TSecond> obj2) => EqualityComparer<FluentType<TFirst, TSecond>>.Default.Equals(obj1, obj2);

    public static bool operator !=(FluentType<TFirst, TSecond> obj1, FluentType<TFirst, TSecond> obj2) => !EqualityComparer<FluentType<TFirst, TSecond>>.Default.Equals(obj1, obj2);

    public override bool Equals(object obj) => obj is FluentType<TFirst, TSecond> other && Equals(other);

    public override string ToString() => _currentType switch
    {
        FluentTypes.First => $"{First}",
        FluentTypes.Second => $"{Second}",
        _ => null
    };

    public static implicit operator FluentType<TFirst, TSecond>(TFirst value) => new(value);
    public static implicit operator FluentType<TFirst, TSecond>(TSecond value) => new(value);

    public static implicit operator TFirst(FluentType<TFirst, TSecond> of) => of.IsFirst ? of.First : throw new InvalidCastException();
    public static implicit operator TSecond(FluentType<TFirst, TSecond> of) => of.IsSecond ? of.Second : throw new InvalidCastException();
}

public readonly struct FluentType<TFirst, TSecond, TThird> : IEquatable<FluentType<TFirst, TSecond, TThird>>
{
    private readonly FluentTypes _currentType;

    public bool IsFirst => _currentType is FluentTypes.First;

    public bool IsSecond => _currentType is FluentTypes.Second;

    public bool IsThird => _currentType is FluentTypes.Third;

    public TFirst First { get; }

    public TSecond Second { get; }

    public TThird Third { get; }

    public Type CurrentType => _currentType switch
    {
        FluentTypes.First => typeof(TFirst),
        FluentTypes.Second => typeof(TSecond),
        FluentTypes.Third => typeof(TThird),
        _ => null
    };

    public FluentType(TFirst value)
    {
        _currentType = FluentTypes.First;
        First = value;
        Second = default;
        Third = default;
    }

    public FluentType(TSecond value)
    {
        _currentType = FluentTypes.Second;
        First = default;
        Second = value;
        Third = default;
    }

    public FluentType(TThird value)
    {
        _currentType = FluentTypes.Third;
        First = default;
        Second = default;
        Third = value;
    }

    public override int GetHashCode() => HashCodeCalculator.GetHashCode(
    [
        3,
        _currentType,
        First,
        Second,
        Third,
        typeof(TFirst),
        typeof(TSecond),
        typeof(TThird)
    ]);

    public bool Equals(FluentType<TFirst, TSecond, TThird> other) => _currentType switch
    {
        FluentTypes.Undefined => other._currentType is FluentTypes.Undefined,
        FluentTypes.First => other.IsFirst && EqualityComparer<TFirst>.Default.Equals(First, other.First),
        FluentTypes.Second => other.IsSecond && EqualityComparer<TSecond>.Default.Equals(Second, other.Second),
        FluentTypes.Third => other.IsThird && EqualityComparer<TThird>.Default.Equals(Third, other.Third),
        _ => false,
    };

    public static bool operator ==(FluentType<TFirst, TSecond, TThird> obj1, FluentType<TFirst, TSecond, TThird> obj2) => EqualityComparer<FluentType<TFirst, TSecond, TThird>>.Default.Equals(obj1, obj2);

    public static bool operator !=(FluentType<TFirst, TSecond, TThird> obj1, FluentType<TFirst, TSecond, TThird> obj2) => !EqualityComparer<FluentType<TFirst, TSecond, TThird>>.Default.Equals(obj1, obj2);

    public override bool Equals(object obj) => obj is FluentType<TFirst, TSecond, TThird> other && Equals(other);

    public override string ToString() => _currentType switch
    {
        FluentTypes.First => $"{First}",
        FluentTypes.Second => $"{Second}",
        FluentTypes.Third => $"{Third}",
        _ => null
    };

    public static implicit operator FluentType<TFirst, TSecond, TThird>(TFirst value) => new(value);
    public static implicit operator FluentType<TFirst, TSecond, TThird>(TSecond value) => new(value);
    public static implicit operator FluentType<TFirst, TSecond, TThird>(TThird value) => new(value);

    public static implicit operator TFirst(FluentType<TFirst, TSecond, TThird> of) => of.IsFirst ? of.First : throw new InvalidCastException();
    public static implicit operator TSecond(FluentType<TFirst, TSecond, TThird> of) => of.IsSecond ? of.Second : throw new InvalidCastException();
    public static implicit operator TThird(FluentType<TFirst, TSecond, TThird> of) => of.IsThird ? of.Third : throw new InvalidCastException();
}

internal static class HashCodeCalculator
{
    public static int GetHashCode(IEnumerable<object> hashFieldValues) => hashFieldValues.Aggregate(-2128831035, (hashCode, value) => (hashCode ^ (value?.GetHashCode() ?? 0)) * 16777619);
}