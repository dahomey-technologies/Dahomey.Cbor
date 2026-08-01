using Dahomey.Cbor.Serialization.Converters;
using System;
using System.Collections.Concurrent;

namespace Dahomey.Cbor.Serialization
{
    /// <summary>
    /// Base class for a source-generated set of CBOR mappings — the reflection-free, Native-AOT-safe
    /// alternative to letting the registry discover types at run time.
    /// </summary>
    /// <remarks>
    /// Derive a <c>partial</c> class and declare the types to support; the generator emits
    /// <see cref="Configure"/> along with a strongly-typed accessor per type:
    /// <code>
    /// [CborSerializable(typeof(Person))]
    /// [CborSerializable(typeof(List&lt;Person&gt;))]
    /// public partial class MyContext : CborSerializerContext { }
    ///
    /// MyContext context = CborSerializerContext.Default&lt;MyContext&gt;();
    /// Cbor.Serialize(person, buffer, context.Options);
    /// </code>
    /// <para>
    /// Nothing here is AOT-hostile: the generated <see cref="Configure"/> registers explicitly
    /// constructed converters and delegate-based member mappings, so no code path reaches
    /// <c>MakeGenericType</c>, <c>Activator.CreateInstance</c>, <c>Expression.Compile</c> or member
    /// enumeration.
    /// </para>
    /// <para>
    /// A context does not disable the reflection fallback. A type that was never declared still
    /// resolves through the normal provider chain, which works on CoreCLR and fails under AOT — which
    /// is why the generator reports undeclared types as build errors.
    /// </para>
    /// </remarks>
    public abstract class CborSerializerContext
    {
        private static readonly ConcurrentDictionary<Type, CborSerializerContext> _defaults = new();

        /// <summary>Options carrying the registrations made by <see cref="Configure"/>.</summary>
        public CborOptions Options { get; }

        protected CborSerializerContext()
            : this(null)
        {
        }

        /// <param name="options">
        /// Options to register into. Null creates a fresh <see cref="CborOptions"/>. Supply your own to
        /// combine generated registrations with additional settings or custom converters.
        /// </param>
        protected CborSerializerContext(CborOptions? options)
        {
            Options = options ?? new CborOptions();
            Configure(Options);
        }

        /// <summary>
        /// Shared instance of <typeparamref name="TContext"/>. Contexts are immutable once
        /// constructed, so one instance can be reused; building one is not free, since it constructs
        /// every converter up front.
        /// </summary>
        public static TContext Default<TContext>()
            where TContext : CborSerializerContext, new()
        {
            // `new TContext()` under a `new()` constraint is resolved statically, so this stays
            // AOT-safe - unlike Activator.CreateInstance(Type).
            return (TContext)_defaults.GetOrAdd(typeof(TContext), _ => new TContext());
        }

        /// <summary>
        /// Registers object mappings and converters. Implemented by the generated partial class.
        /// </summary>
        /// <remarks>
        /// Registration order matters: <see cref="Converters.ObjectConverter{T}"/> resolves a converter
        /// for each of its members as it is constructed, so a member's converter must already be
        /// registered. The generator emits registrations in reverse topological order for that reason.
        /// </remarks>
        protected abstract void Configure(CborOptions options);

        /// <summary>
        /// Looks up the converter for <typeparamref name="T"/>. Generic, so no
        /// <c>MakeGenericType</c> is involved; used by the generated typed accessors.
        /// </summary>
        protected ICborConverter<T> GetConverter<T>()
        {
            return Options.Registry.ConverterRegistry.Lookup<T>();
        }
    }
}
