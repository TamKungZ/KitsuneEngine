using Silk.NET.OpenAL;
using System.Runtime.InteropServices;

namespace KitsuneEngine.Audio;

public unsafe class AudioSystem : IDisposable
{
    private readonly AL _al;
    private readonly ALContext _alc;
    private Device* _device;
    private Context* _context;
    private Dictionary<string, AudioClip> _clips = new();
    private List<AudioSource> _sources = new();

    public AudioSystem()
    {
        _al = AL.GetApi(true);
        _alc = ALContext.GetApi(true);

        _device = _alc.OpenDevice("");
        if (_device == null)
            throw new Exception("Failed to open audio device");

        _context = _alc.CreateContext(_device, null);
        _alc.MakeContextCurrent(_context);

        CheckALError("AudioSystem initialization");
    }

    public AudioClip LoadSound(string name, string path)
    {
        if (_clips.TryGetValue(name, out var existing))
            return existing;

        var clip = new AudioClip(_al, path);
        _clips[name] = clip;
        return clip;
    }

    public AudioSource CreateSource()
    {
        var source = new AudioSource(_al);
        _sources.Add(source);
        return source;
    }

    public void PlaySound(string name, float volume = 1.0f, float pitch = 1.0f, bool loop = false)
    {
        if (!_clips.TryGetValue(name, out var clip))
            return;

        var source = GetAvailableSource();
        source.SetClip(clip);
        source.Volume = volume;
        source.Pitch = pitch;
        source.Loop = loop;
        source.Play();
    }

    private AudioSource GetAvailableSource()
    {
        foreach (var source in _sources)
        {
            if (!source.IsPlaying)
                return source;
        }
        return CreateSource();
    }

    public void SetListenerPosition(float x, float y, float z = 0)
    {
        _al.SetListenerProperty(ListenerVector3.Position, x, y, z);
        CheckALError("SetListenerPosition");
    }

    public void SetMasterVolume(float volume)
    {
        _al.SetListenerProperty(ListenerFloat.Gain, Math.Clamp(volume, 0f, 1f));
        CheckALError("SetMasterVolume");
    }

    private void CheckALError(string operation)
    {
        var error = _al.GetError();
        if (error != AudioError.NoError)
        {
            throw new Exception($"OpenAL Error during {operation}: {error}");
        }
    }

    public void Dispose()
    {
        foreach (var source in _sources)
            source.Dispose();

        foreach (var clip in _clips.Values)
            clip.Dispose();

        _alc.MakeContextCurrent(null);
        _alc.DestroyContext(_context);
        _alc.CloseDevice(_device);

        _al.Dispose();
        _alc.Dispose();
    }
}

public unsafe class AudioClip : IDisposable
{
    private readonly AL _al;
    public uint Buffer { get; private set; }

    public AudioClip(AL al, string path)
    {
        _al = al;
        LoadWav(path);
    }

    private void LoadWav(string path)
    {
        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream);

        // WAV header parsing
        string chunkId = new string(reader.ReadChars(4));
        if (chunkId != "RIFF") throw new Exception("Invalid WAV file");

        reader.ReadInt32(); // ChunkSize
        string format = new string(reader.ReadChars(4));
        if (format != "WAVE") throw new Exception("Invalid WAV file");

        string subchunk1Id = new string(reader.ReadChars(4));
        int subchunk1Size = reader.ReadInt32();
        short audioFormat = reader.ReadInt16();
        short numChannels = reader.ReadInt16();
        int sampleRate = reader.ReadInt32();
        reader.ReadInt32(); // ByteRate
        reader.ReadInt16(); // BlockAlign
        short bitsPerSample = reader.ReadInt16();

        // Find data chunk
        string subchunk2Id = new string(reader.ReadChars(4));
        while (subchunk2Id != "data")
        {
            int skipSize = reader.ReadInt32();
            reader.BaseStream.Seek(skipSize, SeekOrigin.Current);
            subchunk2Id = new string(reader.ReadChars(4));
        }

        int dataSize = reader.ReadInt32();
        byte[] data = reader.ReadBytes(dataSize);

        // Determine OpenAL format
        BufferFormat bufferFormat;
        if (numChannels == 1 && bitsPerSample == 8)
            bufferFormat = BufferFormat.Mono8;
        else if (numChannels == 1 && bitsPerSample == 16)
            bufferFormat = BufferFormat.Mono16;
        else if (numChannels == 2 && bitsPerSample == 8)
            bufferFormat = BufferFormat.Stereo8;
        else if (numChannels == 2 && bitsPerSample == 16)
            bufferFormat = BufferFormat.Stereo16;
        else
            throw new Exception($"Unsupported WAV format: {numChannels} channels, {bitsPerSample} bits");

        Buffer = _al.GenBuffer();
        fixed (byte* ptr = data)
        {
            _al.BufferData(Buffer, bufferFormat, ptr, dataSize, sampleRate);
        }
    }

    public void Dispose()
    {
        _al.DeleteBuffer(Buffer);
    }
}

public class AudioSource : IDisposable
{
    private readonly AL _al;
    public uint Source { get; private set; }

    public AudioSource(AL al)
    {
        _al = al;
        Source = _al.GenSource();
    }

    public void SetClip(AudioClip clip)
    {
        _al.SetSourceProperty(Source, SourceInteger.Buffer, (int)clip.Buffer);
    }

    public float Volume
    {
        get { _al.GetSourceProperty(Source, SourceFloat.Gain, out float v); return v; }
        set { _al.SetSourceProperty(Source, SourceFloat.Gain, Math.Clamp(value, 0f, 1f)); }
    }

    public float Pitch
    {
        get { _al.GetSourceProperty(Source, SourceFloat.Pitch, out float v); return v; }
        set { _al.SetSourceProperty(Source, SourceFloat.Pitch, Math.Clamp(value, 0.5f, 2f)); }
    }

    public bool Loop
    {
        get { _al.GetSourceProperty(Source, SourceBoolean.Looping, out bool v); return v; }
        set { _al.SetSourceProperty(Source, SourceBoolean.Looping, value); }
    }

    public bool IsPlaying
    {
        get
        {
            _al.GetSourceProperty(Source, GetSourceInteger.SourceState, out int state);
            return state == (int)SourceState.Playing;
        }
    }

    public void Play() => _al.SourcePlay(Source);
    public void Pause() => _al.SourcePause(Source);
    public void Stop() => _al.SourceStop(Source);

    public void SetPosition(float x, float y, float z = 0)
    {
        _al.SetSourceProperty(Source, SourceVector3.Position, x, y, z);
    }

    public void Dispose()
    {
        _al.DeleteSource(Source);
    }
}