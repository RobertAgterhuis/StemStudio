using System.Text.Json;
using System.Text.Json.Nodes;
using NSubstitute;
using StemStudio.Agent.Ports;
using StemStudio.Agent.Tests.Fixtures;
using StemStudio.Agent.Tools;
using StemStudio.Contracts.Agent;

namespace StemStudio.Agent.Tests.Tools.Providers;

/// <summary>Helpers for provider tests: catalogue conformance and handler invocation with validated arguments.</summary>
public static class ProviderTestKit
{
    public static IStemStudioActions Actions() => Substitute.For<IStemStudioActions>();

    public static AgentToolDescriptor Tool(IAgentToolProvider provider, string name) => provider.GetTools().Single(tool => tool.Name == name);

    /// <summary>Asserts that the provider's tools are exactly the catalogue rows of the feature.</summary>
    public static void MatchesCatalogue(IAgentToolProvider provider, string feature)
    {
        var tools = provider.GetTools().ToList();
        var expected = Catalogue.Of(feature).OrderBy(entry => entry.Name, StringComparer.Ordinal).ToList();
        var actual = tools.Select(tool => new CatalogueEntry(tool.Name, tool.Feature, tool.Kind, tool.Risk, tool.UndoKind, tool.Group, tool.Availability))
            .OrderBy(entry => entry.Name, StringComparer.Ordinal).ToList();
        actual.ShouldBe(expected);
        foreach (var tool in tools)
        {
            tool.Description.Length.ShouldBeGreaterThan(20, tool.Name);
            tool.Title.ShouldNotBeNullOrWhiteSpace();
            tool.Schema.ToJsonSchema().GetProperty("additionalProperties").GetBoolean().ShouldBeFalse(tool.Name);
            (tool.Handler is not null).ShouldBe(tool.Kind == ToolKindDto.Server, tool.Name);
        }
    }

    /// <summary>Validates the JSON against the tool's schema (must be valid) and runs the handler.</summary>
    public static Task<ToolResult> InvokeAsync(AgentToolDescriptor tool, string json, IStemStudioActions actions, UiRequester? ui = null) =>
        RunAsync(tool, Invocation(tool, json, actions, ui));

    /// <summary>Runs the handler with an invocation built by <see cref="Invocation"/> (e.g. <c>with { Backend = ... }</c>).</summary>
    public static async Task<ToolResult> RunAsync(AgentToolDescriptor tool, ToolInvocation invocation) =>
        await tool.Handler!(invocation, TestContext.Current.CancellationToken);

    public static ToolInvocation Invocation(AgentToolDescriptor tool, string json, IStemStudioActions actions, UiRequester? ui = null)
    {
        var validation = ToolArgumentValidator.Validate(tool.Schema, json);
        validation.IsValid.ShouldBeTrue(validation.Error);
        return new ToolInvocation { Arguments = validation.Arguments, Actions = actions, Context = new PageContextDto { Page = "tracks" }, Backend = AgentBackendDto.Ollama, Ui = ui };
    }

    public static bool Valid(AgentToolDescriptor tool, string json) => ToolArgumentValidator.Validate(tool.Schema, json).IsValid;

    public static string Json(JsonNode node) => node.ToJsonString();

    public static JsonElement Element(string json) => JsonDocument.Parse(json).RootElement.Clone();
}
