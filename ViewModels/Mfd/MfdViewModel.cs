using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using GBMS.Services;
using GBMS.Simulation;

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

    private readonly ISimulationClock? _clock;

    private readonly DispatcherTimer? _clockTimer;

    private readonly Messenger? _messenger;

    public string SimulationTime =>
        _clock?.UtcNow.ToString("HH:mm:ss") ?? "--:--:--";

    /// <summary>
    /// Gets the text currently displayed in the Warnings and Alerts area.
    /// Sourced from the application-wide <see cref="Messenger"/> - the only
    /// route from deeply-nested controls (e.g. RouteManagerControl) up to
    /// this top-level display, since neither owns a direct reference to
    /// the other.
    /// </summary>
    [ObservableProperty]
    private string _alertText = "NO ACTIVE WARNINGS";

    /// <summary>
    /// Gets whether the current alert text represents a high-urgency
    /// warning (as opposed to routine/informational text).
    /// </summary>
    [ObservableProperty]
    private bool _isAlertActive;

    /// <summary>
    /// Gets the foreground brush for the alert text, derived from
    /// <see cref="IsAlertActive"/>.
    /// </summary>
    public IBrush AlertForeground =>
        IsAlertActive ? Brushes.OrangeRed : Brushes.LightGreen;

    partial void OnIsAlertActiveChanged(bool value)
    {
        OnPropertyChanged(nameof(AlertForeground));
    }

    public MfdViewModel()
    {
        if (!Design.IsDesignMode)
        {
            _clock = ApplicationFactory.SimulationManager.Clock;

            _clockTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };

            _clockTimer.Tick += OnClockTimerTick;
            _clockTimer.Start();

            _messenger = ApplicationFactory.Messenger;
            _messenger.MessageReceived += OnMessageReceived;
        }

        UpdateCurrentContentViewModel();
    }

    public bool IsNavigationEnabled => CurrentFunctionalArea != MfdFunctionalArea.None;

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
        if (CurrentContentViewModel is IMfdInputReceiver receiver)
        {
            receiver.HandleFunctionKey(key);
        }
    }

    public void PowerOff()
    {
        ClearFunctionalArea();

        ApplicationFactory.SimulationManager.Stop();

        if (Application.Current?.ApplicationLifetime is IControlledApplicationLifetime lifetime)
        {
            lifetime.Shutdown();
        }
    }

    private void OnClockTimerTick(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(SimulationTime));
    }

    /// <summary>
    /// Handles a message broadcast via the application Messenger, updating
    /// the Warnings and Alerts area text and styling.
    /// </summary>
    private void OnMessageReceived(object? sender, AppMessage message)
    {
        AlertText = message.Message;
        IsAlertActive = message.IsAlert;
    }
}
