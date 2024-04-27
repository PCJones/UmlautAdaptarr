using UmlautAdaptarr.Interfaces;
using UmlautAdaptarr.Providers;

namespace UmlautAdaptarr.Services.Factory
{
    /// <summary>
    /// Factory for creating RrApplication instances.
    /// </summary>
    public class RrApplicationFactory
    {
        private readonly ILogger<RrApplicationFactory> _logger;

        /// <summary>
        /// Get all IArrApplication instances.
        /// </summary>
        public IDictionary<string, IArrApplication> AllInstances { get; init; }

        /// <summary>
        /// Get all SonarrClient instances.
        /// </summary>
        public IEnumerable<SonarrClient> SonarrInstances { get; init; }

        /// <summary>
        /// Get all LidarrClient instances.
        /// </summary>
        public IEnumerable<LidarrClient> LidarrInstances { get; init; }

        /// <summary>
        /// Get all ReadarrClient instances.
        /// </summary>
        public IEnumerable<ReadarrClient> ReadarrInstances { get; init; }

        /// <summary>
        /// Constructor for the RrApplicationFactory.
        /// </summary>
        /// <param name="rrArrApplications">A dictionary of IArrApplication instances.</param>
        public RrApplicationFactory(IDictionary<string, IArrApplication> rrArrApplications, ILogger<RrApplicationFactory> logger)
        {
            _logger = logger;
            try
            {
                SonarrInstances = rrArrApplications.Values.OfType<SonarrClient>();
                LidarrInstances = rrArrApplications.Values.OfType<LidarrClient>();
                ReadarrInstances = rrArrApplications.Values.OfType<ReadarrClient>();
                AllInstances = rrArrApplications;

                if (!AllInstances.Values.Any())
                {
                    throw new Exception("No RrApplication could be successfully initialized. This could be due to a faulty configuration");
                }
            }
            catch (Exception e)
            {
                _logger.LogError("Register RrFactory", e.Message);
                throw;
            }
        }

        /// <summary>
        /// Returns an IArrApplication instance that matches the given name.
        /// </summary>
        /// <param name="nameOfArrInstance">The name of the IArrApplication instance being sought.</param>
        /// <returns>The IArrApplication instance that matches the given name.</returns>
        /// <exception cref="ArgumentException">Thrown when no IArrApplication instance with the given name can be found.</exception>
        public IArrApplication GetArrInstanceByName(string nameOfArrInstance)
        {
            var instance = AllInstances.FirstOrDefault(up => up.Key.Equals(nameOfArrInstance)).Value;
            if (instance == null)
            {
                throw new ArgumentException($"No ArrService with the name {nameOfArrInstance} could be found");
            }

            return instance;
        }
    }
}
