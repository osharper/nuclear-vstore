using System.Collections.Generic;
using System.Linq;

using NuClear.VStore.Descriptors.Templates;

namespace NuClear.VStore.Descriptors.Sessions
{
    public static class TemplateDescriptorExtensions
    {
        public static IReadOnlyCollection<int> GetBinaryElementTemplateCodes(this ITemplateDescriptor templateDescriptor)
        {
            return templateDescriptor.Elements
                                     .Where(x => x.Type == ElementDescriptorType.Article ||
                                                 x.Type == ElementDescriptorType.BitmapImage ||
                                                 x.Type == ElementDescriptorType.VectorImage)
                                     .Select(x => x.TemplateCode)
                                     .ToList();
        }
    }
}
