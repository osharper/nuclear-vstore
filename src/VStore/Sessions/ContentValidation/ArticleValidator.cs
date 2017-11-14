using System;
using System.IO;

using CHMsharp;

using NuClear.VStore.Sessions.ContentValidation.Errors;

namespace NuClear.VStore.Sessions.ContentValidation
{
    public static class ArticleValidator
    {
        public static void ValidateArticle(int templateCode, Stream inputStream)
        {
            var enumeratorContext = new EnumeratorContext { IsGoalReached = false };
            try
            {
                using (var memoryStream = new MemoryStream())
                {
                    inputStream.CopyTo(memoryStream);
                    memoryStream.Position = 0;

                    ChmFile.Open(memoryStream)
                           .Enumerate(
                               EnumerateLevel.Normal | EnumerateLevel.Files,
                               ArticleEnumeratorCallback,
                               enumeratorContext);
                }
            }
            catch (InvalidDataException)
            {
                throw new InvalidBinaryException(templateCode, new InvalidArticleError());
            }

            if (!enumeratorContext.IsGoalReached)
            {
                throw new InvalidBinaryException(templateCode, new ArticleMissingIndexError());
            }
        }

        private static EnumerateStatus ArticleEnumeratorCallback(ChmFile file, ChmUnitInfo unitInfo, EnumeratorContext context)
        {
            if (string.Equals(unitInfo.path.TrimStart('/'), "index.html", StringComparison.OrdinalIgnoreCase))
            {
                context.IsGoalReached = true;
                return EnumerateStatus.Success;
            }

            return EnumerateStatus.Continue;
        }
    }
}
