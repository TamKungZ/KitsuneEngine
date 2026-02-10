using Silk.NET.Input;
using System.Numerics;

namespace KitsuneEngine.Input;

public class InputManager
{
    private IKeyboard _keyboard;
    private IMouse _mouse;
    private List<IGamepad> _gamepads = new();

    private HashSet<Key> _keysDown = new();
    private HashSet<Key> _keysPressed = new();
    private HashSet<Key> _keysReleased = new();

    private HashSet<MouseButton> _buttonsDown = new();
    private HashSet<MouseButton> _buttonsPressed = new();
    private HashSet<MouseButton> _buttonsReleased = new();

    public Vector2 MousePosition { get; private set; }
    public Vector2 MouseDelta { get; private set; }
    public float ScrollDelta { get; private set; }

    // Virtual input axes
    public float HorizontalAxis { get; private set; }
    public float VerticalAxis { get; private set; }

    public InputManager(IInputContext input)
    {
        _keyboard = input.Keyboards[0];
        _mouse = input.Mice[0];

        foreach (var gamepad in input.Gamepads)
            _gamepads.Add(gamepad);

        _keyboard.KeyDown += OnKeyDown;
        _keyboard.KeyUp += OnKeyUp;

        _mouse.MouseDown += OnMouseDown;
        _mouse.MouseUp += OnMouseUp;
        _mouse.MouseMove += OnMouseMove;
        _mouse.Scroll += OnScroll;

        MousePosition = _mouse.Position;
    }

    public void Update()
    {
        _keysPressed.Clear();
        _keysReleased.Clear();
        _buttonsPressed.Clear();
        _buttonsReleased.Clear();
        ScrollDelta = 0;
        MouseDelta = Vector2.Zero;

        UpdateAxes();
    }

    private void UpdateAxes()
    {
        // Keyboard axes
        HorizontalAxis = 0;
        VerticalAxis = 0;

        if (IsKeyDown(Key.A) || IsKeyDown(Key.Left)) HorizontalAxis -= 1;
        if (IsKeyDown(Key.D) || IsKeyDown(Key.Right)) HorizontalAxis += 1;
        if (IsKeyDown(Key.W) || IsKeyDown(Key.Up)) VerticalAxis -= 1;
        if (IsKeyDown(Key.S) || IsKeyDown(Key.Down)) VerticalAxis += 1;

        // Gamepad override
        if (_gamepads.Count > 0 && _gamepads[0].IsConnected)
        {
            var gamepad = _gamepads[0];
            var leftStick = gamepad.Thumbsticks[0];

            if (Math.Abs(leftStick.X) > 0.1f) HorizontalAxis = leftStick.X;
            if (Math.Abs(leftStick.Y) > 0.1f) VerticalAxis = -leftStick.Y;
        }

        // Normalize diagonal movement
        if (HorizontalAxis != 0 && VerticalAxis != 0)
        {
            float length = MathF.Sqrt(HorizontalAxis * HorizontalAxis + VerticalAxis * VerticalAxis);
            HorizontalAxis /= length;
            VerticalAxis /= length;
        }
    }

    private void OnKeyDown(IKeyboard keyboard, Key key, int code)
    {
        if (!_keysDown.Contains(key))
        {
            _keysPressed.Add(key);
            _keysDown.Add(key);
        }
    }

    private void OnKeyUp(IKeyboard keyboard, Key key, int code)
    {
        if (_keysDown.Contains(key))
        {
            _keysReleased.Add(key);
            _keysDown.Remove(key);
        }
    }

    private void OnMouseDown(IMouse mouse, MouseButton button)
    {
        if (!_buttonsDown.Contains(button))
        {
            _buttonsPressed.Add(button);
            _buttonsDown.Add(button);
        }
    }

    private void OnMouseUp(IMouse mouse, MouseButton button)
    {
        if (_buttonsDown.Contains(button))
        {
            _buttonsReleased.Add(button);
            _buttonsDown.Remove(button);
        }
    }

    private void OnMouseMove(IMouse mouse, Vector2 position)
    {
        MouseDelta = position - MousePosition;
        MousePosition = position;
    }

    private void OnScroll(IMouse mouse, ScrollWheel wheel)
    {
        ScrollDelta = wheel.Y;
    }

    // Keyboard
    public bool IsKeyDown(Key key) => _keysDown.Contains(key);
    public bool IsKeyPressed(Key key) => _keysPressed.Contains(key);
    public bool IsKeyReleased(Key key) => _keysReleased.Contains(key);

    // Mouse
    public bool IsMouseDown(MouseButton button) => _buttonsDown.Contains(button);
    public bool IsMousePressed(MouseButton button) => _buttonsPressed.Contains(button);
    public bool IsMouseReleased(MouseButton button) => _buttonsReleased.Contains(button);

    // Virtual buttons
    public bool IsActionPressed(string action)
    {
        return action switch
        {
            "jump" => IsKeyPressed(Key.Space) || IsGamepadButtonPressed(0, 0),
            "attack" => IsMousePressed(MouseButton.Left) || IsGamepadButtonPressed(0, 2),
            "interact" => IsKeyPressed(Key.E) || IsGamepadButtonPressed(0, 1),
            "menu" => IsKeyPressed(Key.Escape) || IsGamepadButtonPressed(0, 7),
            _ => false
        };
    }

    // Gamepad support
    public bool IsGamepadButtonPressed(int gamepadIndex, int buttonIndex)
    {
        if (gamepadIndex >= _gamepads.Count || !_gamepads[gamepadIndex].IsConnected)
            return false;

        var buttons = _gamepads[gamepadIndex].Buttons;
        return buttonIndex < buttons.Count && buttons[buttonIndex].Pressed;
    }

    public Vector2 GetGamepadAxis(int gamepadIndex, int axisIndex)
    {
        if (gamepadIndex >= _gamepads.Count || !_gamepads[gamepadIndex].IsConnected)
            return Vector2.Zero;

        var thumbsticks = _gamepads[gamepadIndex].Thumbsticks;
        if (axisIndex >= thumbsticks.Count)
            return Vector2.Zero;

        var stick = thumbsticks[axisIndex];
        return new Vector2(stick.X, -stick.Y);
    }

    // Helpers
    public bool IsAnyKeyPressed() => _keysPressed.Count > 0;
    public bool IsAnyMousePressed() => _buttonsPressed.Count > 0;
    public Vector2 GetMovementVector() => new Vector2(HorizontalAxis, VerticalAxis);
}