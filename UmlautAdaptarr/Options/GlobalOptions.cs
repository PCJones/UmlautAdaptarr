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

    }
}