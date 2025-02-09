﻿using UmlautAdaptarr.Interfaces;
using UmlautAdaptarr.Providers;

namespace UmlautAdaptarr.Services.Factory
{
    /// <summary>
    /// Factory for creating ArrApplication instances.
    /// </summary>
    public class ArrApplicationFactory
    {
        private readonly ILogger<ArrApplicationFactory> _logger;

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
        /// Constructor for the ArrApplicationFactory.
        /// </summary>
        /// <param name="arrApplications">A dictionary of IArrApplication instances.</param>
        /// <param name="logger">Logger Instanz</param>
        public ArrApplicationFactory(IDictionary<string, IArrApplication> arrApplications, ILogger<ArrApplicationFactory> logger)
        {
            _logger = logger;
            try
            {
                SonarrInstances = arrApplications.Values.OfType<SonarrClient>();
                LidarrInstances = arrApplications.Values.OfType<LidarrClient>();
                ReadarrInstances = arrApplications.Values.OfType<ReadarrClient>();
                AllInstances = arrApplications;

                if (AllInstances.Values.Count == 0)
                {
                    throw new Exception("No ArrApplication could be successfully initialized. This could be due to a faulty configuration");
                }
            }
            catch (Exception e)
            {
                _logger.LogError("Error while registering ArrFactory. This is most likely a config problem, please check your environment variables.", e.Message);
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
