using Newtonsoft.Json;

using NuClear.VStore.Descriptors;
using NuClear.VStore.Json;

namespace NuClear.VStore.Sessions.ContentValidation.Errors
{
    public abstract class BinaryValidationError : ValidationError<BinaryConstraintViolations>
    {
        protected static readonly JsonSerializer JsonSerializer = JsonSerializer.Create(SerializerSettings.Default);
    }
}
