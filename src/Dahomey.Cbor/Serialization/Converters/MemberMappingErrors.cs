using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization.Conventions;
using Dahomey.Cbor.Serialization.Converters.Mappings;
using System;
using System.Linq;

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

        /// <summary>
        /// Refuses a convention whose discriminator member name is already taken by a member of the type
        /// it would be written beside.
        /// </summary>
        /// <remarks>
        /// <see cref="ObjectMapping{T}"/> validates the member names once, with the discriminator under
        /// the name its convention had then. A convention that replaces it afterwards - through
        /// <c>ClearConventions</c> and a new registration - can bring a name the validation never saw,
        /// and nothing re-runs it. So whoever picks up a changed convention asks here, and gets the
        /// answer a fresh <c>CborOptions</c> would have given when the mapping was built. Only a type
        /// whose mapping carries a discriminator member is concerned, and only in the format that keys
        /// its members by name.
        /// </remarks>
        public static void ThrowIfDiscriminatorNameCollides(IObjectMapping objectMapping, IDiscriminatorConvention? convention)
        {
            if (convention == null || objectMapping.ObjectFormat != CborObjectFormat.StringKeyMap)
            {
                return;
            }

            string discriminatorName = System.Text.Encoding.UTF8.GetString(convention.MemberName.ToArray());
            bool hasDiscriminator = false;
            bool collides = false;

            foreach (IMemberMapping memberMapping in objectMapping.MemberMappings)
            {
                if (memberMapping is IDiscriminatorMapping)
                {
                    hasDiscriminator = true;
                }
                else if (string.Equals(memberMapping.MemberName, discriminatorName, StringComparison.Ordinal))
                {
                    collides = true;
                }
            }

            if (hasDiscriminator && collides)
            {
                throw new CborException(DuplicateMemberName(objectMapping.ObjectType, discriminatorName));
            }
        }

        public static string DuplicateMemberIndex(Type objectType)
        {
            return $"class/struct {objectType.Name} holds duplicated MemberIndex fields/properties";
        }

        /// <summary>
        /// A document carrying a discriminator for a subtype the registry does not know.
        /// </summary>
        /// <remarks>
        /// Names the value as well as the remedy, because the remedy is per subtype: a caller reading a
        /// hierarchy has to register each concrete type, and the value is what says which one this
        /// document needed. Kept here so the message stays one string if a second caller ever raises it.
        /// </remarks>
        public static string UnregisteredDiscriminatedSubtype(Type objectType, object discriminator)
        {
            return $"no type is registered for the discriminator {discriminator} carried by this document, "
                + $"so it cannot be read as {objectType.Name}; call "
                + $"CborOptions.Registry.DiscriminatorConventionRegistry.RegisterType<T>() for the concrete "
                + "type that discriminator denotes";
        }
    }
}
