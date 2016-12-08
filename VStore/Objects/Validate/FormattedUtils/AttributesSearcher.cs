using System.Collections.Generic;

using HtmlAgilityPack;

namespace NuClear.VStore.Objects.Validate.FormattedUtils
{
    internal class AttributesSearcher : HtmlNodeVisitor
    {
        private HashSet<string> _result;

        public IReadOnlyCollection<string> GetAttributes(string text)
        {
            _result = new HashSet<string>();
            var html = new HtmlDocument();
            html.LoadHtml(text);
            Visit(html.DocumentNode);
            return _result;
        }

        protected override void VisitElement(HtmlNode node)
        {
            foreach (var htmlAttribute in node.Attributes)
            {
                _result.Add(htmlAttribute.Name);
            }

            base.VisitElement(node);
        }
    }
}
