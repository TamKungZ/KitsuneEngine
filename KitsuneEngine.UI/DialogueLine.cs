using KitsuneEngine.Graphics;

namespace KitsuneEngine.UI;

public sealed class DialogueLine
{
    public string Speaker { get; init; }
    public string Text { get; init; }
    public Texture? Portrait { get; init; }
    public DialogueRevealMode? RevealMode { get; init; }
    public float? CharactersPerSecond { get; init; }

    public DialogueLine(
        string text,
        string speaker = "",
        Texture? portrait = null,
        DialogueRevealMode? revealMode = null,
        float? charactersPerSecond = null)
    {
        Text = text ?? string.Empty;
        Speaker = speaker ?? string.Empty;
        Portrait = portrait;
        RevealMode = revealMode;
        CharactersPerSecond = charactersPerSecond;
    }
}

