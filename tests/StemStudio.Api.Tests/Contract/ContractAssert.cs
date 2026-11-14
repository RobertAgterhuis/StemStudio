using System.Globalization;
using System.Text.Json.Nodes;

namespace StemStudio.Api.Tests.Contract;

/// <summary>Checks a live response against the declared status, headers and body schema of an operation.</summary>
internal static class ContractAssert
{
    public static async Task MatchesAsync(HttpResponseMessage response, string operationId)
    {
        var contract = OpenApiContract.Backend;
        var status = (int)response.StatusCode;
        var declared = contract.Response(operationId, status);
        declared.ShouldNotBeNull($"{operationId} does not declare status {status}");
        CheckHeaders(response, declared, operationId);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        if (declared["content"] is not JsonObject content)
        {
            body.ShouldBeEmpty($"{operationId} {status} must not have a body");
            return;
        }

        var mediaType = response.Content.Headers.ContentType?.MediaType ?? "(none)";
        var media = content[mediaType];
        media.ShouldNotBeNull($"{operationId} {status} does not declare media type {mediaType}");
        CheckBody(contract, media["schema"]!, body, $"{operationId} {status}");
    }

    private static void CheckBody(OpenApiContract contract, JsonNode schema, string body, string context)
    {
        if (contract.Resolve(schema)["contentMediaType"] is not null)
        {
            body.ShouldNotBeEmpty(context);
            return;
        }

        var errors = SchemaValidator.Validate(contract, schema, JsonNode.Parse(body));
        errors.ShouldBeEmpty($"{context}: {string.Join("; ", errors)} in {body}");
    }

    private static void CheckHeaders(HttpResponseMessage response, JsonObject declared, string operationId)
    {
        foreach (var (name, _) in declared["headers"]?.AsObject() ?? [])
        {
            var present = response.Headers.TryGetValues(name, out _) || response.Content.Headers.TryGetValues(name, out _);
            present.ShouldBeTrue(string.Create(CultureInfo.InvariantCulture, $"{operationId} {(int)response.StatusCode} misses header {name}"));
        }
    }
}
