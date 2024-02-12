using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Threading;
using System.Threading.Tasks;
using UmlautAdaptarr.Models;
using UmlautAdaptarr.Providers;

namespace UmlautAdaptarr.Services
{
    public class ArrSyncBackgroundService(
        SonarrClient sonarrClient,
        LidarrClient lidarrClient,
        CacheService cacheService,
        IConfiguration configuration,
        ILogger<ArrSyncBackgroundService> logger) : BackgroundService
    {
        private readonly bool _sonarrEnabled = configuration.GetValue<bool>("SONARR_ENABLED");
        private readonly bool _lidarrEnabled = configuration.GetValue<bool>("LIDARR_ENABLED");
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.LogInformation("ArrSyncBackgroundService is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                logger.LogInformation("ArrSyncBackgroundService is running.");
                var syncSuccess = await FetchAndUpdateDataAsync();
                logger.LogInformation("ArrSyncBackgroundService has completed an iteration.");

                if (syncSuccess)
                {
                    await Task.Delay(TimeSpan.FromHours(12), stoppingToken);
                }
                else
                {
                    logger.LogInformation("ArrSyncBackgroundService is sleeping for one hour only because not all syncs were successful.");
                    await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                }
            }

            logger.LogInformation("ArrSyncBackgroundService is stopping.");
        }

        private async Task<bool> FetchAndUpdateDataAsync()
        {
            try
            {
                var success = true;
                if (_sonarrEnabled)
                {
                    success = await FetchItemsFromSonarrAsync();
                }
                if (_lidarrEnabled)
                {
                    success = await FetchItemsFromLidarrAsync();
                }
                return success;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while fetching items from the Arrs.");
            }
            return false;
        }

        private async Task<bool> FetchItemsFromSonarrAsync()
        {
            try
            {
                var items = await sonarrClient.FetchAllItemsAsync();
                UpdateSearchItems(items);
                return items?.Any()?? false;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while updating search item from Sonarr.");
            }
            return false;
        }

        private async Task<bool> FetchItemsFromLidarrAsync()
        {
            try
            {
                var items = await lidarrClient.FetchAllItemsAsync();
                UpdateSearchItems(items);
                return items?.Any() ?? false;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while updating search item from Lidarr.");
            }
            return false;
        }

        private void UpdateSearchItems(IEnumerable<SearchItem>? searchItems)
        {
            foreach (var searchItem in searchItems ?? [])
            {
                try
                {
                    cacheService.CacheSearchItem(searchItem);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, $"An error occurred while caching search item with ID {searchItem.ArrId}.");
                }
            }
        }
    }
}
