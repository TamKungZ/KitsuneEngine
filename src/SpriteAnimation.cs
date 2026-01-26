using System.Numerics;

namespace KitsuneEngine.Graphics;

public class SpriteAnimation
{
    public string Name { get; set; }
    public AnimationFrame[] Frames { get; set; }
    public float FrameDuration { get; set; }
    public bool Loop { get; set; }
    public bool IsPlaying { get; private set; }

    private int _currentFrame;
    private float _frameTimer;

    public int CurrentFrame => _currentFrame;
    public AnimationFrame CurrentFrameData => Frames[_currentFrame];

    public SpriteAnimation(string name, AnimationFrame[] frames, float frameDuration = 0.1f, bool loop = true)
    {
        Name = name;
        Frames = frames;
        FrameDuration = frameDuration;
        Loop = loop;
    }

    public void Play()
    {
        IsPlaying = true;
        _currentFrame = 0;
        _frameTimer = 0;
    }

    public void Stop()
    {
        IsPlaying = false;
        _currentFrame = 0;
        _frameTimer = 0;
    }

    public void Pause() => IsPlaying = false;
    public void Resume() => IsPlaying = true;

    public void Update(float deltaTime)
    {
        if (!IsPlaying || Frames.Length == 0) return;

        _frameTimer += deltaTime;

        if (_frameTimer >= FrameDuration)
        {
            _frameTimer -= FrameDuration;
            _currentFrame++;

            if (_currentFrame >= Frames.Length)
            {
                if (Loop)
                    _currentFrame = 0;
                else
                {
                    _currentFrame = Frames.Length - 1;
                    IsPlaying = false;
                }
            }
        }
    }

    public void SetFrame(int frame)
    {
        _currentFrame = Math.Clamp(frame, 0, Frames.Length - 1);
    }
}

public struct AnimationFrame
{
    public Vector2 Position;
    public Vector2 Size;

    public AnimationFrame(float x, float y, float width, float height)
    {
        Position = new Vector2(x, y);
        Size = new Vector2(width, height);
    }

    public AnimationFrame(Vector2 position, Vector2 size)
    {
        Position = position;
        Size = size;
    }
}

public class AnimationController
{
    private Dictionary<string, SpriteAnimation> _animations = new();
    private SpriteAnimation? _currentAnimation;

    public string? CurrentAnimationName { get; private set; }
    public AnimationFrame CurrentFrame => _currentAnimation?.CurrentFrameData ?? default;

    public void AddAnimation(SpriteAnimation animation)
    {
        _animations[animation.Name] = animation;
    }

    public void Play(string animationName, bool restart = false)
    {
        if (!_animations.TryGetValue(animationName, out var animation))
            return;

        if (_currentAnimation != animation || restart)
        {
            _currentAnimation?.Stop();
            _currentAnimation = animation;
            CurrentAnimationName = animationName;
            _currentAnimation.Play();
        }
    }

    public void Stop()
    {
        _currentAnimation?.Stop();
        _currentAnimation = null;
        CurrentAnimationName = null;
    }

    public void Update(float deltaTime)
    {
        _currentAnimation?.Update(deltaTime);
    }

    public bool IsPlaying(string animationName) =>
        CurrentAnimationName == animationName && _currentAnimation?.IsPlaying == true;

    // Helper: Create animation from spritesheet
    public static SpriteAnimation CreateFromSpriteSheet(
        string name,
        int frameCount,
        int frameWidth,
        int frameHeight,
        int columns,
        float frameDuration = 0.1f,
        bool loop = true)
    {
        var frames = new AnimationFrame[frameCount];

        for (int i = 0; i < frameCount; i++)
        {
            int x = (i % columns) * frameWidth;
            int y = (i / columns) * frameHeight;
            frames[i] = new AnimationFrame(x, y, frameWidth, frameHeight);
        }

        return new SpriteAnimation(name, frames, frameDuration, loop);
    }
}