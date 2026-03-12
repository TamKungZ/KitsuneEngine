using KitsuneEngine.Graphics;

namespace KitsuneEngine.UI;

public readonly struct DialogueState
{
    public bool IsActive { get; init; }
    public bool IsLineFullyRevealed { get; init; }
    public int CurrentLineIndex { get; init; }
    public int TotalLines { get; init; }
    public string Speaker { get; init; }
    public string FullText { get; init; }
    public string VisibleText { get; init; }
    public Texture? Portrait { get; init; }
    public float CharactersPerSecond { get; init; }
    public DialogueRevealMode RevealMode { get; init; }

    public float Progress => FullText.Length == 0 ? 1f : (float)VisibleText.Length / FullText.Length;
}

