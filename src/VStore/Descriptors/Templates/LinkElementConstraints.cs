using System;

namespace NuClear.VStore.Descriptors.Templates
{
    public class LinkElementConstraints : ILinkElementConstraints, ITextElementConstraints, IEquatable<LinkElementConstraints>
    {
        public bool ValidLink => true;
        public int? MaxSymbols { get; set; }
        public bool WithoutControlChars => true;
        public bool WithoutNonBreakingSpace => true;

        public bool Equals(LinkElementConstraints other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return ValidLink == other.ValidLink &&
                   MaxSymbols == other.MaxSymbols &&
                   WithoutControlChars == other.WithoutControlChars &&
                   WithoutNonBreakingSpace == other.WithoutNonBreakingSpace;
        }

        public override bool Equals(object obj) => Equals(obj as LinkElementConstraints);

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = MaxSymbols.GetHashCode();
                hashCode = (hashCode * 397) ^ ValidLink.GetHashCode();
                hashCode = (hashCode * 397) ^ WithoutControlChars.GetHashCode();
                hashCode = (hashCode * 397) ^ WithoutNonBreakingSpace.GetHashCode();
                return hashCode;
            }
        }
    }
}
