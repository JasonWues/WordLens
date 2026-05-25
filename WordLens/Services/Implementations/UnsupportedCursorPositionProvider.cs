using WordLens.Abstractions.Services;

namespace WordLens.Services.Implementations;

public sealed class UnsupportedCursorPositionProvider : ICursorPositionProvider
{
    public bool TryGetCursorPosition(out CursorPosition position)
    {
        position = default;
        return false;
    }
}
