using System.ComponentModel.DataAnnotations;
using Polly.Hedging;
using Polly.Utils;

namespace Polly.Core.Tests.Hedging;

public class HedgingStrategyOptionsTests
{
    [Fact]
    public async Task Ctor_EnsureDefaults()
    {
        var options = new HedgingStrategyOptions<int>();

        options.StrategyType.Should().Be("Hedging");
        options.ShouldHandle.Should().NotBeNull();
        options.HedgingActionGenerator.Should().NotBeNull();
        options.HedgingDelay.Should().Be(TimeSpan.FromSeconds(2));
        options.MaxHedgedAttempts.Should().Be(2);
        options.OnHedging.Should().BeNull();

        var action = options.HedgingActionGenerator(new HedgingActionGeneratorArguments<int>(ResilienceContext.Get(), ResilienceContext.Get(), 1, c => 99.AsOutcomeAsync()))!;
        action.Should().NotBeNull();
        (await action()).Result.Should().Be(99);
    }

    [Fact]
    public async Task ShouldHandle_EnsureDefaults()
    {
        var options = new HedgingStrategyOptions<int>();
        var args = new HandleHedgingArguments();
        var context = ResilienceContext.Get();

        (await options.ShouldHandle(new(context, new Outcome<int>(0), args))).Should().Be(false);
        (await options.ShouldHandle(new(context, new Outcome<int>(new OperationCanceledException()), args))).Should().Be(false);
        (await options.ShouldHandle(new(context, new Outcome<int>(new InvalidOperationException()), args))).Should().Be(true);
    }

    [Fact]
    public void Validation()
    {
        var options = new HedgingStrategyOptions<int>
        {
            HedgingDelayGenerator = null!,
            ShouldHandle = null!,
            MaxHedgedAttempts = -1,
            OnHedging = null!,
            HedgingActionGenerator = null!
        };

        options
            .Invoking(o => ValidationHelper.ValidateObject(o, "Invalid."))
            .Should()
            .Throw<ValidationException>()
            .WithMessage("""
            Invalid.

            Validation Errors:
            The field MaxHedgedAttempts must be between 2 and 10.
            The ShouldHandle field is required.
            The HedgingActionGenerator field is required.
            """);
    }
}