using Avalonia.Controls;
using GBMS.Simulation;
using GBMS.Services;

namespace GBMS;

public partial class MainWindow : Window
{
    private readonly SimulationManager? _simManager;

    public MainWindow()
    {
        _simManager = SimulationFactory.Create();

        if (_simManager == null )
        {
           Logger.Error("Unable to create SimulationManager");
        }
        else
        {
            _simManager.Start();
        }

        Closing += MainWindow_Closing;
        
        InitializeComponent();
    }

 
    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _simManager?.Stop();
    }
}