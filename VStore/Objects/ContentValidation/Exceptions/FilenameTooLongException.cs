namespace NuClear.VStore.Objects.ContentValidation.Exceptions
{
    public class FilenameTooLongException : ObjectElementValidationException
    {
        public FilenameTooLongException(int maxFilenameLength, int length)
        {
            MaxFilenameLength = maxFilenameLength;
            Length = length;
        }

        public int MaxFilenameLength { get; set; }

        public int Length { get; set; }

        public override ElementValidationErrors ErrorType => ElementValidationErrors.FilenameTooLong;
    }
}
