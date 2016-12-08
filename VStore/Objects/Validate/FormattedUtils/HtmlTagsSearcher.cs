using System.Collections.Generic;
using System.Linq;

using HtmlAgilityPack;

namespace NuClear.VStore.Objects.Validate.FormattedUtils
{
    internal class HtmlTagsSearcher : HtmlNodeVisitor
    {
        private readonly IReadOnlyCollection<string> _supportedTags;
        private HashSet<string> _unsupportedTags;

        public HtmlTagsSearcher(IReadOnlyCollection<string> supportedTags)
        {
            _supportedTags = supportedTags;
        }

        public IReadOnlyCollection<string> GetUnsupportedTags(string text)
        {
            _unsupportedTags = new HashSet<string>();
            var html = new HtmlDocument();
            html.LoadHtml(text);
            Visit(html.DocumentNode);
            return _unsupportedTags;
        }

        protected override void VisitElement(HtmlNode node)
        {
            if (!_supportedTags.Contains(node.Name))
            {
                _unsupportedTags.Add(node.Name);
            }

            base.VisitElement(node);
        }
    }
}
