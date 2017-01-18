using System;

namespace NuClear.VStore.Descriptors.Objects
{
    public class DateElementValue : IObjectElementValue
    {
        public DateTime? BeginDate { get; set; }

        public DateTime? EndDate { get; set; }
    }
}
