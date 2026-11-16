namespace StemStudio.Agent.Tools;

/// <summary>Selection group of a tool (tool catalogue 1.0.0 "Selection per request").</summary>
public enum ToolGroup
{
    /// <summary>Offered in every page context.</summary>
    Core,

    /// <summary>Mixer controls, offered on an open mixer page.</summary>
    Mixer,

    /// <summary>Track jobs and mix management.</summary>
    Tracks,

    /// <summary>Export and score library.</summary>
    Library,

    /// <summary>Styles, generations, songs and own voice (009/010).</summary>
    Create,
}

/// <summary>When a tool can be offered (tool catalogue 1.0.0 "Availability").</summary>
public enum ToolAvailability
{
    /// <summary>The feature is delivered: always offered.</summary>
    Always,

    /// <summary>Only while a mixer page is open in the session.</summary>
    Mixer,

    /// <summary>Only while <c>GET /api/generation/status</c> reports <c>ready</c>.</summary>
    GenerationService,

    /// <summary>Only while the generation status reports the voice service <c>ready</c>.</summary>
    VoiceService,
}
