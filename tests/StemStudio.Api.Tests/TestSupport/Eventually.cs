namespace StemStudio.Api.Tests.TestSupport;

/// <summary>Polls a condition for work done by the hosted service on another thread.</summary>
internal static class Eventually
{
    public static async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        for (var attempt = 0; attempt < 200; attempt++)
        {
            if (await condition())
            {
                return;
            }

            await Task.Delay(25, TestContext.Current.CancellationToken);
        }

        throw new TimeoutException("Condition not met.");
    }
}
