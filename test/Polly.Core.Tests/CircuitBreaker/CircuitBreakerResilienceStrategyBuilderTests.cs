using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Time.Testing;
using Polly.CircuitBreaker;

namespace Polly.Core.Tests.CircuitBreaker;

public class CircuitBreakerResilienceStrategyBuilderTests
{
    public static TheoryData<Action<ResilienceStrategyBuilder>> ConfigureData = new()
    {
        builder => builder.AddAdvancedCircuitBreaker(new AdvancedCircuitBreakerStrategyOptions
        {
            ShouldHandle = _ => PredicateResult.True
        }),
        builder => builder.AddSimpleCircuitBreaker(new SimpleCircuitBreakerStrategyOptions
        {
            ShouldHandle = _ => PredicateResult.True
        }),
    };

    public static TheoryData<Action<ResilienceStrategyBuilder<int>>> ConfigureDataGeneric = new()
    {
        builder => builder.AddAdvancedCircuitBreaker(new AdvancedCircuitBreakerStrategyOptions<int>
        {
            ShouldHandle = _ => PredicateResult.True
        }),
        builder => builder.AddSimpleCircuitBreaker(new SimpleCircuitBreakerStrategyOptions<int>
        {
            ShouldHandle = _ => PredicateResult.True
        }),
    };

    [MemberData(nameof(ConfigureData))]
    [Theory]
    public void AddCircuitBreaker_Configure(Action<ResilienceStrategyBuilder> builderAction)
    {
        var builder = new ResilienceStrategyBuilder();

        builderAction(builder);

        var strategy = builder.Build();

        strategy.Should().BeOfType<CircuitBreakerResilienceStrategy<object>>();
    }

    [MemberData(nameof(ConfigureDataGeneric))]
    [Theory]
    public void AddCircuitBreaker_Generic_Configure(Action<ResilienceStrategyBuilder<int>> builderAction)
    {
        var builder = new ResilienceStrategyBuilder<int>();

        builderAction(builder);

        var strategy = builder.Build().Strategy;

        strategy.Should().BeOfType<CircuitBreakerResilienceStrategy<int>>();
    }

    [Fact]
    public void AddCircuitBreaker_Validation()
    {
        new ResilienceStrategyBuilder<int>()
            .Invoking(b => b.AddSimpleCircuitBreaker(new SimpleCircuitBreakerStrategyOptions<int> { BreakDuration = TimeSpan.MinValue }))
            .Should()
            .Throw<ValidationException>();

        new ResilienceStrategyBuilder()
            .Invoking(b => b.AddSimpleCircuitBreaker(new SimpleCircuitBreakerStrategyOptions { BreakDuration = TimeSpan.MinValue }))
            .Should()
            .Throw<ValidationException>();
    }

    [Fact]
    public void AddAdvancedCircuitBreaker_Validation()
    {
        new ResilienceStrategyBuilder<int>()
            .Invoking(b => b.AddAdvancedCircuitBreaker(new AdvancedCircuitBreakerStrategyOptions<int> { BreakDuration = TimeSpan.MinValue }))
            .Should()
            .Throw<ValidationException>();

        new ResilienceStrategyBuilder()
            .Invoking(b => b.AddAdvancedCircuitBreaker(new AdvancedCircuitBreakerStrategyOptions { BreakDuration = TimeSpan.MinValue }))
            .Should()
            .Throw<ValidationException>();
    }

    [Fact]
    public void AddCircuitBreaker_IntegrationTest()
    {
        int opened = 0;
        int closed = 0;
        int halfOpened = 0;

        var options = new SimpleCircuitBreakerStrategyOptions
        {
            FailureThreshold = 5,
            BreakDuration = TimeSpan.FromMilliseconds(500),
            ShouldHandle = args => new ValueTask<bool>(args.Result is -1),
            OnOpened = _ => { opened++; return default; },
            OnClosed = _ => { closed++; return default; },
            OnHalfOpened = (_) => { halfOpened++; return default; }
        };

        var timeProvider = new FakeTimeProvider();
        var strategy = new ResilienceStrategyBuilder { TimeProvider = timeProvider }.AddSimpleCircuitBreaker(options).Build();

        for (int i = 0; i < options.FailureThreshold; i++)
        {
            strategy.Execute(_ => -1);
        }

        // Circuit opened
        opened.Should().Be(1);
        halfOpened.Should().Be(0);
        closed.Should().Be(0);
        Assert.Throws<BrokenCircuitException<object>>(() => strategy.Execute(_ => 0));

        // Circuit Half Opened
        timeProvider.Advance(options.BreakDuration);
        strategy.Execute(_ => -1);
        Assert.Throws<BrokenCircuitException<object>>(() => strategy.Execute(_ => 0));
        opened.Should().Be(2);
        halfOpened.Should().Be(1);
        closed.Should().Be(0);

        // Now close it
        timeProvider.Advance(options.BreakDuration);
        strategy.Execute(_ => 0);
        opened.Should().Be(2);
        halfOpened.Should().Be(2);
        closed.Should().Be(1);
    }

    [Fact]
    public void AddAdvancedCircuitBreaker_IntegrationTest()
    {
        int opened = 0;
        int closed = 0;
        int halfOpened = 0;

        var options = new AdvancedCircuitBreakerStrategyOptions
        {
            FailureThreshold = 0.5,
            MinimumThroughput = 10,
            SamplingDuration = TimeSpan.FromSeconds(10),
            BreakDuration = TimeSpan.FromSeconds(1),
            ShouldHandle = args => new ValueTask<bool>(args.Result is -1),
            OnOpened = _ => { opened++; return default; },
            OnClosed = _ => { closed++; return default; },
            OnHalfOpened = (_) => { halfOpened++; return default; }
        };

        var timeProvider = new FakeTimeProvider();
        var strategy = new ResilienceStrategyBuilder { TimeProvider = timeProvider }.AddAdvancedCircuitBreaker(options).Build();

        for (int i = 0; i < 10; i++)
        {
            strategy.Execute(_ => -1);
        }

        // Circuit opened
        opened.Should().Be(1);
        halfOpened.Should().Be(0);
        closed.Should().Be(0);
        Assert.Throws<BrokenCircuitException<object>>(() => strategy.Execute(_ => 0));

        // Circuit Half Opened
        timeProvider.Advance(options.BreakDuration);
        strategy.Execute(_ => -1);
        Assert.Throws<BrokenCircuitException<object>>(() => strategy.Execute(_ => 0));
        opened.Should().Be(2);
        halfOpened.Should().Be(1);
        closed.Should().Be(0);

        // Now close it
        timeProvider.Advance(options.BreakDuration);
        strategy.Execute(_ => 0);
        opened.Should().Be(2);
        halfOpened.Should().Be(2);
        closed.Should().Be(1);
    }

    [Fact]
    public async Task AddCircuitBreakers_WithIsolatedManualControl_ShouldBeIsolated()
    {
        var manualControl = new CircuitBreakerManualControl();
        await manualControl.IsolateAsync();

        var strategy1 = new ResilienceStrategyBuilder()
            .AddAdvancedCircuitBreaker(new() { ManualControl = manualControl })
            .Build();

        var strategy2 = new ResilienceStrategyBuilder()
            .AddAdvancedCircuitBreaker(new() { ManualControl = manualControl })
            .Build();

        strategy1.Invoking(s => s.Execute(() => { })).Should().Throw<IsolatedCircuitException>();
        strategy2.Invoking(s => s.Execute(() => { })).Should().Throw<IsolatedCircuitException>();

        await manualControl.CloseAsync();

        strategy1.Execute(() => { });
        strategy2.Execute(() => { });
    }
}
