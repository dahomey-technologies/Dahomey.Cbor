using System;

namespace Dahomey.Cbor.Serialization.Converters
{
    /// <summary>
    /// Messages for a type whose mapping puts two members under one key, so that the two places that
    /// can catch it word it the same.
    /// </summary>
    /// <remarks>
    /// Validating the whole mapping catches this, and the read lookup catches what arrives after that
    /// validation has already run. They report the same failure and should say the same thing about
    /// it, differing only by <see cref="AddedAfterValidation"/> - which is what lets a caller, or a
    /// test, tell which one answered. Keeping the wording in one place is what stops a reword of one
    /// from silently parting company with the other.
    /// </remarks>
    internal static class MemberMappingErrors
    {
        /// <summary>
        /// Appended when the collision reached the read lookup rather than the validation, meaning a
        /// member was mapped after something had already initialized the mapping.
        /// </summary>
        /// <remarks>
        /// Worth saying because the two have different fixes: one is two members declared under one
        /// name, the other is a mapping API call - a <c>SetMemberName</c> or <c>SetMemberIndex</c>
        /// onto a key already taken - made after the mapping had been used.
        /// </remarks>
        public const string AddedAfterValidation =
            ". The collision was added to the mapping after it was validated.";

        public static string DuplicateMemberName(Type objectType, string? memberName)
        {
            return $"class/struct {objectType.Name} maps several fields/properties to the member name '{memberName}'";
        }

        public static string DuplicateMemberIndex(Type objectType)
        {
            return $"class/struct {objectType.Name} holds duplicated MemberIndex fields/properties";
        }
    }
}
