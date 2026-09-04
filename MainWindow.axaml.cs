using System.Diagnostics;
using Avalonia.Controls;
using GBMS.Services;
using GBMS.Simulation;

namespace GBMS;

/// <summary>
/// The main window of the application.
/// </summary>
public partial class MainWindow : Window
{
    private readonly SimulationManager? _simManager;

    public MainWindow()
    {
        ApplicationFactory.Initialize();

        _simManager = ApplicationFactory.SimulationManager;

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

    /// <summary>
    /// Handles the Closing event of the MainWindow.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="System.ComponentModel.CancelEventArgs"/> instance containing the event data.</param>
    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _simManager?.Stop();
    }
}