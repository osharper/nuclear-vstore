namespace NuClear.VStore.Descriptors.Templates
{
    public sealed class ConstraintSetItem
    {
        public ConstraintSetItem(Language language, IElementConstraints elementConstraints)
        {
            Language = language;
            ElementConstraints = elementConstraints;
        }

        public Language Language { get; }
        public IElementConstraints ElementConstraints { get; }
    }
}