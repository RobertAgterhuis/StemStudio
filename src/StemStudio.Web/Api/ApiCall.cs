namespace StemStudio.Web.Api;

/// <summary>Runs backend calls of a component and turns an <see cref="ApiException"/> into the readable message it shows.</summary>
public static class ApiCall
{
    /// <summary>Runs an action.</summary>
    /// <param name="action">The calls.</param>
    /// <returns>Null on success, else the user message of the API error.</returns>
    public static async Task<string?> RunAsync(Func<Task> action)
    {
        try
        {
            await action();
            return null;
        }
        catch (ApiException ex)
        {
            return ex.Error.UserMessage;
        }
    }

    /// <summary>Runs a call that returns a value.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="call">The call.</param>
    /// <returns>The value and null, or the default value and the user message of the API error.</returns>
    public static async Task<(T? Value, string? Error)> TryAsync<T>(Func<Task<T>> call)
    {
        T? value = default;
        var error = await RunAsync(async () => value = await call());
        return (value, error);
    }
}
