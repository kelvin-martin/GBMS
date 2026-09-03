using Avalonia;
using Avalonia.Controls;

namespace GBMS.Views.Mfd;

public partial class SpeedEditPanel : UserControl
{
    public static readonly StyledProperty<int> SpeedProperty =
        AvaloniaProperty.Register<SpeedEditPanel, int>(
            nameof(Speed));

    public int Speed
    {
        get => GetValue(SpeedProperty);
        set => SetValue(SpeedProperty, value);
    }

    public SpeedEditPanel()
    {
        InitializeComponent();
    }
}