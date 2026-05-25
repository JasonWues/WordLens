namespace WordLens.Services;

public readonly record struct CursorPosition(int X, int Y);

public interface ICursorPositionProvider
{
    bool TryGetCursorPosition(out CursorPosition position);
}
