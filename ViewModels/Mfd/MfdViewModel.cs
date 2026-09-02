using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using GBMS.Services;

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

    public TacticalMapViewModel TacticalMapViewModel { get; } = new();

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
            MfdFunctionalArea.SA => TacticalMapViewModel,

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
        if (CurrentFunctionalArea == functionalArea)
            return;

        CurrentFunctionalArea = functionalArea;

        Logger.Information("Functional area selected: {FunctionalArea}", functionalArea);
    }

    public void ClearFunctionalArea()
    {
        CurrentFunctionalArea = MfdFunctionalArea.None;
    }

    public void HandleFunctionKey(MfdFunctionKey key)
    {
        Logger.Debug("MFD function key received: {FunctionKey}", key);

        if (CurrentContentViewModel is IMfdInputReceiver receiver)
        {
            receiver.HandleFunctionKey(key);
        }
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
