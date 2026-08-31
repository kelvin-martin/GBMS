
using CommunityToolkit.Mvvm.ComponentModel;

namespace GBMS.ViewModels.Mfd;

public class MfdPlaceholderViewModel : ObservableObject
{
    public string Title { get; }

    public MfdPlaceholderViewModel(string title)
    {
        Title = title;
    }

}
