using R3;

namespace DotNetQuery.Mvvm.Tests;

public class QueryViewModelLifecycleTests
{
    private readonly Mock<IQuery<int, string>> _queryMock = Mock.Of<IQuery<int, string>>();
    private readonly RecordingSynchronizationContext _uiContext = new();
    private ReactiveProperty<QueryState<string>> _stateSubject = default!;

    [After(Test)]
    public void Teardown()
    {
        _stateSubject.Dispose();
    }

    private QueryViewModel<int, string> CreateSut(QueryState<string> initialState)
    {
        _stateSubject = new(initialState);
        _queryMock.CurrentState.Returns(_stateSubject.CurrentValue);
        _queryMock.State.Returns(_stateSubject.AsObservable());

        return new QueryViewModel<int, string>(_queryMock.Object, new SynchronizationContextUiDispatcher(_uiContext));
    }

    [Test]
    public async Task Ctor_SubscribesToState()
    {
        using var sut = CreateSut(QueryState<string>.CreateIdle());

        // No HasObservers in R3. Verify the subscription is live by observing
        // that the SUT reacts to a state change from the source.
        _stateSubject.OnNext(QueryState<string>.CreateFetching());
        _uiContext.DrainAll();

        await Assert.That(sut.IsFetching).IsTrue();
    }

    [Test]
    public async Task Dispose_WithWrappedQuery_ReleasesSubscriptionButNotQuery()
    {
        var sut = CreateSut(QueryState<string>.CreateIdle());

        sut.Dispose();

        // Subscription released: pushing a new state no longer reaches the SUT.
        _stateSubject.OnNext(QueryState<string>.CreateFetching());
        _uiContext.DrainAll();

        await Assert.That(sut.IsFetching).IsFalse();
        await Assert.That(Mock.Invocations(_queryMock).Any(i => i.MemberName == nameof(IDisposable.Dispose))).IsFalse();
    }

    private QueryViewModel<int, string> CreateClientOwnedSut(out Mock<IQueryClient> clientMock)
    {
        _stateSubject = new(QueryState<string>.CreateIdle());
        _queryMock.CurrentState.Returns(_stateSubject.CurrentValue);
        _queryMock.State.Returns(_stateSubject.AsObservable());

        clientMock = Mock.Of<IQueryClient>();
        clientMock.CreateQuery(Any<QueryOptions<int, string>>()).Returns(_queryMock.Object);

        var options = new QueryOptions<int, string>
        {
            KeyFactory = args => QueryKey.From("test", args),
            Fetcher = (_, _) => Task.FromResult("data"),
        };

        return new QueryViewModel<int, string>(
            clientMock.Object,
            options,
            new SynchronizationContextUiDispatcher(_uiContext)
        );
    }

    [Test]
    public async Task Dispose_WithClientCreatedQuery_DisposesQuery()
    {
        var sut = CreateClientOwnedSut(out _);

        sut.Dispose();

        // Subscription released.
        _stateSubject.OnNext(QueryState<string>.CreateFetching());
        _uiContext.DrainAll();

        await Assert.That(sut.IsFetching).IsFalse();
        await Assert.That(Mock.Invocations(_queryMock).Any(i => i.MemberName == nameof(IDisposable.Dispose))).IsTrue();
    }

    [Test]
    public async Task Dispose_CalledTwice_DoesNotThrow()
    {
        var sut = CreateSut(QueryState<string>.CreateIdle());

        sut.Dispose();
        sut.Dispose();

        // Still released after the second dispose.
        _stateSubject.OnNext(QueryState<string>.CreateFetching());
        _uiContext.DrainAll();

        await Assert.That(sut.IsFetching).IsFalse();
    }

    [Test]
    public async Task PendingPost_DrainedAfterDispose_DoesNotRaisePropertyChanged()
    {
        var sut = CreateSut(QueryState<string>.CreateIdle());
        var raised = new List<string?>();
        sut.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        _stateSubject.OnNext(QueryState<string>.CreateFetching());
        sut.Dispose();
        _uiContext.DrainAll();

        await Assert.That(raised).IsEmpty();
        await Assert.That(sut.IsFetching).IsFalse();
    }

    [Test]
    public async Task SetArgs_DelegatesToQuery()
    {
        using var sut = CreateSut(QueryState<string>.CreateIdle());

        sut.SetArgs(42);

        var invocation = Mock.Invocations(_queryMock).Single(i => i.MemberName == nameof(IQuery<int, string>.SetArgs));
        await Assert.That(invocation.Arguments[0]).IsEqualTo(42);
    }

    [Test]
    public async Task SetEnabled_DelegatesToQuery()
    {
        using var sut = CreateSut(QueryState<string>.CreateIdle());

        sut.SetEnabled(false);

        var invocation = Mock.Invocations(_queryMock)
            .Single(i => i.MemberName == nameof(IQuery<int, string>.SetEnabled));
        await Assert.That((bool)invocation.Arguments[0]!).IsFalse();
    }

    [Test]
    public async Task SetData_DelegatesToQuery()
    {
        using var sut = CreateSut(QueryState<string>.CreateIdle());

        sut.SetData("optimistic");

        var invocation = Mock.Invocations(_queryMock).Single(i => i.MemberName == nameof(IQuery<int, string>.SetData));
        await Assert.That(invocation.Arguments[0]).IsEqualTo("optimistic");
    }
}
