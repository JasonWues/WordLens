namespace WordLens.Messages
{
    public class ShowPopupMessage(string text)
    {
        public string SelectedText { get; } = text;
    }
}