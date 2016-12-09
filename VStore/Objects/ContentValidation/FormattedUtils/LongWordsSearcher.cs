using System.Collections.Generic;
using System.Net;

using HtmlAgilityPack;

namespace NuClear.VStore.Objects.ContentValidation.FormattedUtils
{
    internal class LongWordsSearcher : HtmlNodeVisitor
    {
        private readonly int _maxSymbolsInWord;
        private HashSet<string> _result;

        public LongWordsSearcher(int maxSymbolsInWord)
        {
            _maxSymbolsInWord = maxSymbolsInWord;
        }

        public IReadOnlyCollection<string> GetLongWords(string text)
        {
            _result = new HashSet<string>();

            var html = new HtmlDocument();
            html.LoadHtml(new FormattingTagsRemover().RemoveFormattingTags(text));
            Visit(html.DocumentNode);
            return _result;
        }

        protected override void VisitText(HtmlTextNode node)
        {
            var longWords = TextValidationUtils.GetTooLongWords(WebUtility.HtmlDecode(node.InnerText), _maxSymbolsInWord, new[] { ' ', '-', '/', '\t' });
            foreach (var longWord in longWords)
            {
                _result.Add(longWord);
            }

            base.VisitText(node);
        }
    }
}
