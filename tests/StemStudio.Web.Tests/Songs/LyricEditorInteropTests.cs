using Bunit;
using Microsoft.AspNetCore.Components;
using StemStudio.Web.Components.Lyrics.Assist;

namespace StemStudio.Web.Tests.Songs;

/// <summary>The lyrics editor interop calls <c>lyric-editor.js</c> with the documented function names.</summary>
public sealed class LyricEditorInteropTests : BunitContext
{
    [Fact]
    public async Task Calls_TheModuleFunctions()
    {
        var module = JSInterop.SetupModule(LyricEditorInterop.ModulePath);
        module.Setup<int[]>("getSelection", _ => true).SetResult([3, 7]);
        var listener = module.SetupModule("bindKeys", _ => true);
        listener.SetupVoid("dispose").SetVoidResult();
        module.SetupVoid("select", _ => true).SetVoidResult();
        module.SetupVoid("focusById", _ => true).SetVoidResult();
        await using var interop = new LyricEditorInterop(JSInterop.JSRuntime);

        (await interop.GetSelectionAsync(default)).ShouldBe((3, 7));
        await interop.SelectAsync(default, 1, 2);
        await interop.FocusAsync("x");
        await (await interop.BindKeysAsync(new ElementReference("editor")))!.DisposeAsync();

        listener.VerifyInvoke("dispose");

        module.Invocations.Select(i => i.Identifier).ShouldContain("bindKeys");
        module.VerifyInvoke("select").Arguments.Skip(1).ShouldBe([1, 2]);
        module.VerifyInvoke("focusById").Arguments.ShouldBe(["x"]);
    }

    [Fact]
    public async Task BindKeys_BeforeTheElementIsRendered_CallsNothing_AndReturnsNull()
    {
        // Blazor Server runs OnAfterRender after the client acknowledged the batch; by then the sheet may have loaded while
        // the editor element was not rendered yet. An uncaptured reference reached the module as a plain object and
        // `root.addEventListener` ended the circuit (023 E2E, r22).
        await using var interop = new LyricEditorInterop(JSInterop.JSRuntime);

        (await interop.BindKeysAsync(default)).ShouldBeNull();

        JSInterop.Invocations.ShouldBeEmpty();
    }

    [Fact]
    public async Task ShortSelection_ReadsAsZero()
    {
        var module = JSInterop.SetupModule(LyricEditorInterop.ModulePath);
        module.Setup<int[]>("getSelection", _ => true).SetResult([]);
        await using var interop = new LyricEditorInterop(JSInterop.JSRuntime);

        (await interop.GetSelectionAsync(default(ElementReference))).ShouldBe((0, 0));
    }
}
