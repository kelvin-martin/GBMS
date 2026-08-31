using Avalonia.Controls;
using Avalonia.Interactivity;
using GBMS.ViewModels.Mfd;

namespace GBMS.Views.Mfd;

public partial class MfdView : UserControl
{
    public MfdView()
    {
        InitializeComponent();

        DataContext = ViewModel;
    }

    public MfdViewModel ViewModel { get; } = new();

    private void OnGva1Click(object? sender, RoutedEventArgs e)
    {
        ViewModel.SelectFunctionalArea(MfdFunctionalArea.SA);
    }

    private void OnGva2Click(object? sender, RoutedEventArgs e)
    {
        ViewModel.SelectFunctionalArea(MfdFunctionalArea.WPN);
    }

    private void OnGva3Click(object? sender, RoutedEventArgs e)
    {
        ViewModel.SelectFunctionalArea(MfdFunctionalArea.DEF);
    }

    private void OnGva4Click(object? sender, RoutedEventArgs e)
    {
        ViewModel.SelectFunctionalArea(MfdFunctionalArea.SYS);
    }

    private void OnGva5Click(object? sender, RoutedEventArgs e)
    {
        ViewModel.SelectFunctionalArea(MfdFunctionalArea.DRV);
    }

    private void OnGva6Click(object? sender, RoutedEventArgs e)
    {
        ViewModel.SelectFunctionalArea(MfdFunctionalArea.STR);
    }

    private void OnGva7Click(object? sender, RoutedEventArgs e)
    {
        ViewModel.SelectFunctionalArea(MfdFunctionalArea.COM);
    }

    private void OnGva8Click(object? sender, RoutedEventArgs e)
    {
        ViewModel.SelectFunctionalArea(MfdFunctionalArea.BMS);
    }

    private void OnPowerClick(object? sender, RoutedEventArgs e)
    {
        ViewModel.PowerOff();
    }
}