namespace DotNetQuery.Core.Internals;

internal interface IQueryClientInspector
{
    public Observable<IReadOnlyList<IQueryInspector>> CacheEntries { get; }
}
