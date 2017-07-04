using System;

namespace NuClear.VStore.Descriptors.Templates
{
    public class VideoLinkElementConstraints : ILinkElementConstraints, IEquatable<VideoLinkElementConstraints>
    {
        public bool ValidLink => true;

        public bool Equals(VideoLinkElementConstraints other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return ValidLink == other.ValidLink;
        }

        public override bool Equals(object obj) => Equals(obj as VideoLinkElementConstraints);

        public override int GetHashCode() => ValidLink.GetHashCode();
    }
}
