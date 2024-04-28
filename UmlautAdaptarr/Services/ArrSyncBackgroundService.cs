using UmlautAdaptarr.Models;
using UmlautAdaptarr.Services.Factory;

namespace UmlautAdaptarr.Services;

public class ArrSyncBackgroundService(
    ArrApplicationFactory arrApplicationFactory,
    CacheService cacheService,
    ILogger<ArrSyncBackgroundService> logger)
    : BackgroundService
{
    public ArrApplicationFactory ArrApplicationFactory { get; } = arrApplicationFactory;


    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("ArrSyncBackgroundService is starting.");
        var lastRunSuccess = true;

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
                    logger.LogInformation(
                        "ArrSyncBackgroundService is trying again in 2 minutes because not all syncs were successful.");
                    await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
                }
                else
                {
                    logger.LogInformation(
                        "ArrSyncBackgroundService is trying again in one hour only because not all syncs were successful twice in a row.");
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


            if (ArrApplicationFactory.SonarrInstances.Any())
            {
                var syncSuccess = await FetchItemsFromSonarrAsync();
                success = success && syncSuccess;
            }

            if (ArrApplicationFactory.ReadarrInstances.Any())
            {
                var syncSuccess = await FetchItemsFromReadarrAsync();
                success = success && syncSuccess;
            }

            if (ArrApplicationFactory.ReadarrInstances.Any())
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
            var items = new List<SearchItem>();

            foreach (var sonarrClient in ArrApplicationFactory.SonarrInstances)
            {
                var result = await sonarrClient.FetchAllItemsAsync();
                items = items.Union(result).ToList();
            }


            UpdateSearchItems(items);
            return items?.Any() ?? false;
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
            var items = new List<SearchItem>();

            foreach (var lidarrClient in ArrApplicationFactory.LidarrInstances)
            {
                var result = await lidarrClient.FetchAllItemsAsync();
                items = items.Union(result).ToList();
            }

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
            var items = new List<SearchItem>();

            foreach (var readarrClient in ArrApplicationFactory.ReadarrInstances)
            {
                var result = await readarrClient.FetchAllItemsAsync();
                items = items.Union(result).ToList();
            }

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