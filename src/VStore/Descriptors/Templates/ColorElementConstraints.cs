using System;

namespace NuClear.VStore.Descriptors.Templates
{
    public sealed class ColorElementConstraints : IElementConstraints, IEquatable<ColorElementConstraints>
    {
        public bool ValidColor => true;

        public bool Equals(ColorElementConstraints other) => other != null && ValidColor == other.ValidColor;

        public override bool Equals(object obj) => Equals(obj as ColorElementConstraints);

        public override int GetHashCode() => ValidColor.GetHashCode();
    }
}