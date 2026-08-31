using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;

namespace GBMS.ViewModels.Mfd;


/// <summary>
/// Represents the view model for the MFD (Multi-Function Display).
/// </summary>
public partial class MfdViewModel : ObservableObject
{
    public IReadOnlyList<MfdNavigationItem> NavigationItems { get; } =
    [
        new MfdNavigationItem
        {
            FunctionalArea = MfdFunctionalArea.SA,
            Label = "SA",
            IsSimulated = true
        },
        new MfdNavigationItem
        {
            FunctionalArea = MfdFunctionalArea.WPN,
            Label = "WPN",
            IsSimulated = true
        },
        new MfdNavigationItem
        {
            FunctionalArea = MfdFunctionalArea.DEF,
            Label = "DEF",
            IsSimulated = false
        },
        new MfdNavigationItem
        {
            FunctionalArea = MfdFunctionalArea.SYS,
            Label = "SYS",
            IsSimulated = true
        },
        new MfdNavigationItem
        {
            FunctionalArea = MfdFunctionalArea.DRV,
            Label = "DRV",
            IsSimulated = true
        },
        new MfdNavigationItem
        {
            FunctionalArea = MfdFunctionalArea.STR,
            Label = "STR",
            IsSimulated = true
        },
        new MfdNavigationItem
        {
            FunctionalArea = MfdFunctionalArea.COM,
            Label = "COM",
            IsSimulated = true
        },
        new MfdNavigationItem
        {
            FunctionalArea = MfdFunctionalArea.BMS,
            Label = "BMS",
            IsSimulated = false
        }
    ];

    public MfdViewModel()
    {
        UpdateCurrentContentViewModel();
    }

    public bool IsNavigationEnabled =>
        CurrentFunctionalArea != MfdFunctionalArea.None;

    [ObservableProperty]
    private MfdFunctionalArea _currentFunctionalArea = MfdFunctionalArea.None;

    public ObservableObject? CurrentContentViewModel { get; private set; }

    partial void OnCurrentFunctionalAreaChanged(MfdFunctionalArea value)
    {
        OnPropertyChanged(nameof(IsNavigationEnabled));

        UpdateCurrentContentViewModel();

        OnPropertyChanged(nameof(CurrentContentViewModel));
    }

    private void UpdateCurrentContentViewModel()
    {
        CurrentContentViewModel = CurrentFunctionalArea switch
        {
            MfdFunctionalArea.SA =>
                new MfdPlaceholderViewModel("Situation Awareness"),

            MfdFunctionalArea.WPN =>
                new MfdPlaceholderViewModel("Weapon System"),

            MfdFunctionalArea.DEF =>
                new MfdPlaceholderViewModel("Defence"),

            MfdFunctionalArea.SYS =>
                new MfdPlaceholderViewModel("System"),

            MfdFunctionalArea.DRV =>
                new MfdPlaceholderViewModel("Driver"),

            MfdFunctionalArea.STR =>
                new MfdPlaceholderViewModel("Stores"),

            MfdFunctionalArea.COM =>
                new MfdPlaceholderViewModel("Communications"),

            MfdFunctionalArea.BMS =>
                new MfdPlaceholderViewModel("Battle Management System"),

            _ => null
        };
    }

    public void SelectFunctionalArea(MfdFunctionalArea functionalArea)
    {
        CurrentFunctionalArea = functionalArea;
    }

    public void ClearFunctionalArea()
    {
        CurrentFunctionalArea = MfdFunctionalArea.None;
    }

    public void PowerOff()
    {
        ClearFunctionalArea();

        if (Application.Current?.ApplicationLifetime is IControlledApplicationLifetime lifetime)
        {
            lifetime.Shutdown();
        }
    }
}
