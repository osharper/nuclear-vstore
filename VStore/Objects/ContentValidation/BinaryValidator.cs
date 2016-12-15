using System;
using System.Collections.Generic;

using NuClear.VStore.Descriptors.Objects;
using NuClear.VStore.Descriptors.Templates;
using NuClear.VStore.Objects.ContentValidation.Exceptions;

namespace NuClear.VStore.Objects.ContentValidation
{
    public static class BinaryValidator
    {
        public static IEnumerable<ObjectElementValidationException> CheckFilename(IObjectElementValue elementValue, IElementConstraints elementConstraints)
        {
            var value = (IBinaryElementValue)elementValue;
            var constraints = (IBinaryElementConstraints)elementConstraints;

            return constraints.MaxFilenameLength.HasValue && value.Filename.Length > constraints.MaxFilenameLength
                ? new[] { new FilenameTooLongException(constraints.MaxFilenameLength.Value, value.Filename.Length) }
                : Array.Empty<ObjectElementValidationException>();
        }
    }
}
