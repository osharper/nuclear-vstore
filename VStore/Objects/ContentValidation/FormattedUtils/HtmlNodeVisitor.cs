using System;

using HtmlAgilityPack;

namespace NuClear.VStore.Objects.ContentValidation.FormattedUtils
{
    internal abstract class HtmlNodeVisitor
    {
        protected virtual void Visit(HtmlNode node)
        {
            switch (node.NodeType)
            {
                case HtmlNodeType.Document:
                    {
                        VisitDocument(node);
                        break;
                    }

                case HtmlNodeType.Text:
                    {
                        VisitText((HtmlTextNode)node);
                        break;
                    }

                case HtmlNodeType.Element:
                    {
                        VisitElement(node);
                        break;
                    }

                case HtmlNodeType.Comment:
                    {
                        VisitComment((HtmlCommentNode)node);
                        break;
                    }

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        protected virtual void VisitText(HtmlTextNode node)
        {
            if (node.HasChildNodes)
            {
                throw new InvalidOperationException();
            }
        }

        protected virtual void VisitComment(HtmlCommentNode node)
        {
        }

        protected virtual void VisitElement(HtmlNode node)
        {
            foreach (var childNode in node.ChildNodes)
            {
                Visit(childNode);
            }
        }

        protected virtual void VisitDocument(HtmlNode node)
        {
            foreach (var childNode in node.ChildNodes)
            {
                Visit(childNode);
            }
        }
    }
}
