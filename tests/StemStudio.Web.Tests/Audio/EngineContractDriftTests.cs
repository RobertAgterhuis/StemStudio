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
    // (OnPracticeChanged implemented); 015 Phase 0 (T004) stages the five recording callbacks of contract 2.5.0 until the
    // frontend implements them (T026/T027, RecordingInterop routing).
    private static readonly string[] PendingCallbacks =
        ["OnRecordingStateChanged", "OnRecordingProgress", "OnTakesCaptured", "OnInputLost", "OnInputDevicesChanged"];

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
