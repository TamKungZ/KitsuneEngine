namespace KitsuneEngine.UI;

public sealed class DialogueManager
{
    private DialogueSequence? _sequence;
    private int _lineIndex;
    private int _visibleCharacters;
    private float _characterAccumulator;
    private bool _fastForwardRequested;

    public DialogueSettings Settings { get; }
    public bool IsActive => _sequence != null && _lineIndex < _sequence.Lines.Count;

    public event Action<DialogueLine>? OnLineChanged;
    public event Action? OnDialogueFinished;

    public DialogueManager(DialogueSettings? settings = null)
    {
        Settings = settings ?? new DialogueSettings();
        Settings.Validate();
    }

    public bool TryStart(DialogueSequence sequence)
    {
        ArgumentNullException.ThrowIfNull(sequence);
        if (IsActive)
            return false;

        _sequence = sequence;
        _lineIndex = 0;
        ResetReveal();
        NotifyLineChanged();
        return true;
    }

    public void ForceStart(DialogueSequence sequence)
    {
        ArgumentNullException.ThrowIfNull(sequence);
        _sequence = sequence;
        _lineIndex = 0;
        ResetReveal();
        NotifyLineChanged();
    }

    public void Stop()
    {
        _sequence = null;
        _lineIndex = 0;
        _visibleCharacters = 0;
        _characterAccumulator = 0f;
        _fastForwardRequested = false;
    }

    public void Update(float deltaTime)
    {
        if (!IsActive)
            return;

        if (deltaTime < 0f)
            deltaTime = 0f;

        var line = GetCurrentLine();
        var fullText = line.Text;
        var revealMode = line.RevealMode ?? Settings.DefaultRevealMode;

        if (revealMode == DialogueRevealMode.Instant)
        {
            _visibleCharacters = fullText.Length;
            _characterAccumulator = 0f;
            _fastForwardRequested = false;
            return;
        }

        if (_visibleCharacters >= fullText.Length)
        {
            _fastForwardRequested = false;
            return;
        }

        var cps = ResolveCharactersPerSecond(line);
        if (_fastForwardRequested)
            cps *= Settings.FastForwardMultiplier;

        _characterAccumulator += cps * deltaTime;
        var increment = (int)_characterAccumulator;
        if (increment <= 0)
            return;

        _characterAccumulator -= increment;
        _visibleCharacters = Math.Min(fullText.Length, _visibleCharacters + increment);

        if (_visibleCharacters >= fullText.Length)
            _fastForwardRequested = false;
    }

    public void RequestFastForward(bool enabled)
    {
        _fastForwardRequested = enabled;
    }

    public void SkipOrNext()
    {
        if (!IsActive)
            return;

        var line = GetCurrentLine();
        if (_visibleCharacters < line.Text.Length)
        {
            RevealCurrentLineImmediately();
            return;
        }

        _lineIndex++;
        if (!IsActive)
        {
            Stop();
            OnDialogueFinished?.Invoke();
            return;
        }

        ResetReveal();
        NotifyLineChanged();
    }

    public void RevealCurrentLineImmediately()
    {
        if (!IsActive)
            return;

        var text = GetCurrentLine().Text;
        _visibleCharacters = text.Length;
        _characterAccumulator = 0f;
        _fastForwardRequested = false;
    }

    public DialogueState GetState()
    {
        if (!IsActive)
        {
            return new DialogueState
            {
                IsActive = false,
                IsLineFullyRevealed = true,
                CurrentLineIndex = -1,
                TotalLines = 0,
                Speaker = string.Empty,
                FullText = string.Empty,
                VisibleText = string.Empty,
                Portrait = null,
                CharactersPerSecond = Settings.DefaultCharactersPerSecond,
                RevealMode = Settings.DefaultRevealMode
            };
        }

        var line = GetCurrentLine();
        var fullText = line.Text;
        var visibleCount = Math.Clamp(_visibleCharacters, 0, fullText.Length);
        var revealMode = line.RevealMode ?? Settings.DefaultRevealMode;

        return new DialogueState
        {
            IsActive = true,
            IsLineFullyRevealed = visibleCount >= fullText.Length,
            CurrentLineIndex = _lineIndex,
            TotalLines = _sequence!.Lines.Count,
            Speaker = line.Speaker,
            FullText = fullText,
            VisibleText = fullText.Substring(0, visibleCount),
            Portrait = line.Portrait,
            CharactersPerSecond = ResolveCharactersPerSecond(line),
            RevealMode = revealMode
        };
    }

    private DialogueLine GetCurrentLine() => _sequence!.Lines[_lineIndex];

    private void ResetReveal()
    {
        _visibleCharacters = 0;
        _characterAccumulator = 0f;
        _fastForwardRequested = false;

        if (!IsActive)
            return;

        var line = GetCurrentLine();
        if ((line.RevealMode ?? Settings.DefaultRevealMode) == DialogueRevealMode.Instant)
            _visibleCharacters = line.Text.Length;
    }

    private float ResolveCharactersPerSecond(DialogueLine line)
    {
        var cps = line.CharactersPerSecond ?? Settings.DefaultCharactersPerSecond;
        return cps <= 0f ? Settings.DefaultCharactersPerSecond : cps;
    }

    private void NotifyLineChanged()
    {
        if (!IsActive)
            return;
        OnLineChanged?.Invoke(GetCurrentLine());
    }
}

