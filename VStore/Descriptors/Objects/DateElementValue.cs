using System;

namespace NuClear.VStore.Descriptors.Objects
{
    public class DateElementValue : IObjectElementValue
    {
        public string Raw { get; set; }

        public DateTime BeginDate { get; set; }

        public DateTime EndDate { get; set; }
    }
}
