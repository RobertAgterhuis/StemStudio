namespace StemStudio.Web.Library;

/// <summary>
/// Composition hook of the 022 UI (library folders, live search, library info). Skeleton (architect, 022+023 round
/// start): the frontend-022 lane fills it so no other lane edits <c>Program.cs</c>.
/// </summary>
public static class LibraryUiRegistration
{
    /// <summary>Registers the 022 UI services.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddLibraryUi(this IServiceCollection services) => services;
}
