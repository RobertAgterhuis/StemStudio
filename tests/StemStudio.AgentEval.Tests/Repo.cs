namespace StemStudio.AgentEval.Tests;

/// <summary>Paths of the repository the tests run in (the eval set and the 009 presets).</summary>
internal static class Repo
{
    public static string Root { get; } = FindRoot();

    public static string EvalSetPath => Path.Combine(Root, "tools", "agent-eval", "eval-set.json");

    public static string PresetsPath => Path.Combine(Root, "src", "StemStudio.Domain", "Styles", "presets.v1.json");

    public static EvalSet EvalSet { get; } = EvalSet.Parse(File.ReadAllText(EvalSetPath));

    public static string Presets { get; } = File.ReadAllText(PresetsPath);

    public static EvalCase Case(string id) => EvalSet.Cases.Single(item => item.Id == id);

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "StemStudio.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("StemStudio.slnx not found above the test output.");
    }
}
