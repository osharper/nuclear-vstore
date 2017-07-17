using System;

namespace NuClear.VStore.Descriptors.Templates
{
    public class LinkElementConstraints : PlainTextElementConstraints, ILinkElementConstraints, IEquatable<LinkElementConstraints>
    {
        public bool ValidLink => true;

        public override bool Equals(object obj)
        {
            var other = obj as LinkElementConstraints;
            return Equals(other);
        }

        public bool Equals(LinkElementConstraints other) => base.Equals(other) && ValidLink == other.ValidLink;

        public override int GetHashCode() => unchecked((base.GetHashCode() * 397) ^ ValidLink.GetHashCode());
    }
}
