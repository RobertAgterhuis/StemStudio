using StemStudio.Agent.Tests.Fixtures;
using StemStudio.Agent.Tools;
using StemStudio.Contracts.Agent;

namespace StemStudio.Agent.Tests.Tools;

/// <summary>T012: registry composition and the per-request selection of tool catalogue 1.0.0 (FR-006, cap 32).</summary>
public sealed class RegistryTests
{
    private static readonly string[] Core =
    [
        "set_plan", "wait_for_job", "search_library", "get_track", "import_from_url", "delete_track", "list_mixes", "get_mix",
        "open_track", "open_mixer", "get_processing_device", "search_help", "open_help", "navigate",
    ];

    private static readonly string[] Mixer =
    [
        "mixer_get_state", "mixer_set_channel", "mixer_set_master", "mixer_reset", "transport", "set_loop", "set_metronome",
        "show_karaoke", "save_mix_as", "update_mix",
    ];

    private static readonly string[] Tracks =
    [
        "retry_track", "duplicate_mix", "rename_mix", "delete_mix", "start_detail_separation", "cancel_detail_separation",
        "get_artist_info", "refresh_artwork", "get_lyrics_status", "fetch_lyrics", "align_lyrics", "export_audio", "transcribe_stem",
    ];

    private static readonly string[] Library =
    [
        "export_stem_pack", "list_exports", "delete_export", "list_scores", "delete_score",
        "browse_library_folder", "search_library_items", "open_library_folder", "set_library_info",
    ];

    private static readonly string[] LyricAssist =
    [
        "check_lyrics", "suggest_lyric_lines", "list_lyric_suggestions", "apply_lyric_suggestions", "mark_lyrics_intentional",
        "find_rhymes", "count_syllables", "open_lyrics_editor",
    ];

    private static readonly string[] LyricsExtras =
        ["list_lyrics_sheets", "save_lyrics_sheet", "generate_song", "list_styles", "get_generation_status", "list_generations"];

    private static readonly string[] Create =
    [
        "get_generation_status", "list_styles", "create_style", "delete_style", "restyle_track", "list_generations",
        "cancel_generation", "retry_generation", "import_take", "delete_generation", "list_lyrics_sheets", "save_lyrics_sheet",
        "generate_song", "list_voice_profiles", "convert_take_voice",
    ];

    private static readonly string[] DefaultExtras = ["list_styles", "restyle_track", "generate_song", "get_generation_status", "list_generations"];

    private static readonly string[] MixerExtras =
        ["start_detail_separation", "export_audio", "transcribe_stem", "get_lyrics_status", "fetch_lyrics", "align_lyrics", "list_styles", "restyle_track"];

    private static readonly string[] LibraryExtras =
    [
        "export_audio", "transcribe_stem", "get_lyrics_status",
        "get_generation_status", "list_generations", "import_take", "cancel_generation", "retry_generation", "delete_generation",
    ];

    private static AgentToolRegistry Registry() => new(FixedToolProvider.PerFeature());

    private static ToolContext Ready(string page) => new()
    {
        Page = page,
        MixerOpen = page == "mixer",
        Generation = ServiceAvailability.Available,
        Voice = ServiceAvailability.Available,
        LyricModel = ServiceAvailability.Available,
    };

    private static string[] Names(IEnumerable<AgentToolDescriptor> tools) => [.. tools.Select(tool => tool.Name)];

    private static string[] Sorted(params IEnumerable<string>[] parts) =>
        [.. parts.SelectMany(part => part).Distinct().Order(StringComparer.Ordinal)];

    [Fact]
    public void Providers_ContributeEveryCatalogueTool_SortedByName()
    {
        var registry = Registry();

        registry.All.Count.ShouldBe(69);
        Names(registry.All).ShouldBe(Sorted(Catalogue.All.Select(entry => entry.Name)));
        registry.Find("delete_track")!.Risk.ShouldBe(ToolRiskDto.Destructive);
        registry.Find("no_such_tool").ShouldBeNull();
    }

    [Fact]
    public void DuplicateNames_AreRejectedAtStartUp()
    {
        var first = new FixedToolProvider([Catalogue.Descriptor(Catalogue.All[0])]);
        var second = new FixedToolProvider([Catalogue.Descriptor(Catalogue.All[0])]);

        var error = Should.Throw<InvalidOperationException>(() => new AgentToolRegistry([first, second]));

        error.Message.ShouldContain("set_plan");
    }

    [Fact]
    public void APageAboveTheCap_IsRejectedAtStartUp()
    {
        var tools = Enumerable.Range(0, 33).Select(i => Catalogue.Descriptor(Catalogue.All[0]) with { Name = $"tool_{i:00}" });

        var error = Should.Throw<InvalidOperationException>(() => new AgentToolRegistry([new FixedToolProvider(tools)]));

        error.Message.ShouldContain("33");
    }

    [Fact]
    public void Registry_RejectsNullProviders()
    {
        Should.Throw<ArgumentNullException>(() => new AgentToolRegistry(null!));
    }

    [Theory]
    [InlineData("011", 2)]
    [InlineData("001", 21)]
    [InlineData("002", 2)]
    [InlineData("003", 1)]
    [InlineData("004", 3)]
    [InlineData("005", 2)]
    [InlineData("006", 4)]
    [InlineData("007", 4)]
    [InlineData("008", 3)]
    [InlineData("009", 10)]
    [InlineData("010", 5)]
    [InlineData("022", 4)]
    [InlineData("023", 8)]
    public void CatalogueCounts_PerFeature_MatchTheContract(string feature, int tools)
    {
        Registry().All.Count(tool => tool.Feature == feature).ShouldBe(tools);
    }

    [Fact]
    public void GroupsOfTheCatalogue_HaveTheContractSizes()
    {
        var registry = Registry();

        Names(registry.All.Where(tool => tool.Group == ToolGroup.Core)).ShouldBe(Sorted(Core), ignoreOrder: true);
        Names(registry.All.Where(tool => tool.Group == ToolGroup.Mixer)).ShouldBe(Sorted(Mixer), ignoreOrder: true);
        Names(registry.All.Where(tool => tool.Group == ToolGroup.Tracks)).ShouldBe(Sorted(Tracks), ignoreOrder: true);
        Names(registry.All.Where(tool => tool.Group == ToolGroup.Library)).ShouldBe(Sorted(Library), ignoreOrder: true);
        Names(registry.All.Where(tool => tool.Group == ToolGroup.Create)).ShouldBe(Sorted(Create), ignoreOrder: true);
        Names(registry.All.Where(tool => tool.Group == ToolGroup.LyricAssist)).ShouldBe(Sorted(LyricAssist), ignoreOrder: true);
        registry.All.Count(tool => tool.Risk is ToolRiskDto.Heavy or ToolRiskDto.Destructive).ShouldBe(13);
    }

    [Theory]
    [InlineData("import")]
    [InlineData("tracks")]
    [InlineData("track")]
    [InlineData("mixes")]
    [InlineData("settings")]
    [InlineData("help")]
    [InlineData("something-else")]
    public void DefaultPages_OfferCoreTracksAndTheGenerationEntryPoints(string page)
    {
        var tools = Registry().For(Ready(page));

        Names(tools).ShouldBe(Sorted(Core, Tracks, DefaultExtras));
        tools.Count.ShouldBe(32);
    }

    [Fact]
    public void MixerPage_OffersCoreMixerAndTheTrackJobs()
    {
        var tools = Registry().For(Ready("mixer"));

        Names(tools).ShouldBe(Sorted(Core, Mixer, MixerExtras));
        tools.Count.ShouldBe(32);
    }

    [Fact]
    public void LibraryPage_OffersCoreLibraryAndTheAssetTools()
    {
        var tools = Registry().For(Ready("library"));

        Names(tools).ShouldBe(Sorted(Core, Library, LibraryExtras));
        tools.Count.ShouldBe(32);
    }

    [Theory]
    [InlineData("styles")]
    [InlineData("generations")]
    [InlineData("songs")]
    [InlineData("voices")]
    public void CreatePages_OfferCoreAndCreate(string page)
    {
        var tools = Registry().For(Ready(page));

        Names(tools).ShouldBe(Sorted(Core, Create));
        tools.Count.ShouldBe(29);
    }

    [Theory]
    [InlineData("lyrics")]
    [InlineData(" Lyrics ")]
    public void LyricsPage_OffersCoreLyricAssistAndTheSongTools(string page)
    {
        var tools = Registry().For(Ready(page.Trim().ToLowerInvariant()));

        Names(tools).ShouldBe(Sorted(Core, LyricAssist, LyricsExtras));
        tools.Count.ShouldBe(28);
    }

    [Fact]
    public void LyricModelNotReady_HidesOnlySuggestLyricLines()
    {
        var offers = Registry().Describe(Ready("lyrics") with { LyricModel = ServiceAvailability.Unavailable("Ollama is not running.") });

        offers.Where(offer => !offer.Available).Select(offer => offer.Descriptor.Name).ShouldBe(["suggest_lyric_lines"]);
        offers.Single(offer => offer.Descriptor.Name == "suggest_lyric_lines").UnavailableReason.ShouldBe("Ollama is not running.");
        Registry().For(Ready("lyrics") with { LyricModel = ServiceAvailability.Unknown }).Select(tool => tool.Name).ShouldNotContain("suggest_lyric_lines");
    }

    [Theory]
    [InlineData("import")]
    [InlineData("mixer")]
    [InlineData("library")]
    [InlineData("styles")]
    [InlineData("tracks")]
    [InlineData("lyrics")]
    public void EveryContext_StaysWithinTheCap(string page)
    {
        Registry().For(Ready(page)).Count.ShouldBeLessThanOrEqualTo(AgentToolRegistry.MaxToolsPerRequest);
        Registry().For(Ready(page) with { Generation = ServiceAvailability.Unavailable("x"), Voice = ServiceAvailability.Unavailable("y") })
            .Count.ShouldBeLessThanOrEqualTo(AgentToolRegistry.MaxToolsPerRequest);
    }

    [Fact]
    public void PageNames_AreMatchedCaseInsensitively()
    {
        Names(Registry().For(Ready("Styles"))).ShouldBe(Names(Registry().For(Ready("styles"))));
    }

    [Fact]
    public void GenerationServiceDown_DropsItsTools_ButKeepsTheStatusTool()
    {
        var context = Ready("styles") with { Generation = ServiceAvailability.Unavailable("The generator is not installed.") };

        var names = Names(Registry().For(context));

        names.ShouldContain("get_generation_status");
        names.ShouldNotContain("restyle_track");
        names.ShouldNotContain("list_lyrics_sheets");
        names.ShouldContain("list_voice_profiles");
        names.Length.ShouldBe(14 + 1 + 2);
    }

    [Fact]
    public void VoiceServiceDown_DropsTheVoiceTools()
    {
        var names = Names(Registry().For(Ready("voices") with { Voice = ServiceAvailability.Unavailable("Voice model missing.") }));

        names.ShouldNotContain("convert_take_voice");
        names.ShouldNotContain("list_voice_profiles");
        names.ShouldContain("generate_song");
    }

    [Fact]
    public void MixerNotOpen_DropsTheMixerTools()
    {
        var names = Names(Registry().For(Ready("mixer") with { MixerOpen = false }));

        names.ShouldNotContain("mixer_set_channel");
        names.ShouldNotContain("save_mix_as");
        names.ShouldContain("open_mixer");
        names.ShouldContain("start_detail_separation");
    }

    [Fact]
    public void Describe_ListsTheSelectionWithUnavailableReasons()
    {
        var context = Ready("import") with { Generation = ServiceAvailability.Unavailable("Generator not reachable.") };

        var offers = Registry().Describe(context);

        offers.Count.ShouldBe(32);
        var restyle = offers.Single(offer => offer.Descriptor.Name == "restyle_track");
        restyle.Available.ShouldBeFalse();
        restyle.UnavailableReason.ShouldBe("Generator not reachable.");
        offers.Single(offer => offer.Descriptor.Name == "get_track").Available.ShouldBeTrue();
        offers.Single(offer => offer.Descriptor.Name == "get_track").UnavailableReason.ShouldBeNull();
    }

    [Fact]
    public void Describe_ExplainsAClosedMixerAndAnUnknownServiceState()
    {
        var context = Ready("mixer") with { MixerOpen = false, Generation = ServiceAvailability.Unknown };

        var offers = Registry().Describe(context);

        offers.Single(offer => offer.Descriptor.Name == "transport").UnavailableReason.ShouldBe(ToolContext.MixerClosedReason);
        offers.Single(offer => offer.Descriptor.Name == "list_styles").UnavailableReason.ShouldBe(ServiceAvailability.UnknownReason);
    }

    [Fact]
    public void ToolContext_FromPageContext_NormalisesThePage()
    {
        var context = ToolContext.From(new PageContextDto { Page = "  Mixer " }, ServiceAvailability.Available, ServiceAvailability.Unknown);

        context.Page.ShouldBe("mixer");
        context.MixerOpen.ShouldBeTrue();
        context.Voice.Ready.ShouldBeFalse();
        ToolContext.From(new PageContextDto { Page = "tracks" }, ServiceAvailability.Available, ServiceAvailability.Available).MixerOpen.ShouldBeFalse();
        Should.Throw<ArgumentNullException>(() => ToolContext.From(null!, ServiceAvailability.Available, ServiceAvailability.Available));
    }

    [Fact]
    public void ToolInfo_MapsAnOfferToTheContractDto()
    {
        var offer = Registry().Describe(Ready("import")).Single(item => item.Descriptor.Name == "delete_track");

        var info = offer.ToInfo();

        info.Name.ShouldBe("delete_track");
        info.Feature.ShouldBe("001");
        info.Kind.ShouldBe(ToolKindDto.Server);
        info.Risk.ShouldBe(ToolRiskDto.Destructive);
        info.Undo.ShouldBe(UndoKindDto.None);
        info.Available.ShouldBeTrue();
        info.Title.ShouldBe("delete_track");
        info.Description.ShouldBe("Test tool delete_track");
    }
}
