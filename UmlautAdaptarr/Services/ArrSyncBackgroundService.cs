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
        ReadarrClient readarrClient,
        CacheService cacheService,
        ILogger<ArrSyncBackgroundService> logger) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.LogInformation("ArrSyncBackgroundService is starting.");
            bool lastRunSuccess = true;

            while (!stoppingToken.IsCancellationRequested)
            {
                logger.LogInformation("ArrSyncBackgroundService is running.");
                var syncSuccess = await FetchAndUpdateDataAsync();
                logger.LogInformation("ArrSyncBackgroundService has completed an iteration.");

                if (syncSuccess)
                {
                    lastRunSuccess = true;
                    await Task.Delay(TimeSpan.FromHours(12), stoppingToken);
                }
                else
                {
                    if (lastRunSuccess)
                    {
                        lastRunSuccess = false;
                        logger.LogInformation("ArrSyncBackgroundService is trying again in 2 minutes because not all syncs were successful.");
                        await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
                    }
                    else
                    {
                        logger.LogInformation("ArrSyncBackgroundService is trying again in one hour only because not all syncs were successful twice in a row.");
                        await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                    }
                }
            }

            logger.LogInformation("ArrSyncBackgroundService is stopping.");
        }

        private async Task<bool> FetchAndUpdateDataAsync()
        {
            try
            {
                var success = true;
                if (readarrClient.ReadarrOptions.Enabled)
                {
                    var syncSuccess = await FetchItemsFromReadarrAsync();
                    success = success && syncSuccess;
                }
                if (sonarrClient.SonarrOptions.Enabled)
                {
                    var syncSuccess = await FetchItemsFromSonarrAsync();
                    success = success && syncSuccess;
                }
                if (lidarrClient.LidarrOptions.Enabled)
                {
                    var syncSuccess = await FetchItemsFromLidarrAsync();
                    success = success && syncSuccess;
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

        private async Task<bool> FetchItemsFromReadarrAsync()
        {
            try
            {
                var items = await readarrClient.FetchAllItemsAsync();
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
