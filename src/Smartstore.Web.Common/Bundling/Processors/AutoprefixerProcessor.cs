﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AutoprefixerHost;
using AutoprefixerHost.Helpers;
using JavaScriptEngineSwitcher.V8;

namespace Smartstore.Web.Bundling.Processors
{
    public class AutoprefixerProcessor : BundleProcessor
    {
        internal const string Code = "autoprefix";
        internal static readonly AutoprefixerProcessor Instance = new();

        public override Task ProcessAsync(BundleContext context)
        {
            if (context.Options.EnableAutoPrefixer == false || context.ProcessorCodes.Contains(Code))
            {
                return Task.CompletedTask;
            }

            var options = new ProcessingOptions
            {
                Browsers = new List<string> 
                {
                    "last 2 version",
                    "> 1%",
                    "Chrome >= 70",
                    "Firefox >= 52",
                    "Edge >= 17",
                    "Explorer > 11",
                    "iOS >= 11",
                    "Safari >= 11",
                    "Android >= 4.4",
                    "Opera >= 63"
                },
                Cascade = false,
                Add = true,
                Remove = true,
                Supports = true,
                Flexbox = FlexboxMode.None,
                Grid = GridMode.None,
                IgnoreUnknownVersions = false
            };

            try
            {
                using (var autoprefixer = new Autoprefixer(new V8JsEngineFactory(), options))
                {
                    foreach (var asset in context.Content)
                    {
                        try
                        {
                            var result = autoprefixer.Process(asset.Content, context.HttpContext.Request.Path);
                            asset.Content = result.ProcessedContent;
                        }
                        catch (AutoprefixerProcessingException ex)
                        {
                            HandleError(asset, AutoprefixerErrorHelpers.GenerateErrorDetails(ex));
                        }
                        catch (AutoprefixerException ex)
                        {
                            HandleError(asset, AutoprefixerErrorHelpers.GenerateErrorDetails(ex));
                        }
                    }
                }
            }
            catch (AutoprefixerLoadException)
            {
                //HandleError(null, AutoprefixerErrorHelpers.GenerateErrorDetails(ex));
                throw;
            }

            return Task.CompletedTask;
        }

        private void HandleError(AssetContent asset, string message)
        {
            var errorHeader = string.Concat(
                "// AutoPrefixer error ======================================================================\r\n",
                "/*\r\n",
                message + "\r\n",
                "*/\r\n",
                "// =========================================================================================\r\n\r\n");

            asset.Content = errorHeader + asset.Content;
        }
    }
}
