﻿using System.Diagnostics;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Html;

namespace Smartstore.Core.Widgets
{
    [DebuggerDisplay("{DebuggerToString()}")]
    public class ZoneHtmlContent : IHtmlContent
    {
        private HtmlContentBuilder _preContent;
        private HtmlContentBuilder _postContent;

        /// <summary>
        /// Gets a value indicating whether the content is empty or whitespace.
        /// </summary>
        public bool IsEmptyOrWhiteSpace 
        { 
            get
            {
                if (HasPreContent)
                {
                    return false;
                }

                if (HasPostContent)
                {
                    return false;
                }

                return true;
            }
        }

        /// <summary>
        /// Gets a value indicating whether any PRE content exists.
        /// </summary>
        public bool HasPreContent
        {
            get => _preContent != null && _preContent.HasContent();
        }

        /// <summary>
        /// Gets a value indicating whether any POST content exists.
        /// </summary>
        public bool HasPostContent
        {
            get => _postContent != null && _postContent.HasContent();
        }

        /// <summary>
        /// The zone content that should precede the existing content.
        /// </summary>
        public IHtmlContentBuilder PreContent
        {
            get => _preContent ??= new HtmlContentBuilder();
        }

        /// <summary>
        /// The zone content that should follow the existing content.
        /// </summary>
        public IHtmlContentBuilder PostContent
        {
            get => _postContent ??= new HtmlContentBuilder();
        }

        public void WriteTo(TextWriter writer, HtmlEncoder encoder)
        {
            if (_preContent != null)
            {
                _preContent.WriteTo(writer, encoder);
            }

            if (_postContent != null)
            {
                _postContent.WriteTo(writer, encoder);
            }
        }

        private string DebuggerToString()
        {
            using var writer = new StringWriter();
            WriteTo(writer, HtmlEncoder.Default);
            return writer.ToString();
        }
    }
}
