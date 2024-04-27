namespace UmlautAdaptarr.Options.ArrOptions.InstanceOptions
{
    public class GlobalInstanceOptions
    {
        /// <summary>
        /// Indicates whether the Arr application is enabled.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Name of the Instance
        /// </summary>
        public string Name { get; set; }

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
