#nullable enable

using System.Collections;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace Simple.Expressions;

/// <summary>
/// Reads named members off an arbitrary model object. Every strategy decision — dictionary
/// versus property bag, and the getter for each property — is made once per <see cref="Type"/>
/// and cached, so evaluation costs a dictionary lookup and a delegate call per segment.
/// </summary>
/// <remarks>
/// Upstream distinguishes an absent member from a member holding <c>null</c> via
/// <c>hasOwnProperty</c>. That distinction is load-bearing here too — it is what separates
/// <see cref="ExpressionValueKind.Missing"/> from <see cref="ExpressionValueKind.Null"/> — so
/// every accessor returns presence as its result and the value through an out parameter.
/// </remarks>
internal static class ModelAccessor
{
    private static readonly ConcurrentDictionary<Type, MemberAccessor> Accessors = new();
    private static readonly Func<Type, MemberAccessor> AccessorFactory = Build;

    public static bool TryGetMember(object instance, string name, out object? value)
        => Accessors.GetOrAdd(instance.GetType(), AccessorFactory).TryGet(instance, name, out value);

    public static IEnumerable<KeyValuePair<string, object?>> GetEntries(object instance)
        => Accessors.GetOrAdd(instance.GetType(), AccessorFactory).GetEntries(instance);

    /// <summary>
    /// Whether a value is keyed rather than sequential. Dictionaries are objects even though
    /// they are also enumerable, so this decides the object/array split in
    /// <see cref="ExpressionModel"/>.
    /// </summary>
    public static bool IsDictionary(object instance)
        => Accessors.GetOrAdd(instance.GetType(), AccessorFactory).IsDictionary;

    private static MemberAccessor Build(Type type)
    {
        // A dictionary's keys are its members, so it is checked before the property bag: a
        // Dictionary<string, T> also exposes Count and Keys as properties, and those are not
        // members of the model the caller wrote.
        foreach (var candidate in EnumerateInterfaces(type))
        {
            if (!candidate.IsGenericType || candidate.GetGenericTypeDefinition() != typeof(IDictionary<,>))
                continue;

            var arguments = candidate.GetGenericArguments();
            if (arguments[0] != typeof(string))
                continue;

            return (MemberAccessor)Activator.CreateInstance(typeof(GenericDictionaryAccessor<>).MakeGenericType(arguments[1]))!;
        }

        foreach (var candidate in EnumerateInterfaces(type))
        {
            if (!candidate.IsGenericType || candidate.GetGenericTypeDefinition() != typeof(IReadOnlyDictionary<,>))
                continue;

            var arguments = candidate.GetGenericArguments();
            if (arguments[0] != typeof(string))
                continue;

            return (MemberAccessor)Activator.CreateInstance(typeof(GenericReadOnlyDictionaryAccessor<>).MakeGenericType(arguments[1]))!;
        }

        if (typeof(IDictionary).IsAssignableFrom(type))
            return new NonGenericDictionaryAccessor();

        return new PropertyAccessor(type);
    }

    private static IEnumerable<Type> EnumerateInterfaces(Type type)
    {
        if (type.IsInterface)
            yield return type;

        foreach (var candidate in type.GetInterfaces())
            yield return candidate;
    }

    private abstract class MemberAccessor
    {
        public abstract bool IsDictionary { get; }
        public abstract bool TryGet(object instance, string name, out object? value);
        public abstract IEnumerable<KeyValuePair<string, object?>> GetEntries(object instance);
    }

    private sealed class GenericDictionaryAccessor<TValue> : MemberAccessor
    {
        public override bool IsDictionary => true;

        public override bool TryGet(object instance, string name, out object? value)
        {
            if (((IDictionary<string, TValue>)instance).TryGetValue(name, out var typed))
            {
                value = typed;
                return true;
            }

            value = null;
            return false;
        }

        public override IEnumerable<KeyValuePair<string, object?>> GetEntries(object instance)
        {
            foreach (var entry in (IDictionary<string, TValue>)instance)
                yield return new KeyValuePair<string, object?>(entry.Key, entry.Value);
        }
    }

    private sealed class GenericReadOnlyDictionaryAccessor<TValue> : MemberAccessor
    {
        public override bool IsDictionary => true;

        public override bool TryGet(object instance, string name, out object? value)
        {
            if (((IReadOnlyDictionary<string, TValue>)instance).TryGetValue(name, out var typed))
            {
                value = typed;
                return true;
            }

            value = null;
            return false;
        }

        public override IEnumerable<KeyValuePair<string, object?>> GetEntries(object instance)
        {
            foreach (var entry in (IReadOnlyDictionary<string, TValue>)instance)
                yield return new KeyValuePair<string, object?>(entry.Key, entry.Value);
        }
    }

    private sealed class NonGenericDictionaryAccessor : MemberAccessor
    {
        public override bool IsDictionary => true;

        public override bool TryGet(object instance, string name, out object? value)
        {
            var dictionary = (IDictionary)instance;
            if (dictionary.Contains(name))
            {
                value = dictionary[name];
                return true;
            }

            value = null;
            return false;
        }

        public override IEnumerable<KeyValuePair<string, object?>> GetEntries(object instance)
        {
            var enumerator = ((IDictionary)instance).GetEnumerator();
            try
            {
                while (enumerator.MoveNext())
                {
                    if (enumerator.Key is string key)
                        yield return new KeyValuePair<string, object?>(key, enumerator.Value);
                }
            }
            finally
            {
                (enumerator as IDisposable)?.Dispose();
            }
        }
    }

    private sealed class PropertyAccessor : MemberAccessor
    {
        private readonly Dictionary<string, Func<object, object?>> _getters;

        public override bool IsDictionary => false;

        public PropertyAccessor(Type type)
        {
            // Ordinal and case-sensitive, matching the CLR property name exactly. This is what
            // the serializer produced before — no naming policy was ever configured — and it is
            // what keeps len(#value) working on `new { length = 4 }` but not `new { Length = 4 }`.
            _getters = new Dictionary<string, Func<object, object?>>(StringComparer.Ordinal);

            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                // Indexers are not addressable by name, and a property without a public getter
                // was never part of the serialized shape.
                if (property.GetIndexParameters().Length != 0 || property.GetGetMethod() is null)
                    continue;

                // A shadowed property (`new`) appears twice; the most derived one comes first.
                if (!_getters.ContainsKey(property.Name))
                    _getters.Add(property.Name, CreateGetter(property));
            }
        }

        public override bool TryGet(object instance, string name, out object? value)
        {
            if (_getters.TryGetValue(name, out var getter))
            {
                value = getter(instance);
                return true;
            }

            value = null;
            return false;
        }

        public override IEnumerable<KeyValuePair<string, object?>> GetEntries(object instance)
        {
            foreach (var getter in _getters)
                yield return new KeyValuePair<string, object?>(getter.Key, getter.Value(instance));
        }

        private static Func<object, object?> CreateGetter(PropertyInfo property)
        {
            try
            {
                return EmitGetter(property);
            }
            catch (Exception)
            {
                // Platforms without runtime code generation fall back to plain reflection.
                return instance => property.GetValue(instance);
            }
        }

        /// <summary>
        /// Compiles <c>instance =&gt; (object)((TDeclaring)instance).Property</c>.
        /// </summary>
        /// <remarks>
        /// The most common model shape by far is an anonymous type, which is <c>internal</c> to
        /// the assembly that wrote it. Expression tree compilation gets at those because it
        /// emits with restricted-skip-visibility, which full-trust hosts allow.
        /// </remarks>
        private static Func<object, object?> EmitGetter(PropertyInfo property)
        {
            var instance = Expression.Parameter(typeof(object), "instance");
            var body = Expression.Convert(
                Expression.Property(Expression.Convert(instance, property.DeclaringType!), property),
                typeof(object));

            return Expression.Lambda<Func<object, object?>>(body, instance).Compile();
        }
    }
}
