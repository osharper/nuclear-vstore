using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using HtmlAgilityPack;

namespace NuClear.VStore.Objects.Validate.FormattedUtils
{
    internal class FormattingTagsRemover : HtmlNodeVisitor
    {
        private readonly string[] _formattingTags =
            {
                ElementFormattedTextTagNames.Strong,
                ElementFormattedTextTagNames.Bold,
                ElementFormattedTextTagNames.Emphasis,
                ElementFormattedTextTagNames.Italic
            };

        private List<HtmlNode> _nodesToDelete;

        public string RemoveFormattingTags(string text)
        {
            _nodesToDelete = new List<HtmlNode>();
            var html = new HtmlDocument();
            html.LoadHtml(text);
            Visit(html.DocumentNode);

            foreach (var node in _nodesToDelete)
            {
                RemoveNodeKeepChildren(node);
            }

            var sb = new StringBuilder();
            using (var sw = new StringWriter(sb))
            {
                html.Save(sw);
            }

            return sb.ToString();
        }

        protected override void VisitElement(HtmlNode node)
        {
            if (_formattingTags.Contains(node.Name))
            {
                _nodesToDelete.Add(node);
            }

            base.VisitElement(node);
        }

        private static void RemoveNodeKeepChildren(HtmlNode node)
        {
            foreach (var child in node.ChildNodes)
            {
                node.ParentNode.InsertBefore(child, node);
            }

            node.Remove();
        }
    }
}
