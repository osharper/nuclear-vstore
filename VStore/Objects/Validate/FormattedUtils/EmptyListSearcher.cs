using System;
using System.Linq;

using HtmlAgilityPack;

namespace NuClear.VStore.Objects.Validate.FormattedUtils
{
    internal class EmptyListSearcher : HtmlNodeVisitor
    {
        private bool _emptyListFound;

        public bool IsThereEmptyList(string text)
        {
            _emptyListFound = false;
            var html = new HtmlDocument();
            html.LoadHtml(text);
            Visit(html.DocumentNode);
            return _emptyListFound;
        }

        protected override void VisitElement(HtmlNode node)
        {
            if (node.Name.Equals(ElementFormattedTextTagNames.UnorderedList, StringComparison.OrdinalIgnoreCase))
            {
                _emptyListFound |=
                    !node.ChildNodes.Any(x => x.Name.Equals(ElementFormattedTextTagNames.ListItem, StringComparison.OrdinalIgnoreCase));
            }

            if (_emptyListFound)
            {
                return;
            }

            base.VisitElement(node);
        }

        protected override void Visit(HtmlNode node)
        {
            if (_emptyListFound)
            {
                return;
            }

            base.Visit(node);
        }
    }
}
