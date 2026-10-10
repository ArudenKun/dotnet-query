namespace DotNetQuery.Core.Internals;

internal sealed class QueryClient : IQueryClient, IQueryClientInspector
{
    private readonly QueryCache _cache;
    private readonly QueryClientOptions _globalOptions;
    private readonly TimeProvider _timeProvider;
    private readonly QueryInstrumentation _instrumentation;

    public QueryClient(
        QueryClientOptions globalOptions,
        TimeProvider timeProvider,
        QueryInstrumentation instrumentation
    )
    {
        _globalOptions = globalOptions;
        _timeProvider = timeProvider;
        _instrumentation = instrumentation;
        _cache = new(_timeProvider, _instrumentation);
    }

    public Observable<IReadOnlyList<IQueryInspector>> CacheEntries => _cache.Entries;

    public IQuery<TArgs, TData> CreateQuery<TArgs, TData>(QueryOptions<TArgs, TData> options)
        where TData : class =>
        new QueryObserver<TArgs, TData>(options, _globalOptions, _cache, _timeProvider, _instrumentation);

    public IInfiniteQuery<TArgs, TData, TPageParam> CreateInfiniteQuery<TArgs, TData, TPageParam>(
        InfiniteQueryOptions<TArgs, TData, TPageParam> options
    )
        where TData : class =>
        new InfiniteQueryObserver<TArgs, TData, TPageParam>(
            options,
            _globalOptions,
            _cache,
            _timeProvider,
            _instrumentation
        );

    public IMutation<TArgs, TData> CreateMutation<TArgs, TData>(MutationOptions<TArgs, TData> options)
    {
        var mutation = new Mutation<TArgs, TData>(options, _globalOptions, _instrumentation);

        if (options.InvalidateKeys is { Count: > 0 } keys)
        {
            var subscription = mutation.Success.Subscribe(_ =>
            {
                foreach (var key in keys)
                {
                    Invalidate(key);
                }
            });

            mutation.AddDisposable(subscription);
        }

        return mutation;
    }

    public void Invalidate(QueryKey key) => _cache.Invalidate(key);

    public void Invalidate(Func<QueryKey, bool> predicate) => _cache.Invalidate(predicate);

    public void Dispose() => _cache.Dispose();
}
