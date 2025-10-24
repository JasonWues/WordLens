namespace WordLens.Messages
{
    public class TriggerTranslationMessage(string text)
    {
        public string SelectedText { get; } = text;
    }
}