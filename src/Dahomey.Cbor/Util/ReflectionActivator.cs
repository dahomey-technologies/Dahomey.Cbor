using System;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace Dahomey.Cbor.Util
{
    /// <summary>
    /// <see cref="Activator.CreateInstance(Type)"/>, with whatever the constructor threw reaching the
    /// caller as itself.
    /// </summary>
    /// <remarks>
    /// Activator wraps a constructor's exception in a <see cref="TargetInvocationException"/>, so every
    /// type this library builds by reflection — a converter, a member's converter, an object mapping, a
    /// naming convention — reported its refusal in an envelope a caller's <c>catch (CborException)</c>
    /// does not match. The exception was always the right one; only the envelope was wrong.
    /// <para>
    /// <see cref="ExceptionDispatchInfo"/> rather than <c>throw exception.InnerException</c>, which would
    /// reset the stack trace to this line and lose the frame the refusal actually came from.
    /// </para>
    /// <para>
    /// Each site unwraps one layer, which is what the nesting needs: a construction that itself
    /// constructs something reflectively wraps twice, as a member's <c>[CborConverter]</c> and a
    /// <c>[CborNamingConvention]</c> both did.
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

        public static object? CreateInstance(Type type, params object?[] arguments)
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
    }
}
