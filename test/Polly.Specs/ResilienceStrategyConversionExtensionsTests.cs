using Polly.TestUtils;

namespace Polly.Specs;

public class ResilienceStrategyConversionExtensionsTests
{
    private static readonly ResiliencePropertyKey<string> Incoming = new("incoming-key");

    private static readonly ResiliencePropertyKey<string> Executing = new("executing-key");

    private static readonly ResiliencePropertyKey<string> Outgoing = new("outgoing-key");

    private readonly TestResilienceStrategy _strategy;
    private readonly ResilienceStrategy<string> _genericStrategy;
    private bool _isSynchronous;
    private bool _isVoid;
    private Type? _resultType;

    public ResilienceStrategyConversionExtensionsTests()
    {
        _strategy = new TestResilienceStrategy
        {
            Before = (context, _) =>
            {
                context.IsVoid.Should().Be(_isVoid);
                context.IsSynchronous.Should().Be(_isSynchronous);
                context.Properties.Set(Outgoing, "outgoing-value");
                context.Properties.GetValue(Incoming, string.Empty).Should().Be("incoming-value");
                context.OperationKey.Should().Be("op-key");

                if (_resultType != null)
                {
                    context.ResultType.Should().Be(_resultType);
                }
            }
        };

        _genericStrategy = new ResilienceStrategyBuilder<string>()
            .AddStrategy(_strategy)
            .Build();
    }

    [Fact]
    public void AsSyncPolicy_Ok()
    {
        _isVoid = true;
        _isSynchronous = true;
        var context = new Context("op-key")
        {
            [Incoming.Key] = "incoming-value"
        };

        _strategy.AsSyncPolicy().Execute(_ =>
        {
            context[Executing.Key] = "executing-value";
        },
        context);

        AssertContext(context);
    }

    [Fact]
    public void AsSyncPolicy_Generic_Ok()
    {
        _isVoid = false;
        _isSynchronous = true;
        _resultType = typeof(string);
        var context = new Context("op-key")
        {
            [Incoming.Key] = "incoming-value"
        };

        var result = _genericStrategy.AsSyncPolicy().Execute(_ => { context[Executing.Key] = "executing-value"; return "dummy"; }, context);
        AssertContext(context);
        result.Should().Be("dummy");
    }

    [Fact]
    public void AsSyncPolicy_Result_Ok()
    {
        _isVoid = false;
        _isSynchronous = true;
        _resultType = typeof(string);
        var context = new Context("op-key")
        {
            [Incoming.Key] = "incoming-value"
        };

        var result = _strategy.AsSyncPolicy().Execute(_ => { context[Executing.Key] = "executing-value"; return "dummy"; }, context);

        AssertContext(context);
        result.Should().Be("dummy");
    }

    [Fact]
    public async Task AsAsyncPolicy_Ok()
    {
        _isVoid = true;
        _isSynchronous = false;
        var context = new Context("op-key")
        {
            [Incoming.Key] = "incoming-value"
        };

        await _strategy.AsAsyncPolicy().ExecuteAsync(_ =>
        {
            context[Executing.Key] = "executing-value";
            return Task.CompletedTask;
        },
        context);

        AssertContext(context);
    }

    [Fact]
    public async Task AsAsyncPolicy_Generic_Ok()
    {
        _isVoid = false;
        _isSynchronous = false;
        _resultType = typeof(string);
        var context = new Context("op-key")
        {
            [Incoming.Key] = "incoming-value"
        };

        var result = await _genericStrategy.AsAsyncPolicy().ExecuteAsync(_ =>
        {
            context[Executing.Key] = "executing-value";
            return Task.FromResult("dummy");
        },
        context);
        AssertContext(context);
        result.Should().Be("dummy");
    }

    [Fact]
    public async Task AsAsyncPolicy_Result_Ok()
    {
        _isVoid = false;
        _isSynchronous = false;
        _resultType = typeof(string);
        var context = new Context("op-key")
        {
            [Incoming.Key] = "incoming-value"
        };

        var result = await _strategy.AsAsyncPolicy().ExecuteAsync(_ =>
        {
            context[Executing.Key] = "executing-value";
            return Task.FromResult("dummy");
        },
        context);

        AssertContext(context);
        result.Should().Be("dummy");
    }

    [Fact]
    public void RetryStrategy_AsSyncPolicy_Ok()
    {
        var policy = new ResilienceStrategyBuilder<string>()
            .AddRetry(new RetryStrategyOptions<string>
            {
                ShouldHandle = _ => PredicateResult.True,
                BackoffType = RetryBackoffType.Constant,
                RetryCount = 5,
                BaseDelay = TimeSpan.FromMilliseconds(1)
            })
            .Build()
            .AsSyncPolicy();

        var context = new Context("op-key")
        {
            ["retry"] = 0
        };

        policy.Execute(
            c =>
            {
                c["retry"] = (int)c["retry"] + 1;
                return "dummy";
            },
            context)
            .Should()
            .Be("dummy");

        context["retry"].Should().Be(6);
    }

    private static void AssertContext(Context context)
    {
        context[Outgoing.Key].Should().Be("outgoing-value");
        context[Executing.Key].Should().Be("executing-value");
    }
}
