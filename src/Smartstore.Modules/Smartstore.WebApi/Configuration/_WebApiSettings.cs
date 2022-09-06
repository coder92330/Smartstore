﻿using Smartstore.Core.Configuration;

namespace Smartstore.WebApi
{
    public class WebApiSettings : ISettings
    {
        /// <summary>
        /// Gets the max value of $top that a client can request.
        /// </summary>
        public const int DefaultMaxTop = 120;

        /// <summary>
        /// Gets the max expansion depth for the $expand query option.
        /// </summary>
        public const int DefaultMaxExpansionDepth = 8;

        /// <summary>
        /// Gets or sets a value indicating whether to log unauthorized API calls.
        /// </summary>
        public bool LogUnauthorized { get; set; }

        /// <summary>
        /// Gets or sets the max value of $top that a client can request.
        /// </summary>
        public int MaxTop { get; set; } = DefaultMaxTop;

        /// <summary>
        /// Gets or sets the max expansion depth for the $expand query option.
        /// </summary>
        public int MaxExpansionDepth { get; set; } = DefaultMaxExpansionDepth;
    }
}
