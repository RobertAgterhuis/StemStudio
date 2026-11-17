namespace StemStudio.Web.Library;

/// <summary>
/// Composition hook of the 022 UI (library folders, live search, library info), called from <c>Program.cs</c> so no other
/// lane edits it.
/// </summary>
public static class LibraryUiRegistration
{
    /// <summary>Registers the 022 UI services: the per-device view choices of the library.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddLibraryUi(this IServiceCollection services) => services.AddScoped<LibraryViewPreferences>();
}
