namespace StemStudio.Web.Api;

/// <summary>
/// A readable problem of a form, optionally tied to a field (ui-test-ids 1.8.0/1.9.0 <c>*-error</c> with <c>data-field</c>):
/// the first field of a validation problem, the form's conflict field for a 409 (a taken style name), or no field.
/// </summary>
/// <param name="Field">The camelCase field name, e.g. <c>description</c>; null for a problem of the whole form.</param>
/// <param name="Message">The text the user can act on.</param>
public sealed record FieldProblem(string? Field, string Message)
{
    /// <summary>Maps an API error.</summary>
    /// <param name="error">The error.</param>
    /// <param name="conflictField">The field a 409 without field errors belongs to (e.g. <c>name</c> in the style editor), or null.</param>
    /// <returns>The problem.</returns>
    public static FieldProblem From(ApiError error, string? conflictField = null) =>
        new(error.FieldErrors.Keys.FirstOrDefault() ?? (error.IsConflict ? conflictField : null), error.UserMessage);
}
