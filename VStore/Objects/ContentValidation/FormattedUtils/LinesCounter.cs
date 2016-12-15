using System;

using HtmlAgilityPack;

namespace NuClear.VStore.Objects.ContentValidation.FormattedUtils
{
    internal class LinesCounter : HtmlNodeVisitor
    {
        private bool _outline;
        private int _result;

        public int CountLines(string text)
        {
            _result = 0;
            _outline = true;
            var html = new HtmlDocument();
            html.LoadHtml(text);

            Visit(html.DocumentNode);

            return _result;
        }

        protected override void VisitText(HtmlTextNode node)
        {
            if (_outline)
            {
                _result++;
                _outline = false;
            }
        }

        protected override void VisitElement(HtmlNode node)
        {
            if (_outline)
            {
                if (IsListItem(node))
                {
                    _result++;
                    _outline = false;
                    base.VisitElement(node);
                    _outline = true;
                    return;
                }

                if (IsBreak(node))
                {
                    _result++;
                    return;
                }
            }
            else
            {
                if (IsListItem(node))
                {
                    _result++;
                    base.VisitElement(node);
                    _outline = true;
                    return;
                }

                if (IsBreak(node))
                {
                    _outline = true;
                    return;
                }
            }

            base.VisitElement(node);
        }

        private static bool IsBreak(HtmlNode node)
        {
            return ElementFormattedTextTagNames.Break.Equals(node.Name, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsListItem(HtmlNode node)
        {
            return ElementFormattedTextTagNames.ListItem.Equals(node.Name, StringComparison.OrdinalIgnoreCase);
        }
    }
}
