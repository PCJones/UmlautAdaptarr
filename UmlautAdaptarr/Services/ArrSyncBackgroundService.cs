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
        CacheService cacheService,
        ILogger<ArrSyncBackgroundService> logger) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.LogInformation("ArrSyncBackgroundService is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                logger.LogInformation("ArrSyncBackgroundService is running.");
                await FetchAndUpdateDataAsync();
                logger.LogInformation("ArrSyncBackgroundService has completed an iteration.");

                await Task.Delay(TimeSpan.FromHours(12), stoppingToken);
            }

            logger.LogInformation("ArrSyncBackgroundService is stopping.");
        }

        private async Task FetchAndUpdateDataAsync()
        {
            try
            {
                await FetchItemsFromSonarrAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while fetching items from the Arrs.");
            }
        }

        private async Task FetchItemsFromSonarrAsync()
        {
            try
            {
                var items = await sonarrClient.FetchAllItemsAsync();
                UpdateSearchItems(items);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while updating search item from Sonarr.");
            }
        }

        private void UpdateSearchItems(IEnumerable<SearchItem> searchItems)
        {
            foreach (var searchItem in searchItems)
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
