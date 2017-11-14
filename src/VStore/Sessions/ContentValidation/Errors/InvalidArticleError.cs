using NuClear.VStore.Descriptors.Templates;

namespace NuClear.VStore.Sessions.ContentValidation.Errors
{
    public class InvalidArticleError : BinaryValidationError
    {
        public override string ErrorType => nameof(ArticleElementConstraints.ValidArticle);
    }
}
