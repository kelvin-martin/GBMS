
using CommunityToolkit.Mvvm.ComponentModel;

namespace GBMS.ViewModels.Mfd;

public class MfdPlaceholderViewModel : ObservableObject, IMfdInputReceiver
{
    public string Title { get; }

    public MfdPlaceholderViewModel(string title)
    {
        Title = title;
    }

    public void HandleFunctionKey(MfdFunctionKey key)
    {
        
    }

}
