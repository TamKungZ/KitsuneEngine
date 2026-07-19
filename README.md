<p align="center">
  <img src="https://dev.tamkungz.me/assets-image/kitsune-banner.png" width="20%">
</p>

# Kitsune 2D Game Engine

A modern, lightweight 2D game engine built with .NET 8, featuring high-performance rendering, networking support, and a custom asset pipeline. Designed for simplicity and moddability.

## Development Philosophy

Kitsune Engine is built to give developers freedom:
- You can use all built-in systems as-is.
- You can use only some parts (for example, only rendering or only input).
- You can replace any helper system with your own implementation.

This means optional helpers such as scene/map flow, hitbox, and tile-map tooling are provided for convenience, not as mandatory architecture.

## Features

### Core Engine
- Cross-platform support (Windows, Linux, macOS)
- Entity Component System architecture using DefaultEcs
- Scene management with hot reload support
- Flexible service container for dependency injection

### Graphics System
- High-performance sprite batching with dynamic batching
- Camera system with zoom, rotation, and bounds
- Lighting system with up to 32 dynamic lights
- Normal mapping support
- Custom shader system with GLSL support

### Asset Pipeline
- Custom KPAK binary format with LZ4 compression
- Asynchronous asset loading with caching
- Texture, audio, JSON, and text asset support
- Package mounting system for mod support

### Audio System
- OpenAL-based audio engine
- 3D positional audio
- WAV file support with automatic format detection
- Audio clip and source management

### Networking
- Built-in multiplayer with LiteNetLib
- Entity synchronization over network
- Remote Procedure Call (RPC) system
- Peer-to-peer and client-server architectures

### Input System
- Unified input handling (keyboard, mouse, gamepad)
- Virtual axes and action mapping
- Gamepad support with multiple controller support

## Requirements

- .NET 8.0 SDK or higher
- OpenGL 3.3+ compatible graphics card
- OpenAL compatible audio device

## Quick Start

```csharp
using KitsuneEngine.Core;

public class SimpleGame : Game
{
    protected override void LoadContent()
    {
        // Mount asset packages
        var assets = Services.Get<AssetManager>();
        assets.MountDirectory("Assets");
        
        // Load a texture
        var playerTexture = assets.LoadTexture("player.png");
        
        // Play a sound
        var audio = Services.Get<AudioSystem>();
        audio.PlaySound("jump", 0.8f);
    }
    
    protected override void Update(float deltaTime)
    {
        // Game logic here
    }
}
```

## Project Structure

```
KitsuneEngine/
├── Core/          - Game loop, scene management, ECS
├── Graphics/      - Rendering, textures, sprites, cameras
├── Audio/         - Sound system with OpenAL
├── Assets/        - Asset loading and KPAK format
├── Network/       - Multiplayer networking
├── Input/         - Input handling system
└── Components/    - Built-in ECS components
```

## KPAK Asset Format

Kitsune Engine uses a custom binary format (.kpak) for packaging game assets:

```csharp
// Pack assets from directory
var packer = new KpakPacker()
    .SetCompression(true, LZ4Level.L04_HC);
packer.Pack("Assets/", "game.kpak");

// Load in game
assets.Mount("game.kpak");
var data = assets.LoadBytes("textures/player.png");
```

Features:
- LZ4 compression for reduced file size
- FNV-1a hashing for fast asset lookup
- Streaming support for large assets
- Metadata storage for debugging

## Building from Source

1. Clone the repository:
```bash
git clone https://github.com/TamKungZ/Kitsune-2D-Game-Engine.git
cd Kitsune-2D-Game-Engine
```

2. Restore dependencies:
```bash
dotnet restore
```

3. Build the engine:
```bash
dotnet build -c Release
```

## Creating a Game Project

1. Create a new .NET console project:
```bash
dotnet new console -n MyGame
cd MyGame
```

2. Add reference to KitsuneEngine:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="..\KitsuneEngine\KitsuneEngine.csproj" />
  </ItemGroup>
</Project>
```

3. Create your game class and run it.

## Examples

### Creating an Entity

```csharp
var world = new DefaultEcs.World();
var entity = world.CreateEntity();
entity.Set(new Transform { Position = new Vector2(100, 100) });
entity.Set(new Sprite { Texture = playerTexture });
entity.Set(new NetworkComponent { NetworkId = 1, IsOwner = true });
```

### Custom Component System

```csharp
public class MovementSystem : AComponentSystem<float, Transform, Velocity>
{
    public MovementSystem(World world) : base(world) { }
    
    protected override void Update(float deltaTime, ref Transform transform, ref Velocity velocity)
    {
        transform.Position += velocity.Value * deltaTime;
    }
}
```

### Networked Game

```csharp
public class NetworkedGame : Game
{
    protected override void Initialize()
    {
        var network = new NetworkService(World);
        network.StartServer(7777);
        Services.Register(network);
    }
}
```

## Performance Considerations

- Sprite batching supports up to 10,000 sprites per batch
- Asset caching reduces file I/O operations
- Network updates use delta compression
- ECS architecture ensures cache-friendly memory layout

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- Silk.NET for cross-platform graphics and input
- DefaultEcs for the Entity Component System
- StbImageSharp for image loading
- LiteNetLib for networking capabilities
- All contributors and users of the engine

## Support

For questions, issues, or discussions, please use the GitHub Issues section of this repository.

---

**Kitsune Engine** - Made for developers who want to focus on making games, not fighting with complex engines.
