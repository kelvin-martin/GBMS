using Avalonia.Controls;
using Avalonia.Logging;
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
            GBMS.Services.Logger.Error("Unable to create SimulationManager");
        }

        InitializeComponent();
    }
}