﻿using System;
using System.Linq;
using Smartstore.Engine.Modularity;

namespace Smartstore.Core.Content.Widgets
{
    public static class WidgetExtensions
    {
        public static bool IsWidgetActive(this Provider<IWidget> widget, WidgetSettings widgetSettings)
        {
            Guard.NotNull(widget, nameof(widget));

            return widget.ToLazy().IsWidgetActive(widgetSettings);
        }

        public static bool IsWidgetActive(this Lazy<IWidget, ProviderMetadata> widget, WidgetSettings widgetSettings)
        {
            Guard.NotNull(widget, nameof(widget));
            Guard.NotNull(widgetSettings, nameof(widgetSettings));

            if (widgetSettings.ActiveWidgetSystemNames == null)
            {
                return false;
            }

            return widgetSettings.ActiveWidgetSystemNames.Contains(widget.Metadata.SystemName, StringComparer.OrdinalIgnoreCase);
        }
    }
}
