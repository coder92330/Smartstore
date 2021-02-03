﻿using System;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Smartstore.Web.UI
{
    public static class AttributeDictionaryExtensions
    {
        /// <summary>
        /// Copies all attributes from <paramref name="attributes"/> to <paramref name="target"/>
        /// overriding any existing attribute.
        /// </summary>
        public static void CopyTo(this AttributeDictionary attributes, TagHelperAttributeList target)
        {
            Guard.NotNull(attributes, nameof(attributes));
            Guard.NotNull(target, nameof(target));

            foreach (var attr in attributes)
            {
                target.SetAttribute(attr.Key, attr.Value);
            }
        }
        
        public static AttributeDictionary AppendCssClass(this AttributeDictionary attributes, Func<string> cssClass)
        {
            attributes.AppendInValue("class", ' ', cssClass());
            return attributes;
        }

        public static AttributeDictionary PrependCssClass(this AttributeDictionary attributes, Func<string> cssClass)
        {
            attributes.PrependInValue("class", ' ', cssClass());
            return attributes;
        }

        public static AttributeDictionary AppendCssClass(this AttributeDictionary attributes, string cssClass)
        {
            attributes.AppendInValue("class", ' ', cssClass);
            return attributes;
        }

        public static AttributeDictionary PrependCssClass(this AttributeDictionary attributes, string cssClass)
        {
            attributes.PrependInValue("class", ' ', cssClass);
            return attributes;
        }

        public static void AppendCssClass(this TagBuilder builder, Func<string> cssClass)
        {
            builder.Attributes.AppendInValue("class", ' ', cssClass());
        }

        public static void PrependCssClass(this TagBuilder builder, Func<string> cssClass)
        {
            builder.Attributes.PrependInValue("class", ' ', cssClass());
        }

        public static void AppendCssClass(this TagBuilder builder, string cssClass)
        {
            builder.Attributes.AppendInValue("class", ' ', cssClass);
        }

        public static void PrependCssClass(this TagBuilder builder, string cssClass)
        {
            builder.Attributes.PrependInValue("class", ' ', cssClass);
        }
    }
}
