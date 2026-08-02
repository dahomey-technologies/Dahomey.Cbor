using Xunit;

namespace Dahomey.Cbor.Tests.Cddl
{
    /// <summary>
    /// A <see cref="FactAttribute"/> that skips when the cddl gem is absent. Setting CDDL_REQUIRED=1
    /// removes the skip, so the test runs and fails on the missing binary.
    /// </summary>
    public sealed class CddlFactAttribute : FactAttribute
    {
        public CddlFactAttribute()
        {
            if (!CddlTool.Available)
            {
                Skip = "the cddl gem is not installed; set CDDL_REQUIRED=1 to make this a failure";
            }
        }
    }

    /// <summary>The <see cref="TheoryAttribute"/> equivalent of <see cref="CddlFactAttribute"/>.</summary>
    public sealed class CddlTheoryAttribute : TheoryAttribute
    {
        public CddlTheoryAttribute()
        {
            if (!CddlTool.Available)
            {
                Skip = "the cddl gem is not installed; set CDDL_REQUIRED=1 to make this a failure";
            }
        }
    }
}
