using UmlautAdaptarr.Models;

namespace UmlautAdaptarr.Interfaces;

public interface IArrApplication
{
    Task<IEnumerable<SearchItem>> FetchAllItemsAsync();
    Task<SearchItem?> FetchItemByExternalIdAsync(string externalId);
    Task<SearchItem?> FetchItemByTitleAsync(string title);
}