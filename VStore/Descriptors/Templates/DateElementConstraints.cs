namespace NuClear.VStore.Descriptors.Templates
{
    public class DateElementConstraints : IElementConstraints
    {
        public int? MinDays { get; set; }

        public int? MaxDays { get; set; }
    }
}
