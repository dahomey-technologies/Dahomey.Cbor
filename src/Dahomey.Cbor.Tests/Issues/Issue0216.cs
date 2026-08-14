using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Serialization.Conventions;
using Dahomey.Cbor.Serialization.Converters;
using Dahomey.Cbor.Serialization.Converters.Providers;
using System;
using Xunit;

namespace Dahomey.Cbor.Tests.Issues
{
    /// <summary>
    /// Everything this library builds by reflection is built with <c>Activator.CreateInstance</c>, which
    /// wraps whatever the constructor threw in a <see cref="System.Reflection.TargetInvocationException"/>.
    /// A caller's <c>catch (CborException)</c> — the one the rest of the API trains them into — misses
    /// every one of these.
    /// </summary>
    /// <remarks>
    /// The point is the envelope, not the message: each of these threw the right <c>CborException</c> all
    /// along, and it arrived as something else. The cases below are the four places a constructor can run
    /// code that fails — the converter a provider builds, a member's own <c>[CborConverter]</c>, a type's
    /// object mapping, and a <c>[CborNamingConvention]</c> — which is every reflective construction site
    /// where the thing being constructed can refuse.
    /// </remarks>
    public class Issue0216
    {
        /// <summary>
        /// A converter built through a provider. <c>CborConverterProviderBase</c> is public and
        /// <c>CreateConverter</c> is protected, so this is the supported extension point rather than a
        /// contrivance; in-library converters reach it the same way.
        /// </summary>
        [Fact]
        public void AConverterRefusingItsTypeThrowsCborException()
        {
            CborOptions options = new CborOptions();
            options.Registry.ConverterRegistry.RegisterConverterProvider(new RefusingConverterProvider());

            CborException exception = Assert.Throws<CborException>(
                () => Helper.Write(new RefusedByItsConverter(), options));

            Assert.Equal("this type is refused by its converter", exception.Message);
        }

        /// <summary>A member's own <c>[CborConverter]</c>, which is a type the caller named.</summary>
        [Fact]
        public void AMemberConverterRefusingItsTypeThrowsCborException()
        {
            CborException exception = Assert.Throws<CborException>(
                () => Helper.Write(new HolderWithARefusingMemberConverter()));

            Assert.Equal("this member is refused by its converter", exception.Message);
        }

        /// <summary>
        /// An object mapping that refuses itself while being built. This is the one an ordinary user
        /// reaches without writing a converter at all: two members under one CBOR name, which the mapping
        /// validation refuses.
        /// </summary>
        [Fact]
        public void AMappingRefusingItselfThrowsCborException()
        {
            CborException exception = Assert.Throws<CborException>(
                () => Helper.Write(new TwoMembersUnderOneName()));

            Assert.Contains("TwoMembersUnderOneName", exception.Message);
        }

        /// <summary>A <c>[CborNamingConvention]</c> whose constructor refuses.</summary>
        [Fact]
        public void ANamingConventionRefusingItselfThrowsCborException()
        {
            CborException exception = Assert.Throws<CborException>(
                () => Helper.Write(new HolderWithARefusingNamingConvention()));

            Assert.Equal("this naming convention is refused", exception.Message);
        }

        /// <summary>
        /// The unwrap must not swallow anything: an exception that is not a <c>CborException</c> reaches
        /// the caller as itself rather than as a <c>TargetInvocationException</c> around it, and keeps the
        /// stack frame it was thrown from.
        /// </summary>
        [Fact]
        public void AnExceptionOfAnotherTypeIsNotConvertedOrSwallowed()
        {
            CborOptions options = new CborOptions();
            options.Registry.ConverterRegistry.RegisterConverterProvider(new ThrowingConverterProvider());

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => Helper.Write(new RefusedByAnotherException(), options));

            Assert.Equal("not a CborException", exception.Message);
            Assert.Contains(nameof(ThrowingConverter<int>), exception.StackTrace);
        }

        public class RefusedByItsConverter
        {
        }

        public class RefusedByAnotherException
        {
        }

        public class RefusingConverter<T> : CborConverterBase<T>
        {
            public RefusingConverter()
            {
                throw new CborException("this type is refused by its converter");
            }

            public override T Read(ref CborReader reader) => throw new NotSupportedException();

            public override void Write(ref CborWriter writer, T value) => throw new NotSupportedException();
        }

        public class ThrowingConverter<T> : CborConverterBase<T>
        {
            public ThrowingConverter()
            {
                throw new InvalidOperationException("not a CborException");
            }

            public override T Read(ref CborReader reader) => throw new NotSupportedException();

            public override void Write(ref CborWriter writer, T value) => throw new NotSupportedException();
        }

        public class RefusingConverterProvider : CborConverterProviderBase
        {
            public override ICborConverter? GetConverter(Type type, CborOptions options)
            {
                return type == typeof(RefusedByItsConverter)
                    ? CreateGenericConverter(options, typeof(RefusingConverter<>), type)
                    : null;
            }
        }

        public class ThrowingConverterProvider : CborConverterProviderBase
        {
            public override ICborConverter? GetConverter(Type type, CborOptions options)
            {
                return type == typeof(RefusedByAnotherException)
                    ? CreateGenericConverter(options, typeof(ThrowingConverter<>), type)
                    : null;
            }
        }

        public class RefusingMemberConverter : CborConverterBase<int>
        {
            public RefusingMemberConverter()
            {
                throw new CborException("this member is refused by its converter");
            }

            public override int Read(ref CborReader reader) => throw new NotSupportedException();

            public override void Write(ref CborWriter writer, int value) => throw new NotSupportedException();
        }

        public class HolderWithARefusingMemberConverter
        {
            [CborConverter(typeof(RefusingMemberConverter))]
            public int Value { get; set; }
        }

        public class TwoMembersUnderOneName
        {
            [CborProperty("x")]
            public int First { get; set; }

            [CborProperty("x")]
            public int Second { get; set; }
        }

        public class RefusingNamingConvention : INamingConvention
        {
            public RefusingNamingConvention()
            {
                throw new CborException("this naming convention is refused");
            }

            public string GetPropertyName(System.Reflection.MemberInfo memberInfo) => memberInfo.Name;
        }

        [CborNamingConvention(typeof(RefusingNamingConvention))]
        public class HolderWithARefusingNamingConvention
        {
            public int Value { get; set; }
        }
    }
}
