using System;

namespace NuClear.VStore.Descriptors.Templates
{
    public class DateElementConstraints : IElementConstraints, IEquatable<DateElementConstraints>
    {
        public bool ValidDateRange => true;

        public bool Equals(DateElementConstraints other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return ValidDateRange == other.ValidDateRange;
        }

        public override bool Equals(object obj)
        {
            var other = obj as DateElementConstraints;
            return Equals(other);
        }

        public override int GetHashCode()
        {
            return ValidDateRange.GetHashCode();
        }
    }
}
