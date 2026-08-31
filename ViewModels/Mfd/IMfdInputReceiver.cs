

namespace GBMS.ViewModels.Mfd;

public enum MfdFunctionKey
{
    F1,
    F2,
    F3,
    F4,
    F5,
    F6,
    F7,
    F8,
    F9,
    F10,
    F11,
    F12
}

public interface IMfdInputReceiver
{
    void HandleFunctionKey(MfdFunctionKey key);
}
