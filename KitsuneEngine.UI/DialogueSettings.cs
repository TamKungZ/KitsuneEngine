namespace KitsuneEngine.UI;

public sealed class DialogueSettings
{
    public DialogueRevealMode DefaultRevealMode { get; set; } = DialogueRevealMode.Typewriter;
    public float DefaultCharactersPerSecond { get; set; } = 40f;
    public float FastForwardMultiplier { get; set; } = 3f;

    public void Validate()
    {
        if (DefaultCharactersPerSecond <= 0f)
            throw new InvalidOperationException("DefaultCharactersPerSecond must be greater than zero.");

        if (FastForwardMultiplier <= 0f)
            throw new InvalidOperationException("FastForwardMultiplier must be greater than zero.");
    }
}

