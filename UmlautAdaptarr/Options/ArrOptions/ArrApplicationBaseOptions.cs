namespace UmlautAdaptarr.Options.ArrOptions
{
    /// <summary>
    /// Base Options for ARR applications
    /// </summary>
    public class ArrApplicationBaseOptions
    {
        /// <summary>
        /// Indicates whether the Arr application is enabled.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// The host of the ARR application.
        /// </summary>
        public string Host { get; set; }

        /// <summary>
        /// The API key of the ARR application.
        /// </summary>
        public string ApiKey { get; set; }
    }
}