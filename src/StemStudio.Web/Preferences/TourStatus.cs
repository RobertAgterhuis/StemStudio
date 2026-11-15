namespace StemStudio.Web.Preferences;

/// <summary>How a guided tour ended (data-model.md 1.1 <c>help.tours[].status</c>).</summary>
public enum TourStatus
{
    /// <summary>The user reached the last step and pressed Done.</summary>
    Completed = 0,

    /// <summary>Closed, Escape, navigation or the offer was dismissed.</summary>
    Dismissed = 1,
}
