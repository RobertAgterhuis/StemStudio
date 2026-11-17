using StemStudio.Contracts.LibraryCare;

namespace StemStudio.Api;

/// <summary>Limits of library care (feature 013, section <c>StemStudio:LibraryCare</c>, data-model 1).</summary>
public sealed class LibraryCareOptions
{
    /// <summary>Margin above the estimate before a backup starts (FR-007).</summary>
    public long MinFreeMarginBytes { get; set; } = LibraryCareLimits.DefaultMinFreeMarginBytes;

    /// <summary>A running backup or restore stops below this free space.</summary>
    public long StopBelowFreeBytes { get; set; } = LibraryCareLimits.DefaultStopBelowFreeBytes;

    /// <summary>Cache of the storage overview in seconds.</summary>
    public int StorageCacheSeconds { get; set; } = LibraryCareLimits.DefaultStorageCacheSeconds;

    /// <summary>Validity of a clean-up plan in minutes (FR-023).</summary>
    public int CleanupPlanMinutes { get; set; } = LibraryCareLimits.DefaultCleanupPlanMinutes;

    /// <summary>File copy buffer in bytes.</summary>
    public int CopyBufferBytes { get; set; } = LibraryCareLimits.DefaultCopyBufferBytes;
}
