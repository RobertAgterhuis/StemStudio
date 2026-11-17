using System.Diagnostics;

namespace StemStudio.Api.Tests.TestSupport;

/// <summary>
/// Polls a condition for work done by a hosted service on another thread. The budget is a wall-clock deadline, not a number
/// of attempts: the old 200 x 25 ms (about 5 s) ran out under full parallel load (flake round 022/023), while a met condition
/// still returns at the first poll that sees it.
/// </summary>
internal static class Eventually
{
    /// <summary>How long a condition may take before the wait fails.</summary>
    public static TimeSpan Deadline { get; } = TimeSpan.FromSeconds(30);

    public static async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        var watch = Stopwatch.StartNew();
        while (!await condition())
        {
            if (watch.Elapsed > Deadline)
            {
                throw new TimeoutException($"Condition not met within {Deadline.TotalSeconds:0} s.");
            }

            await Task.Delay(25, TestContext.Current.CancellationToken);
        }
    }
}
