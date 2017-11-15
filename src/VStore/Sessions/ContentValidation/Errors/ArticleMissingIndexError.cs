using NuClear.VStore.Descriptors.Templates;

namespace NuClear.VStore.Sessions.ContentValidation.Errors
{
    public class ArticleMissingIndexError : BinaryValidationError
    {
        public override string ErrorType => nameof(ArticleElementConstraints.ContainsIndexFile);
    }
}
