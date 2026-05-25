namespace WordLens.Services.Implementations;

public sealed class UnsupportedStartupService : IStartupService
{
    public bool IsSupported => false;

    public bool IsEnabled()
    {
        return false;
    }

    public void SetEnabled(bool enabled)
    {
    }
}
