

namespace GBMS.ViewModels.Mfd;

public enum MfdFunctionKey
{
    L1,
    L2,
    L3,
    L4,
    L5,
    L6,
    R1,
    R2,
    R3,
    R4,
    R5,
    R6
}

public interface IMfdInputReceiver
{
    void HandleFunctionKey(MfdFunctionKey key);
}
