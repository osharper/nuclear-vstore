using System;

namespace NuClear.VStore.Descriptors.Templates
{
    public class PhoneElementConstraints : IElementConstraints, IEquatable<PhoneElementConstraints>
    {
        public bool Equals(PhoneElementConstraints other)
        {
            return !ReferenceEquals(null, other);
        }

        public override bool Equals(object obj)
        {
            var other = obj as PhoneElementConstraints;
            return Equals(other);
        }

        public override int GetHashCode()
        {
            return 1;
        }
    }
}
