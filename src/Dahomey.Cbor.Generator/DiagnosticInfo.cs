using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Linq;

namespace Dahomey.Cbor.Generator
{
    /// <summary>
    /// A source position, detached from the syntax tree that produced it.
    /// </summary>
    /// <remarks>
    /// <see cref="Location"/> holds its <see cref="SyntaxTree"/>, and a syntax tree holds its
    /// <see cref="Compilation"/>. Keeping one in a pipeline model roots the whole compilation for as
    /// long as the model is cached, and makes the model unequal to the next run's by reference.
    /// </remarks>
    internal sealed record LocationInfo(string FilePath, TextSpan TextSpan, LinePositionSpan LineSpan)
    {
        public Location ToLocation()
        {
            return Location.Create(FilePath, TextSpan, LineSpan);
        }

        public static LocationInfo? From(Location? location)
        {
            if (location?.SourceTree is null)
            {
                return null;
            }

            return new LocationInfo(
                location.SourceTree.FilePath, location.SourceSpan, location.GetLineSpan().Span);
        }

        public static LocationInfo? From(ISymbol? symbol)
        {
            return symbol is null ? null : From(symbol.Locations.FirstOrDefault());
        }
    }

    /// <summary>
    /// A diagnostic the generator wants to report, as a value.
    /// </summary>
    /// <remarks>
    /// Reporting happens in the source-output step, which cannot be cached; deciding what to report
    /// happens in the step before it, which can. Descriptors are static singletons, so comparing them
    /// by reference is comparing them by identity.
    /// </remarks>
    internal sealed record DiagnosticInfo(
        DiagnosticDescriptor Descriptor,
        LocationInfo? Location,
        EquatableArray<string> MessageArgs)
    {
        public Diagnostic ToDiagnostic()
        {
            return Diagnostic.Create(Descriptor, Location?.ToLocation(), MessageArgs.ToObjectArray());
        }

        public static DiagnosticInfo Create(
            DiagnosticDescriptor descriptor, Location? location, params string[] messageArgs)
        {
            return new DiagnosticInfo(
                descriptor, LocationInfo.From(location), new EquatableArray<string>(messageArgs));
        }

        public static DiagnosticInfo Create(
            DiagnosticDescriptor descriptor, ISymbol? symbol, params string[] messageArgs)
        {
            return new DiagnosticInfo(
                descriptor, LocationInfo.From(symbol), new EquatableArray<string>(messageArgs));
        }
    }
}
