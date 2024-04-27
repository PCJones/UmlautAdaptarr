using UmlautAdaptarr.Interfaces;
using UmlautAdaptarr.Models;

namespace UmlautAdaptarr.Providers;

public abstract class ArrClientBase : IArrApplication
{
    public string InstanceName;
    public abstract Task<IEnumerable<SearchItem>> FetchAllItemsAsync();
    public abstract Task<SearchItem?> FetchItemByExternalIdAsync(string externalId);
    public abstract Task<SearchItem?> FetchItemByTitleAsync(string title);
}