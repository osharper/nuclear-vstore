using System;

using HtmlAgilityPack;

namespace NuClear.VStore.Objects.Validate.FormattedUtils
{
    internal class NestedListSearcher : HtmlNodeVisitor
    {
        private bool _nestedListFound;
        private bool _isInsideOfList;

        public bool IsThereNestedList(string text)
        {
            _nestedListFound = false;
            _isInsideOfList = false;
            var html = new HtmlDocument();
            html.LoadHtml(text);
            Visit(html.DocumentNode);
            return _nestedListFound;
        }

        protected override void VisitElement(HtmlNode node)
        {
            var isInsideOfListSaved = _isInsideOfList;
            if (node.Name.Equals(ElementFormattedTextTagNames.UnorderedList, StringComparison.OrdinalIgnoreCase))
            {
                _nestedListFound |= _isInsideOfList;
                _isInsideOfList = true;
            }

            if (_nestedListFound)
            {
                return;
            }

            base.VisitElement(node);
            _isInsideOfList = isInsideOfListSaved;
        }

        protected override void Visit(HtmlNode node)
        {
            if (_nestedListFound)
            {
                return;
            }

            base.Visit(node);
        }
    }
}
