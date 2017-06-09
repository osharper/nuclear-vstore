using System;

using NuClear.VStore.Objects.ContentValidation;

namespace NuClear.VStore.Descriptors.Templates
{
    public class FormattedTextElementConstraints : TextElementConstraints, IEquatable<FormattedTextElementConstraints>
    {
        public bool ValidHtml => true;

        public bool NoEmptyLists => true;

        public bool NoNestedLists => true;

        public string[] SupportedAttributes { get; } = new string[0];

        public string[] SupportedTags { get; } =
            {
                ElementFormattedTextTagNames.Break,
                ElementFormattedTextTagNames.UnorderedList,
                ElementFormattedTextTagNames.ListItem,
                ElementFormattedTextTagNames.Strong,
                ElementFormattedTextTagNames.Bold,
                ElementFormattedTextTagNames.Emphasis,
                ElementFormattedTextTagNames.Italic
            };

        public string[] SupportedListElements { get; } =
            {
                ElementFormattedTextTagNames.ListItem
            };

        public override bool Equals(object obj)
        {
            var other = obj as FormattedTextElementConstraints;
            return Equals(other);
        }

        public bool Equals(FormattedTextElementConstraints other) => base.Equals(other);

        public override int GetHashCode() => base.GetHashCode();
    }
}
