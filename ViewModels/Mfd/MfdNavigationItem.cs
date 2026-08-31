
namespace GBMS.ViewModels.Mfd;

/// <summary>
/// Represents a navigation item in the MFD (Multi-Function Display).
/// </summary>
public class MfdNavigationItem
{
    public MfdFunctionalArea FunctionalArea { get; init; }

    public string Label { get; init; } = string.Empty;

    public bool IsSimulated { get; init; }
}
