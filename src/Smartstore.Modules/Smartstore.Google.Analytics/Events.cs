﻿using DouglasCrockford.JsMin;
using Microsoft.AspNetCore.Html;
using Smartstore.Core.Widgets;
using Smartstore.Events;
using Smartstore.Google.Analytics.Services;
using Smartstore.Google.Analytics.Settings;
using Smartstore.Web.Components;
using Smartstore.Web.Models.Catalog;

namespace Smartstore.Google.Analytics
{
    public class Events : IConsumer
    {
        // TODO: (mh) (core) Maybe we must shift home_page_after_products & home_page_after_bestsellers into the components.
        private static readonly Dictionary<string, string> _interceptableViewComponents = new(StringComparer.OrdinalIgnoreCase)
        {
            { "HomeProducts", "home_page_after_products" },
            { "HomeBestSellers", "home_page_after_bestsellers" },
            { "RecentlyViewedProducts", "after_recently_viewed_products" },
            { "CrossSellProducts", "after_cross_sell_products" }
        };

        private static readonly JsMinifier Minifier = new();

        private readonly GoogleAnalyticsSettings _settings;
        private readonly GoogleAnalyticsScriptHelper _googleAnalyticsScriptHelper;
        private readonly IWidgetProvider _widgetProvider;

        public Events(GoogleAnalyticsSettings settings, GoogleAnalyticsScriptHelper googleAnalyticsScriptHelper, IWidgetProvider widgetProvider)
        {
            _settings = settings;
            _googleAnalyticsScriptHelper = googleAnalyticsScriptHelper;
            _widgetProvider = widgetProvider;
        }

        public async Task HandleEventAsync(ViewComponentResultExecutingEvent message)
        {
            // If GoogleId is empty or is default don't render anything. Also if catalog scripts are configured not to be rendered.
            if (!_settings.GoogleId.HasValue() || _settings.GoogleId == "UA-0000000-0" || !_settings.RenderCatalogScripts)
                return;

            var componentName = message.Descriptor.ShortName;

            if (!_interceptableViewComponents.Keys.Contains(componentName, StringComparer.OrdinalIgnoreCase))
            {
                return;
            }
            else
            {
                var model = (ProductSummaryModel)message.Model;
                var productList = model.Items;

                if (productList.Count > 0)
                {
                    var itemsScript = await _googleAnalyticsScriptHelper.GetListScriptAsync(productList, componentName);

                    if (_settings.MinifyScripts)
                    {
                        itemsScript = Minifier.Minify(itemsScript);
                    }

                    _widgetProvider.RegisterHtml(_interceptableViewComponents[componentName], new HtmlString($"<script>{itemsScript}</script>"));
                }
            }
        }
    }
}