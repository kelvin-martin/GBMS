using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using GBMS.ViewModels.Mfd;

namespace GBMS.Views.Mfd;

/// <summary>
/// Interaction logic for MfdView.xaml
/// </summary>
public partial class MfdView : UserControl
{
    /// <summary>
    /// The currently active functional area.
    /// </summary>
    private MfdFunctionalArea? _activeFunctionalArea;

    public MfdView()
    {
        InitializeComponent();

        DataContext = ViewModel;

        TacticalMapView.DataContext = ViewModel.TacticalMapViewModel;
    }

    /// <summary>
    /// The view model for the MfdView.
    /// </summary>
    public MfdViewModel ViewModel { get; } = new();

    /// <summary>
    /// Sets the currently active functional area.
    /// </summary>
    /// <param name="functionalArea">The functional area to set as active.</param>
    private void SetActiveFunctionalArea(MfdFunctionalArea functionalArea)
    {
        _activeFunctionalArea = functionalArea;

        TacticalMapView.IsVisible = functionalArea == MfdFunctionalArea.SA;
        ContentArea.IsVisible = functionalArea != MfdFunctionalArea.SA;

        UpdateFunctionalAreaIndicators();
    }

    /// <summary>
    /// Updates the background indicators for each functional area 
    /// based on the currently active functional area.
    /// </summary>
    private void UpdateFunctionalAreaIndicators()
    {
        SaStatusIndicator.Background =
            _activeFunctionalArea == MfdFunctionalArea.SA
                ? Brushes.Yellow : Brushes.Gray;

        WpnStatusIndicator.Background =
            _activeFunctionalArea == MfdFunctionalArea.WPN
                ? Brushes.Yellow : Brushes.Gray;

        DefStatusIndicator.Background =
            _activeFunctionalArea == MfdFunctionalArea.DEF
                ? Brushes.Yellow : Brushes.Gray;

        SysStatusIndicator.Background =
            _activeFunctionalArea == MfdFunctionalArea.SYS
                ? Brushes.Yellow : Brushes.Gray;

        DrvStatusIndicator.Background =
            _activeFunctionalArea == MfdFunctionalArea.DRV
                ? Brushes.Yellow : Brushes.Gray;

        StrStatusIndicator.Background =
            _activeFunctionalArea == MfdFunctionalArea.STR
                ? Brushes.Yellow : Brushes.Gray;

        ComStatusIndicator.Background =
            _activeFunctionalArea == MfdFunctionalArea.COM
                ? Brushes.Yellow : Brushes.Gray;

        BmsStatusIndicator.Background =
            _activeFunctionalArea == MfdFunctionalArea.BMS
                ? Brushes.Yellow : Brushes.Gray;
    }

    private void OnGva1Click(object? sender, RoutedEventArgs e)
    {
        ViewModel.SelectFunctionalArea(MfdFunctionalArea.SA);
        SetActiveFunctionalArea(MfdFunctionalArea.SA);
    }

    private void OnGva2Click(object? sender, RoutedEventArgs e)
    {
        ViewModel.SelectFunctionalArea(MfdFunctionalArea.WPN);
        SetActiveFunctionalArea(MfdFunctionalArea.WPN);
    }

    private void OnGva3Click(object? sender, RoutedEventArgs e)
    {
        ViewModel.SelectFunctionalArea(MfdFunctionalArea.DEF);
        SetActiveFunctionalArea(MfdFunctionalArea.DEF);
    }

    private void OnGva4Click(object? sender, RoutedEventArgs e)
    {
        ViewModel.SelectFunctionalArea(MfdFunctionalArea.SYS);
        SetActiveFunctionalArea(MfdFunctionalArea.SYS);
    }

    private void OnGva5Click(object? sender, RoutedEventArgs e)
    {
        ViewModel.SelectFunctionalArea(MfdFunctionalArea.DRV);
        SetActiveFunctionalArea(MfdFunctionalArea.DRV);
    }

    private void OnGva6Click(object? sender, RoutedEventArgs e)
    {
        ViewModel.SelectFunctionalArea(MfdFunctionalArea.STR);
        SetActiveFunctionalArea(MfdFunctionalArea.STR);
    }

    private void OnGva7Click(object? sender, RoutedEventArgs e)
    {
        ViewModel.SelectFunctionalArea(MfdFunctionalArea.COM);
        SetActiveFunctionalArea(MfdFunctionalArea.COM);
    }

    private void OnGva8Click(object? sender, RoutedEventArgs e)
    {
        ViewModel.SelectFunctionalArea(MfdFunctionalArea.BMS);
        SetActiveFunctionalArea(MfdFunctionalArea.BMS);
    }

    private void OnPowerClick(object? sender, RoutedEventArgs e)
    {
        ViewModel.PowerOff();
    }

    private void OnL1Click(object? sender, RoutedEventArgs e)
    {
        ViewModel.HandleFunctionKey(MfdFunctionKey.L1);
    }

    private void OnL2Click(object? sender, RoutedEventArgs e)
    {
        ViewModel.HandleFunctionKey(MfdFunctionKey.L2);
    }

    private void OnL3Click(object? sender, RoutedEventArgs e)
    {
        ViewModel.HandleFunctionKey(MfdFunctionKey.L3);
    }

    private void OnL4Click(object? sender, RoutedEventArgs e)
    {
        ViewModel.HandleFunctionKey(MfdFunctionKey.L4);
    }

    private void OnL5Click(object? sender, RoutedEventArgs e)
    {
        ViewModel.HandleFunctionKey(MfdFunctionKey.L5);
    }

    private void OnL6Click(object? sender, RoutedEventArgs e)
    {
        ViewModel.HandleFunctionKey(MfdFunctionKey.L6);
    }

    private void OnR1Click(object? sender, RoutedEventArgs e)
    {
        ViewModel.HandleFunctionKey(MfdFunctionKey.R1);
    }

    private void OnR2Click(object? sender, RoutedEventArgs e)
    {
        ViewModel.HandleFunctionKey(MfdFunctionKey.R2);
    }

    private void OnR3Click(object? sender, RoutedEventArgs e)
    {
        ViewModel.HandleFunctionKey(MfdFunctionKey.R3);
    }

    private void OnR4Click(object? sender, RoutedEventArgs e)
    {
        ViewModel.HandleFunctionKey(MfdFunctionKey.R4);
    }

    private void OnR5Click(object? sender, RoutedEventArgs e)
    {
        ViewModel.HandleFunctionKey(MfdFunctionKey.R5);
    }

    private void OnR6Click(object? sender, RoutedEventArgs e)
    {
        ViewModel.HandleFunctionKey(MfdFunctionKey.R6);
    }
}