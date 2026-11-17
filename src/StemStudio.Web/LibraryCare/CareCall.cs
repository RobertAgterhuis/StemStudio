using StemStudio.Web.Api;

namespace StemStudio.Web.LibraryCare;

/// <summary>Runs a library care call and keeps the whole <see cref="ApiError"/> (the dialogs show its code in <c>data-code</c>).</summary>
public static class CareCall
{
    /// <summary>Runs a call that returns a value.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="call">The call.</param>
    /// <returns>The value and null, or the default value and the error.</returns>
    public static async Task<(T? Value, ApiError? Error)> TryAsync<T>(Func<Task<T>> call)
    {
        try
        {
            return (await call(), null);
        }
        catch (ApiException ex)
        {
            return (default, ex.Error);
        }
    }
}
