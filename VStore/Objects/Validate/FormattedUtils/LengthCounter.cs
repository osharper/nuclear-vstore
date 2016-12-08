using System.Net;

using HtmlAgilityPack;

namespace NuClear.VStore.Objects.Validate.FormattedUtils
{
    internal class LengthCounter : HtmlNodeVisitor
    {
        private int _result;

        public int GetLength(string text)
        {
            _result = 0;
            var html = new HtmlDocument();
            html.LoadHtml(text);
            Visit(html.DocumentNode);
            return _result;
        }

        protected override void VisitText(HtmlTextNode node)
        {
            _result += TextValidationUtils.GetTextLength(WebUtility.HtmlDecode(node.InnerText));

            base.VisitText(node);
        }
    }
}
