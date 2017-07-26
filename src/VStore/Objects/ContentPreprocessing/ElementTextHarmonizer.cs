using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using HtmlAgilityPack;

using NuClear.VStore.Objects.ContentValidation;
using NuClear.VStore.Objects.ContentValidation.FormattedUtils;

namespace NuClear.VStore.Objects.ContentPreprocessing
{
    public static class ElementTextHarmonizer
    {
        public static string ProcessPlain(string rawPlainText) =>
            string.IsNullOrEmpty(rawPlainText)
                ? rawPlainText
                : rawPlainText.Replace("\r\n", "\n")
                              .Replace((char)160, ' ')
                              .TrimEnd();

        public static string ProcessLink(string rawLink) => rawLink?.Trim();

        public static string ProcessFormatted(string rawFormattedText)
        {
            if (string.IsNullOrEmpty(rawFormattedText))
            {
                return rawFormattedText;
            }

            var emptyListItemsRemover = new EmptyListItemsRemover();
            return emptyListItemsRemover.RemoveEmptyListItems(TrimEndFormatted(rawFormattedText.Replace("\r\n", string.Empty)
                                                                                               .Replace("\n", string.Empty)
                                                                                               .Replace("\u001d", string.Empty)
                                                                                               .Replace("&nbsp;", " ")));
        }

        private static string TrimEndFormatted(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(text);

            bool nodeWasRemoved;
            do
            {
                nodeWasRemoved = false;
                var nodeToCheck = htmlDoc.DocumentNode.LastChild;
                if (nodeToCheck == null)
                {
                    break;
                }

                switch (nodeToCheck.NodeType)
                {
                    case HtmlNodeType.Element:
                    {
                        if (nodeToCheck.Name == ElementFormattedTextTagNames.Break)
                        {
                            htmlDoc.DocumentNode.RemoveChild(nodeToCheck);
                            nodeWasRemoved = true;
                        }

                        break;
                    }

                    case HtmlNodeType.Text:
                    {
                        ((HtmlTextNode)nodeToCheck).Text = nodeToCheck.InnerText.Trim();
                        if (string.IsNullOrEmpty(nodeToCheck.InnerText))
                        {
                            htmlDoc.DocumentNode.RemoveChild(nodeToCheck);
                            nodeWasRemoved = true;
                        }

                        break;
                    }
                }
            }
            while (nodeWasRemoved);

            var sb = new StringBuilder();
            using (var sw = new StringWriter(sb))
            {
                htmlDoc.Save(sw);
            }

            return sb.ToString();
        }

        private class EmptyListItemsRemover : HtmlNodeVisitor
        {
            private List<HtmlNode> _nodesToDelete;
            public string RemoveEmptyListItems(string text)
            {
                if (string.IsNullOrEmpty(text))
                {
                    return text;
                }

                _nodesToDelete = new List<HtmlNode>();
                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(text);

                Visit(htmlDoc.DocumentNode);

                foreach (var node in _nodesToDelete)
                {
                    node.ParentNode?.RemoveChild(node);
                }

                var sb = new StringBuilder();
                using (var sw = new StringWriter(sb))
                {
                    htmlDoc.Save(sw);
                }

                return sb.ToString();
            }

            protected override void VisitElement(HtmlNode node)
            {
                if (IsListItem(node))
                {
                    if (node.ChildNodes.All(x =>
                                                IsBreak(x) ||
                                                string.IsNullOrWhiteSpace(x.InnerHtml) ||
                                                x.NodeType == HtmlNodeType.Comment))
                    {
                        _nodesToDelete.Add(node);
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
}
