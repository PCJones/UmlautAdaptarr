using Microsoft.Extensions.Caching.Memory;
using UmlautAdaptarr.Models;
using UmlautAdaptarr.Services;

namespace UmlautAdaptarr.Providers
{
    public abstract class ArrClientBase()
    {
        public abstract Task<IEnumerable<SearchItem>> FetchAllItemsAsync();
        public abstract Task<SearchItem?> FetchItemByExternalIdAsync(string externalId);
        public abstract Task<SearchItem?> FetchItemByTitleAsync(string title);
    }
}
