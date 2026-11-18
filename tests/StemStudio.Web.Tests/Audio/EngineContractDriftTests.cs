using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.JSInterop;
using StemStudio.Web.Audio;

namespace StemStudio.Web.Tests.Audio;

/// <summary>
/// U2: guards the C# wrapper against drift from the binding TypeScript contract
/// <c>src/audio-engine/src/types.ts</c> (method names of <c>AudioEngine</c>, callback names, version).
/// </summary>
public sealed partial class EngineContractDriftTests
{
    private static readonly string TypesTs = File.ReadAllText(RepoFile("src", "audio-engine", "src", "types.ts"));

    // Staged callbacks: in types.ts but not yet [JSInvokable] in AudioEngineInterop. Empty since 012 T030
    // (OnPracticeChanged implemented); a later contract stages new names here until the frontend implements them.
    // 014 Phase 0 part 2 (engine 2.4.0): OnLoopPass, implemented by the 014 frontend lane (AudioEngineInterop.Score.cs).
    // 015 Phase 0 (T004, engine 2.5.0): the five recording callbacks, removed when the frontend implements them (T026/T027).
    // 016 Phase 0 (T004, engine 2.6.0): the two singing callbacks, removed when the frontend implements them (T105/T106).
    private static readonly string[] PendingCallbacks = ["OnSingingUnitScored", "OnSingingStateChanged"];

    [Fact]
    public void EveryInvokedEngineMethod_ExistsInAudioEngineInterface()
    {
        var contractMethods = InterfaceMethods("AudioEngine");

        contractMethods.ShouldNotBeEmpty();
        foreach (var method in EngineMethods.All)
        {
            contractMethods.ShouldContain(method, $"'{method}' is invoked by AudioEngineInterop but missing in types.ts");
        }
    }

    [Fact]
    public void ScoreFollowerMethods_ExistInAudioEngineInterface()
    {
        var contractMethods = InterfaceMethods("AudioEngine");

        AudioEngineInterop.ScoreMethods.ShouldAllBe(m => contractMethods.Contains(m));
    }

    [Fact]
    public void ModuleExport_CreateEngine_IsInContract()
    {
        TypesTs.ShouldContain("export type CreateEngine =");
        EngineMethods.CreateEngine.ShouldBe("createEngine");
        EngineMethods.ModulePath.ShouldBe("./js/audio-engine.js");
    }

    [Fact]
    public void JsInvokableCallbacks_MatchDotNetCallbacksType()
    {
        var match = CallbacksRegex().Match(TypesTs);
        match.Success.ShouldBeTrue();
        var contractCallbacks = QuotedRegex().Matches(match.Groups[1].Value).Select(m => m.Groups[1].Value).Except(PendingCallbacks).Order().ToList();

        var invokables = typeof(AudioEngineInterop).GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Select(m => m.GetCustomAttribute<JSInvokableAttribute>()?.Identifier ?? (m.IsDefined(typeof(JSInvokableAttribute)) ? m.Name : null))
            .OfType<string>().Order().ToList();

        invokables.ShouldBe(contractCallbacks);
        PendingCallbacks.ShouldAllBe(name => match.Groups[1].Value.Contains($"'{name}'", StringComparison.Ordinal) && !invokables.Contains(name));
    }

    [Fact]
    public void PracticeMethods_AreInvoked()
    {
        EngineMethods.All.ShouldContain("setPractice");
        EngineMethods.All.ShouldContain("getPracticeStatus");
    }

    [Fact]
    public void ContractVersion_IsTheSupportedOne()
    {
        TypesTs.ShouldContain($"export type EngineContractVersion = '{EngineMethods.ContractVersion}';");
    }

    private static List<string> InterfaceMethods(string interfaceName)
    {
        var start = TypesTs.IndexOf($"export interface {interfaceName} {{", StringComparison.Ordinal);
        start.ShouldBeGreaterThanOrEqualTo(0);
        var end = TypesTs.IndexOf("\n}", start, StringComparison.Ordinal);
        var body = TypesTs[start..end];
        return MethodRegex().Matches(body).Select(m => m.Groups[1].Value).ToList();
    }

    private static string RepoFile(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "StemStudio.slnx")))
        {
            dir = dir.Parent;
        }

        dir.ShouldNotBeNull("repository root (StemStudio.slnx) not found");
        return Path.Combine([dir.FullName, .. parts]);
    }

    [GeneratedRegex(@"^\s{2}(\w+)\(", RegexOptions.Multiline)]
    private static partial Regex MethodRegex();

    [GeneratedRegex(@"export type DotNetCallbacks =([^;]+);")]
    private static partial Regex CallbacksRegex();

    [GeneratedRegex("'(\\w+)'")]
    private static partial Regex QuotedRegex();
}
