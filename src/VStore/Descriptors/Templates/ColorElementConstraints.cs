using System;

namespace NuClear.VStore.Descriptors.Templates
{
    public sealed class ColorElementConstraints : IElementConstraints, IEquatable<ColorElementConstraints>
    {
        public bool Equals(ColorElementConstraints other) => other != null;

        public override bool Equals(object obj) => obj is ColorElementConstraints;

        public override int GetHashCode() => 0;
    }
}