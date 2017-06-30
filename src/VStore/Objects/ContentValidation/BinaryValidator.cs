using System;
using System.Collections.Generic;

using NuClear.VStore.Descriptors.Objects;
using NuClear.VStore.Descriptors.Templates;
using NuClear.VStore.Objects.ContentValidation.Errors;

namespace NuClear.VStore.Objects.ContentValidation
{
    public static class BinaryValidator
    {
        public static IEnumerable<ObjectElementValidationError> CheckFilename(IObjectElementValue elementValue, IElementConstraints elementConstraints)
        {
            var value = (IBinaryElementValue)elementValue;
            if (string.IsNullOrEmpty(value.Filename))
            {
                return Array.Empty<ObjectElementValidationError>();
            }

            var constraints = (IBinaryElementConstraints)elementConstraints;
            return constraints.MaxFilenameLength.HasValue && value.Filename.Length > constraints.MaxFilenameLength
                ? new[] { new FilenameTooLongError(constraints.MaxFilenameLength.Value, value.Filename.Length) }
                : Array.Empty<ObjectElementValidationError>();
        }
    }
}
