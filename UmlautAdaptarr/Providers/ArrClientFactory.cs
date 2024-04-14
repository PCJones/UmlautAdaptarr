namespace UmlautAdaptarr.Providers
{
    public static class ArrClientFactory
    {
        // TODO, still uses old IConfiguration
        // TODO not used yet
        public static IEnumerable<TClient> CreateClients<TClient>(
            Func<string, TClient> constructor, IConfiguration configuration, string configKey) where TClient : ArrClientBase
        {
            var hosts = configuration.GetValue<string>(configKey)?.Split(',') ?? throw new ArgumentException($"{configKey} environment variable must be set if the app is enabled");
            foreach (var host in hosts)
            {
                yield return constructor(host.Trim());
            }
        }
    }
}