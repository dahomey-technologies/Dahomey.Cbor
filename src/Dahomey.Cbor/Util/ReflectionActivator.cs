using System;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace Dahomey.Cbor.Util
{
    /// <summary>
    /// Late-bound construction and invocation, with whatever the callee threw reaching the caller as
    /// itself.
    /// </summary>
    /// <remarks>
    /// <see cref="Activator.CreateInstance(Type)"/> and <see cref="Delegate.DynamicInvoke"/> both wrap
    /// the callee's exception in a <see cref="TargetInvocationException"/>, so a type this library builds
    /// by reflection — a converter, a member's converter, an object mapping, a naming convention — and a
    /// creator it invokes reported its refusal in an envelope a caller's <c>catch (CborException)</c>
    /// does not match. The exception was always the right one; only the envelope was wrong.
    /// <para>
    /// <see cref="ExceptionDispatchInfo"/> rather than <c>throw exception.InnerException</c>, which would
    /// reset the stack trace to this line and lose the frame the refusal came from.
    /// </para>
    /// <para>
    /// Each call site unwraps one layer, which is what the nesting needs: a construction that itself
    /// constructs something reflectively wraps twice, as a member's <c>[CborConverter]</c> and a
    /// <c>[CborNamingConvention]</c> both do. One layer per site composes correctly — the inner site
    /// rethrows bare, the outer callee re-wraps once, the outer site unwraps once — and it cannot strip
    /// an envelope a caller deliberately put on.
    /// </para>
    /// <para>
    /// This covers the sites that go through <see cref="Activator"/> or <see cref="Delegate.DynamicInvoke"/>.
    /// It is not a claim about every route into user code: a converter's <c>Read</c> and <c>Write</c> are
    /// called directly and were never wrapped, and an <c>Expression</c>-compiled member accessor invokes
    /// its target directly too.
    /// </para>
    /// </remarks>
    internal static class ReflectionActivator
    {
        public static object? CreateInstance(Type type)
        {
            try
            {
                return Activator.CreateInstance(type);
            }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                throw;
            }
        }

        /// <remarks>
        /// Deliberately not <c>params</c>. Under <c>params</c> a caller passing a pre-built
        /// <c>object?[]</c> means "these arguments" while a caller passing <c>null</c> means "no
        /// arguments", and neither reads differently at the call site from passing one argument that
        /// happens to be an array or a null. Every caller here spells the array out.
        /// </remarks>
        public static object? CreateInstance(Type type, object?[] arguments)
        {
            try
            {
                return Activator.CreateInstance(type, arguments);
            }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                throw;
            }
        }

        /// <summary>
        /// Invokes a delegate whose signature is not known at compile time — a creator built from a
        /// constructor, a factory method, or one the caller supplied.
        /// </summary>
        /// <remarks>
        /// This one runs while a document is being read rather than while a mapping is built, and the
        /// code it reaches is the caller's own constructor, so it is the site whose envelope a user is
        /// most likely to meet.
        /// </remarks>
        public static object? Invoke(Delegate @delegate, object?[] arguments)
        {
            try
            {
                return @delegate.DynamicInvoke(arguments);
            }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                throw;
            }
        }
    }
}
