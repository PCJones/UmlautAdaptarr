namespace UmlautAdaptarr.Options
{
    /// <summary>
    /// Global options for the UmlautAdaptarr application.
    /// </summary>
    public class GlobalOptions
    {
        /// <summary>
        /// The host of the UmlautAdaptarr API.
        /// </summary>
        public string UmlautAdaptarrApiHost { get; set; }

        /// <summary>
        /// The User-Agent string used in HTTP requests.
        /// </summary>
        public string UserAgent { get; set; }

        /// <summary>
        /// The duration in minutes to cache the indexer requests.
        /// </summary>  
        public int IndexerRequestsCacheDurationInMinutes { get; set; } = 12;

        /// <summary>
        /// API key for requests to the UmlautAdaptarr. Optional.
        /// </summary>
        public string? ApiKey { get; set; } = null;

        /// <summary>
        /// Proxy port for the internal UmlautAdaptarr proxy.
        /// </summary>
        public int ProxyPort { get; set; } = 5006;

		/// <summary>
		/// Enable or disable the cache for changed titles.
		/// </summary>
		public bool EnableChangedTitleCache { get; set; } = false;
	}
}