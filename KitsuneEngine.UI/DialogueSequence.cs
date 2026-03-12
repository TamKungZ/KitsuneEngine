namespace KitsuneEngine.UI;

public sealed class DialogueSequence
{
    public IReadOnlyList<DialogueLine> Lines { get; }

    public DialogueSequence(IEnumerable<DialogueLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        Lines = lines.ToList();
    }
}

