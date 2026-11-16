namespace StemStudio.AgentEval.Tests;

/// <summary>Invokes the harness's top-level <c>Main</c> (async, returns the exit code).</summary>
internal static class EntryPoint
{
    public static async Task<int> RunAsync(params string[] args)
    {
        var result = typeof(EvalOptions).Assembly.EntryPoint!.Invoke(null, [args]);
        return result is Task<int> task ? await task : (int)result!;
    }
}
