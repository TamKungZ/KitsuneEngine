using Silk.NET.Input;
using System.Numerics;

namespace KitsuneEngine.Input;

public class InputManager : IDisposable
{
    private IKeyboard? _keyboard;
    private IMouse? _mouse;
    private List<IGamepad> _gamepads = new();
    private IInputContext _inputContext;

    private Dictionary<Key, InputState> _keyStates = new();
    private Dictionary<MouseButton, InputState> _mouseStates = new();
    private Dictionary<(int, int), InputState> _gamepadButtonStates = new();

    private Vector2 _lastMousePosition;
    private Vector2 _currentMousePosition;
    private float _scrollDelta;

    // Input configuration
    private Dictionary<string, InputAction> _inputActions = new();
    private Dictionary<string, AxisConfig> _inputAxes = new(); // Changed from InputAxis to AxisConfig

    // Input buffer for combos
    private Queue<BufferedInput> _inputBuffer = new();
    private float _bufferTime = 0.2f;

    // Mouse settings
    public bool IsMouseLocked { get; private set; }
    public bool IsMouseVisible { get; private set; } = true;
    public CursorStyle CursorStyle { get; private set; } = CursorStyle.Default;

    // Gamepad settings
    public float GamepadDeadzone { get; set; } = 0.1f;
    public float GamepadTriggerThreshold { get; set; } = 0.1f;

    public Vector2 MousePosition => _currentMousePosition;
    public Vector2 MouseDelta { get; private set; }
    public float ScrollDelta => _scrollDelta;
    public float MouseSensitivity { get; set; } = 1.0f;

    public InputManager(IInputContext input)
    {
        _inputContext = input;
        _keyboard = input.Keyboards.Count > 0 ? input.Keyboards[0] : null;
        _mouse = input.Mice.Count > 0 ? input.Mice[0] : null;

        foreach (var gamepad in input.Gamepads)
        {
            _gamepads.Add(gamepad);
        }

        if (_keyboard != null)
        {
            _keyboard.KeyDown += OnKeyDown;
            _keyboard.KeyUp += OnKeyUp;
        }

        if (_mouse != null)
        {
            _mouse.MouseDown += OnMouseDown;
            _mouse.MouseUp += OnMouseUp;
            _mouse.MouseMove += OnMouseMove;
            _mouse.Scroll += OnScroll;

            _currentMousePosition = _mouse.Position;
            _lastMousePosition = _currentMousePosition;
        }

        InitializeDefaultActions();
        InitializeDefaultAxes();
    }

    private void InitializeDefaultActions()
    {
        AddAction("Jump", Key.Space, 0, 0);
        AddMouseAction("Attack", MouseButton.Left);
        AddAction("Interact", Key.E, 0, 1);
        AddAction("Menu", Key.Escape, 0, 7);
        AddAction("Sprint", Key.ShiftLeft, 0, 4);
        AddAction("Crouch", Key.ControlLeft, 0, 5);
    }

    private void InitializeDefaultAxes()
    {
        AddAxis("Horizontal",
            new AxisConfig
            {
                PositiveKey = Key.D,
                NegativeKey = Key.A,
                PositiveAltKey = Key.Right,
                NegativeAltKey = Key.Left,
                GamepadAxis = 0,
                Invert = false
            });

        AddAxis("Vertical",
            new AxisConfig
            {
                PositiveKey = Key.W,
                NegativeKey = Key.S,
                PositiveAltKey = Key.Up,
                NegativeAltKey = Key.Down,
                GamepadAxis = 1,
                Invert = true
            });

        AddAxis("MouseX",
            new AxisConfig
            {
                MouseAxis = 0,
                Sensitivity = 0.1f
            });

        AddAxis("MouseY",
            new AxisConfig
            {
                MouseAxis = 1,
                Sensitivity = 0.1f,
                Invert = true
            });
    }

    public void Update()
    {
        _scrollDelta = 0;
        MouseDelta = Vector2.Zero;

        if (_mouse != null)
        {
            MouseDelta = (_currentMousePosition - _lastMousePosition) * MouseSensitivity;
            _lastMousePosition = _currentMousePosition;
        }

        UpdateInputStates();
        UpdateInputBuffer();
        UpdateGamepadVibration();
    }

    private void UpdateInputStates()
    {
        foreach (var key in _keyStates.Keys.ToList())
        {
            var state = _keyStates[key];
            if (state == InputState.Pressed)
                _keyStates[key] = InputState.Held;
        }

        foreach (var button in _mouseStates.Keys.ToList())
        {
            var state = _mouseStates[button];
            if (state == InputState.Pressed)
                _mouseStates[button] = InputState.Held;
        }

        foreach (var key in _gamepadButtonStates.Keys.ToList())
        {
            var state = _gamepadButtonStates[key];
            if (state == InputState.Pressed)
                _gamepadButtonStates[key] = InputState.Held;
        }
    }

    private void UpdateInputBuffer()
    {
        var currentTime = (float)DateTime.Now.TimeOfDay.TotalSeconds;
        while (_inputBuffer.Count > 0 && currentTime - _inputBuffer.Peek().Timestamp > _bufferTime)
        {
            _inputBuffer.Dequeue();
        }
    }

    private void UpdateGamepadVibration()
    {
        for (int i = 0; i < _gamepads.Count; i++)
        {
            if (_gamepads[i].IsConnected && _gamepads[i].VibrationMotors != null && _gamepads[i].VibrationMotors.Count >= 2)
            {
                var leftMotor = _gamepads[i].VibrationMotors[0];
                var rightMotor = _gamepads[i].VibrationMotors[1];

                if ((leftMotor.Speed > 0 || rightMotor.Speed > 0))
                {
                    leftMotor.Speed = Math.Max(0, leftMotor.Speed - 0.05f);
                    rightMotor.Speed = Math.Max(0, rightMotor.Speed - 0.05f);
                }
            }
        }
    }

    private void OnKeyDown(IKeyboard keyboard, Key key, int code)
    {
        if (!_keyStates.ContainsKey(key) || _keyStates[key] == InputState.Released)
        {
            _keyStates[key] = InputState.Pressed;
            _inputBuffer.Enqueue(new BufferedInput(InputType.Key, key.ToString(), (float)DateTime.Now.TimeOfDay.TotalSeconds));
        }
    }

    private void OnKeyUp(IKeyboard keyboard, Key key, int code)
    {
        _keyStates[key] = InputState.Released;
    }

    private void OnMouseDown(IMouse mouse, MouseButton button)
    {
        if (!_mouseStates.ContainsKey(button) || _mouseStates[button] == InputState.Released)
        {
            _mouseStates[button] = InputState.Pressed;
            _inputBuffer.Enqueue(new BufferedInput(InputType.MouseButton, button.ToString(), (float)DateTime.Now.TimeOfDay.TotalSeconds));
        }
    }

    private void OnMouseUp(IMouse mouse, MouseButton button)
    {
        _mouseStates[button] = InputState.Released;
    }

    private void OnMouseMove(IMouse mouse, Vector2 position)
    {
        _currentMousePosition = position;
    }

    private void OnScroll(IMouse mouse, ScrollWheel wheel)
    {
        _scrollDelta = wheel.Y;
    }

    // Configuration API
    public void AddAction(string name, Key key = Key.Unknown, int gamepadIndex = 0, int gamepadButton = -1)
    {
        if (!_inputActions.ContainsKey(name))
        {
            _inputActions[name] = new InputAction();
        }
        if (key != Key.Unknown)
            _inputActions[name].Keys.Add(key);
        if (gamepadButton >= 0)
            _inputActions[name].GamepadButtons.Add((gamepadIndex, gamepadButton));
    }

    public void AddMouseAction(string name, MouseButton button)
    {
        if (!_inputActions.ContainsKey(name))
        {
            _inputActions[name] = new InputAction();
        }
        _inputActions[name].MouseButtons.Add(button);
    }

    public void AddAxis(string name, AxisConfig config)
    {
        _inputAxes[name] = config;
    }

    public void RemapAction(string name, Key newKey)
    {
        if (_inputActions.ContainsKey(name))
        {
            _inputActions[name].Keys.Clear();
            _inputActions[name].Keys.Add(newKey);
        }
    }

    // Query API
    public bool IsKeyDown(Key key) => _keyStates.ContainsKey(key) && _keyStates[key] != InputState.Released;
    public bool IsKeyPressed(Key key) => _keyStates.ContainsKey(key) && _keyStates[key] == InputState.Pressed;
    public bool IsKeyReleased(Key key) => _keyStates.ContainsKey(key) && _keyStates[key] == InputState.Released;

    public bool IsMouseDown(MouseButton button) => _mouseStates.ContainsKey(button) && _mouseStates[button] != InputState.Released;
    public bool IsMousePressed(MouseButton button) => _mouseStates.ContainsKey(button) && _mouseStates[button] == InputState.Pressed;
    public bool IsMouseReleased(MouseButton button) => _mouseStates.ContainsKey(button) && _mouseStates[button] == InputState.Released;

    public bool IsActionPressed(string action)
    {
        if (!_inputActions.ContainsKey(action)) return false;

        var actionConfig = _inputActions[action];

        bool keyPressed = actionConfig.Keys.Any(key => IsKeyPressed(key));
        bool mousePressed = actionConfig.MouseButtons.Any(button => IsMousePressed(button));
        bool gamepadPressed = actionConfig.GamepadButtons.Any(g => IsGamepadButtonPressed(g.Item1, g.Item2));

        return keyPressed || mousePressed || gamepadPressed;
    }

    public bool IsActionDown(string action)
    {
        if (!_inputActions.ContainsKey(action)) return false;

        var actionConfig = _inputActions[action];

        bool keyDown = actionConfig.Keys.Any(key => IsKeyDown(key));
        bool mouseDown = actionConfig.MouseButtons.Any(button => IsMouseDown(button));
        bool gamepadDown = actionConfig.GamepadButtons.Any(g => IsGamepadButtonDown(g.Item1, g.Item2));

        return keyDown || mouseDown || gamepadDown;
    }

    public float GetAxis(string axisName)
    {
        if (!_inputAxes.ContainsKey(axisName)) return 0f;

        var config = _inputAxes[axisName];
        float value = 0f;

        // Keyboard input
        if (IsKeyDown(config.PositiveKey)) value += 1f;
        if (IsKeyDown(config.NegativeKey)) value -= 1f;
        if (IsKeyDown(config.PositiveAltKey)) value += 1f;
        if (IsKeyDown(config.NegativeAltKey)) value -= 1f;

        // Gamepad input
        if (config.GamepadAxis >= 0)
        {
            float gamepadValue = GetGamepadAxis(0, config.GamepadAxis).X;
            if (Math.Abs(gamepadValue) > GamepadDeadzone)
            {
                value += config.Invert ? -gamepadValue : gamepadValue;
            }
        }

        // Mouse input
        if (config.MouseAxis >= 0)
        {
            float mouseValue = config.MouseAxis == 0 ? MouseDelta.X : MouseDelta.Y;
            value += mouseValue * config.Sensitivity;
        }

        return Math.Clamp(value, -1f, 1f);
    }

    public Vector2 GetAxis2D(string horizontalAxis, string verticalAxis)
    {
        return new Vector2(GetAxis(horizontalAxis), GetAxis(verticalAxis));
    }

    // Gamepad API
    public bool IsGamepadConnected(int index) => index < _gamepads.Count && _gamepads[index].IsConnected;

    public bool IsGamepadButtonDown(int gamepadIndex, int buttonIndex)
    {
        if (!IsGamepadConnected(gamepadIndex)) return false;

        var key = (gamepadIndex, buttonIndex);
        return _gamepadButtonStates.ContainsKey(key) && _gamepadButtonStates[key] != InputState.Released;
    }

    public bool IsGamepadButtonPressed(int gamepadIndex, int buttonIndex)
    {
        if (!IsGamepadConnected(gamepadIndex)) return false;

        var buttons = _gamepads[gamepadIndex].Buttons;
        if (buttonIndex >= buttons.Count) return false;

        var key = (gamepadIndex, buttonIndex);
        bool isPressed = buttons[buttonIndex].Pressed;

        if (isPressed && (!_gamepadButtonStates.ContainsKey(key) || _gamepadButtonStates[key] == InputState.Released))
        {
            _gamepadButtonStates[key] = InputState.Pressed;
            return true;
        }

        if (!isPressed && _gamepadButtonStates.ContainsKey(key) && _gamepadButtonStates[key] != InputState.Released)
        {
            _gamepadButtonStates[key] = InputState.Released;
        }

        return false;
    }

    public Vector2 GetGamepadAxis(int gamepadIndex, int axisIndex)
    {
        if (!IsGamepadConnected(gamepadIndex)) return Vector2.Zero;

        var thumbsticks = _gamepads[gamepadIndex].Thumbsticks;
        if (axisIndex >= thumbsticks.Count) return Vector2.Zero;

        var stick = thumbsticks[axisIndex];
        Vector2 value = new Vector2(stick.X, -stick.Y);

        // Apply deadzone
        if (value.Length() < GamepadDeadzone)
            return Vector2.Zero;

        return value;
    }

    public float GetGamepadTrigger(int gamepadIndex, int triggerIndex)
    {
        if (!IsGamepadConnected(gamepadIndex)) return 0f;

        var triggers = _gamepads[gamepadIndex].Triggers;
        if (triggerIndex >= triggers.Count) return 0f;

        var trigger = triggers[triggerIndex];
        float position = trigger.Position;
        return position > GamepadTriggerThreshold ? position : 0f;
    }

    public void SetGamepadVibration(int gamepadIndex, float leftMotor, float rightMotor, float duration = 0)
    {
        if (!IsGamepadConnected(gamepadIndex)) return;

        var motors = _gamepads[gamepadIndex].VibrationMotors;
        if (motors == null || motors.Count < 2) return;

        motors[0].Speed = Math.Clamp(leftMotor, 0f, 1f);
        motors[1].Speed = Math.Clamp(rightMotor, 0f, 1f);
    }

    // Mouse control API
    public void SetMouseLocked(bool locked)
    {
        IsMouseLocked = locked;
        if (_mouse != null)
        {
            _mouse.Cursor.CursorMode = locked ? CursorMode.Raw : CursorMode.Normal;
        }
    }

    public void SetMouseVisible(bool visible)
    {
        IsMouseVisible = visible;
        if (_mouse != null)
        {
            _mouse.Cursor.CursorMode = visible ? CursorMode.Normal : CursorMode.Hidden;
        }
    }

    public void SetCursorStyle(CursorStyle style)
    {
        CursorStyle = style;
        if (_mouse != null)
        {
            _mouse.Cursor.CursorMode = style == CursorStyle.Hidden ? CursorMode.Hidden :
                                      style == CursorStyle.Raw ? CursorMode.Raw :
                                      CursorMode.Normal;
        }
    }

    // Input buffer and combo detection
    public bool CheckCombo(string[] comboSequence, float maxTimeBetweenInputs = 0.5f)
    {
        var bufferList = _inputBuffer.ToList();
        if (bufferList.Count < comboSequence.Length) return false;

        float currentTime = (float)DateTime.Now.TimeOfDay.TotalSeconds;
        int sequenceIndex = 0;

        for (int i = bufferList.Count - 1; i >= 0 && sequenceIndex < comboSequence.Length; i--)
        {
            if (currentTime - bufferList[i].Timestamp > maxTimeBetweenInputs)
                return false;

            if (bufferList[i].InputName == comboSequence[sequenceIndex])
                sequenceIndex++;
            else
                return false;
        }

        return sequenceIndex == comboSequence.Length;
    }

    public bool GetBufferedInput(out string inputName)
    {
        if (_inputBuffer.Count > 0)
        {
            inputName = _inputBuffer.Peek().InputName;
            return true;
        }
        inputName = string.Empty;
        return false;
    }

    // Helper methods
    public bool IsAnyKeyPressed() => _keyStates.Values.Any(state => state == InputState.Pressed);
    public bool IsAnyMousePressed() => _mouseStates.Values.Any(state => state == InputState.Pressed);

    public void Dispose()
    {
        if (_keyboard != null)
        {
            _keyboard.KeyDown -= OnKeyDown;
            _keyboard.KeyUp -= OnKeyUp;
        }

        if (_mouse != null)
        {
            _mouse.MouseDown -= OnMouseDown;
            _mouse.MouseUp -= OnMouseUp;
            _mouse.MouseMove -= OnMouseMove;
            _mouse.Scroll -= OnScroll;
        }

        _inputContext?.Dispose();
    }
}

// Supporting classes
public enum InputState
{
    Released,
    Pressed,
    Held
}

public enum InputType
{
    Key,
    MouseButton,
    GamepadButton
}

public enum CursorStyle
{
    Default,
    Hidden,
    Raw
}

public class InputAction
{
    public List<Key> Keys { get; set; } = new();
    public List<MouseButton> MouseButtons { get; set; } = new();
    public List<(int, int)> GamepadButtons { get; set; } = new(); // (gamepadIndex, buttonIndex)
}

public class AxisConfig
{
    public Key PositiveKey { get; set; } = Key.Unknown;
    public Key NegativeKey { get; set; } = Key.Unknown;
    public Key PositiveAltKey { get; set; } = Key.Unknown;
    public Key NegativeAltKey { get; set; } = Key.Unknown;
    public int GamepadAxis { get; set; } = -1;
    public int MouseAxis { get; set; } = -1;
    public float Sensitivity { get; set; } = 1.0f;
    public bool Invert { get; set; } = false;
}

public struct BufferedInput
{
    public InputType Type { get; }
    public string InputName { get; }
    public float Timestamp { get; }

    public BufferedInput(InputType type, string inputName, float timestamp)
    {
        Type = type;
        InputName = inputName;
        Timestamp = timestamp;
    }
}