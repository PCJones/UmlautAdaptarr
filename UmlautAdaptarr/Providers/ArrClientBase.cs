using UmlautAdaptarr.Interfaces;
using UmlautAdaptarr.Models;

namespace UmlautAdaptarr.Providers;

public abstract class ArrClientBase : IArrApplication
{
#pragma warning disable CS8618 // Ein Non-Nullable-Feld muss beim Beenden des Konstruktors einen Wert ungleich NULL enthalten. Erwägen Sie die Deklaration als Nullable.
    public string InstanceName;
#pragma warning restore CS8618 // Ein Non-Nullable-Feld muss beim Beenden des Konstruktors einen Wert ungleich NULL enthalten. Erwägen Sie die Deklaration als Nullable.
    public abstract Task<IEnumerable<SearchItem>> FetchAllItemsAsync();
    public abstract Task<SearchItem?> FetchItemByExternalIdAsync(string externalId);
    public abstract Task<SearchItem?> FetchItemByTitleAsync(string title);
}