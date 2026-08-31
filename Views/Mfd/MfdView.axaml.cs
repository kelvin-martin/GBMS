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

    private void OnF1Click(object? sender, RoutedEventArgs e)
    {
        ViewModel.HandleFunctionKey(MfdFunctionKey.F1);
    }

    private void OnF2Click(object? sender, RoutedEventArgs e)
    {
        ViewModel.HandleFunctionKey(MfdFunctionKey.F2);
    }

    private void OnF3Click(object? sender, RoutedEventArgs e)
    {
        ViewModel.HandleFunctionKey(MfdFunctionKey.F3);
    }

    private void OnF4Click(object? sender, RoutedEventArgs e)
    {
        ViewModel.HandleFunctionKey(MfdFunctionKey.F4);
    }

    private void OnF5Click(object? sender, RoutedEventArgs e)
    {
        ViewModel.HandleFunctionKey(MfdFunctionKey.F5);
    }

    private void OnF6Click(object? sender, RoutedEventArgs e)
    {
        ViewModel.HandleFunctionKey(MfdFunctionKey.F6);
    }

    private void OnF7Click(object? sender, RoutedEventArgs e)
    {
        ViewModel.HandleFunctionKey(MfdFunctionKey.F7);
    }

    private void OnF8Click(object? sender, RoutedEventArgs e)
    {
        ViewModel.HandleFunctionKey(MfdFunctionKey.F8);
    }

    private void OnF9Click(object? sender, RoutedEventArgs e)
    {
        ViewModel.HandleFunctionKey(MfdFunctionKey.F9);
    }

    private void OnF10Click(object? sender, RoutedEventArgs e)
    {
        ViewModel.HandleFunctionKey(MfdFunctionKey.F10);
    }

    private void OnF11Click(object? sender, RoutedEventArgs e)
    {
        ViewModel.HandleFunctionKey(MfdFunctionKey.F11);
    }

    private void OnF12Click(object? sender, RoutedEventArgs e)
    {
        ViewModel.HandleFunctionKey(MfdFunctionKey.F12);
    }
}