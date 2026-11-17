using StemStudio.Contracts.LibraryCare;

namespace StemStudio.Web.LibraryCare;

/// <summary>A backup profile of the backup dialog (ui-test-ids 1.12.0 <c>backup-profile-*</c>).</summary>
public enum BackupProfile
{
    /// <summary>Every optional category.</summary>
    Complete,

    /// <summary>Everything except the detail stems (regenerable with "Split further").</summary>
    Compact,

    /// <summary>Any other choice of options (Advanced and Expert).</summary>
    Custom,
}

/// <summary>
/// The state of the backup dialog: the optional categories and the profile they amount to. Choosing a profile sets the
/// options; changing an option selects the matching profile, or <see cref="BackupProfile.Custom"/>.
/// </summary>
public sealed class BackupForm
{
    /// <summary>Every optional category, in the order of the dialog.</summary>
    public static IReadOnlyList<BackupCategoryDto> OptionalCategories { get; } =
    [
        BackupCategoryDto.DetailStems, BackupCategoryDto.Exports, BackupCategoryDto.Scores,
        BackupCategoryDto.Generations, BackupCategoryDto.Recordings, BackupCategoryDto.VoiceProfiles,
    ];

    /// <summary>The options sent to the estimate and the backup.</summary>
    public BackupOptionsDto Options { get; private set; } = new();

    /// <summary>The profile the options amount to.</summary>
    public BackupProfile Profile => Options == new BackupOptionsDto() ? BackupProfile.Complete
        : Options == new BackupOptionsDto { DetailStems = false } ? BackupProfile.Compact
        : BackupProfile.Custom;

    /// <summary>The checkbox label of an optional category.</summary>
    /// <param name="category">The category.</param>
    /// <returns>For example "Detail stems (regenerable)".</returns>
    public static string Label(BackupCategoryDto category) => category switch
    {
        BackupCategoryDto.DetailStems => "Detail stems (can be split again)",
        BackupCategoryDto.Recordings => "Recordings (play-along takes)",
        _ => CareText.Category(category),
    };

    /// <summary>Selects a profile; <see cref="BackupProfile.Custom"/> keeps the options.</summary>
    /// <param name="profile">The profile.</param>
    public void Choose(BackupProfile profile) => Options = profile switch
    {
        BackupProfile.Complete => new BackupOptionsDto(),
        BackupProfile.Compact => new BackupOptionsDto { DetailStems = false },
        _ => Options,
    };

    /// <summary>Whether an optional category is included.</summary>
    /// <param name="category">The category.</param>
    /// <returns>True when included.</returns>
    public bool Includes(BackupCategoryDto category) => category switch
    {
        BackupCategoryDto.DetailStems => Options.DetailStems,
        BackupCategoryDto.Exports => Options.Exports,
        BackupCategoryDto.Scores => Options.Scores,
        BackupCategoryDto.Generations => Options.Generations,
        BackupCategoryDto.Recordings => Options.Recordings,
        BackupCategoryDto.VoiceProfiles => Options.VoiceProfiles,
        _ => true,
    };

    /// <summary>Includes or leaves out an optional category.</summary>
    /// <param name="category">The category.</param>
    /// <param name="included">The checkbox value.</param>
    public void Set(BackupCategoryDto category, bool included) => Options = category switch
    {
        BackupCategoryDto.DetailStems => Options with { DetailStems = included },
        BackupCategoryDto.Exports => Options with { Exports = included },
        BackupCategoryDto.Scores => Options with { Scores = included },
        BackupCategoryDto.Generations => Options with { Generations = included },
        BackupCategoryDto.Recordings => Options with { Recordings = included },
        BackupCategoryDto.VoiceProfiles => Options with { VoiceProfiles = included },
        _ => Options,
    };

    /// <summary>True when voice profiles are included and the estimate counts at least one voice file.</summary>
    /// <param name="estimate">The estimate, or null while loading.</param>
    /// <returns>Whether the voice notice is shown.</returns>
    public bool ShowsVoiceNotice(BackupEstimateDto? estimate) =>
        Options.VoiceProfiles && estimate?.Categories.Any(c => c.Category == BackupCategoryDto.VoiceProfiles && c.Files > 0) == true;
}
