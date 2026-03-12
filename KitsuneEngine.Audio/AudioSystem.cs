using Silk.NET.OpenAL;
using System.Numerics;
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

    public void RegisterClip(string name, AudioClip clip)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Clip name cannot be null or empty.", nameof(name));
        if (clip == null)
            throw new ArgumentNullException(nameof(clip));

        _clips[name] = clip;
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

    public void PlaySound3D(
        string name,
        Vector3 position,
        float volume = 1.0f,
        float pitch = 1.0f,
        bool loop = false,
        SpatialAudioSettings? spatial = null)
    {
        if (!_clips.TryGetValue(name, out var clip))
            return;

        var source = GetAvailableSource();
        source.SetClip(clip);
        source.Volume = volume;
        source.Pitch = pitch;
        source.Loop = loop;
        source.SetPosition(position.X, position.Y, position.Z);

        if (spatial != null)
        {
            source.SetDistanceAttenuation(spatial.ReferenceDistance, spatial.MaxDistance, spatial.RolloffFactor);
            source.SetDirection(spatial.Direction.X, spatial.Direction.Y, spatial.Direction.Z);
            source.SetCone(spatial.InnerAngle, spatial.OuterAngle, spatial.OuterGain);
        }

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

    public void SetListenerVelocity(float x, float y, float z = 0)
    {
        _al.SetListenerProperty(ListenerVector3.Velocity, x, y, z);
        CheckALError("SetListenerVelocity");
    }

    public void SetListenerOrientation(Vector3 forward, Vector3 up)
    {
        forward = Vector3.Normalize(forward);
        up = Vector3.Normalize(up);

        float[] orientation =
        [
            forward.X, forward.Y, forward.Z,
            up.X, up.Y, up.Z
        ];

        unsafe
        {
            fixed (float* ptr = orientation)
            {
                _al.SetListenerProperty(ListenerFloatArray.Orientation, ptr);
            }
        }
        CheckALError("SetListenerOrientation");
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
        LoadAudioFile(path);
    }

    private void LoadAudioFile(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        switch (ext)
        {
            case ".wav":
                LoadWav(path);
                break;
            case ".ogg":
                LoadOgg(path);
                break;
            default:
                throw new NotSupportedException($"Unsupported audio format: {ext}. Supported formats: .wav, .ogg");
        }
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

    private void LoadOgg(string path)
    {
        using var reader = new NVorbis.VorbisReader(path);

        int channels = reader.Channels;
        int sampleRate = reader.SampleRate;

        var samples = new List<float>();
        var readBuffer = new float[4096];
        int read;

        while ((read = reader.ReadSamples(readBuffer, 0, readBuffer.Length)) > 0)
        {
            for (int i = 0; i < read; i++)
                samples.Add(readBuffer[i]);
        }

        short[] pcm = new short[samples.Count];
        for (int i = 0; i < samples.Count; i++)
        {
            float s = Math.Clamp(samples[i], -1f, 1f);
            pcm[i] = (short)(s * short.MaxValue);
        }

        BufferFormat format = channels switch
        {
            1 => BufferFormat.Mono16,
            2 => BufferFormat.Stereo16,
            _ => throw new Exception($"Unsupported OGG channel count: {channels}")
        };

        Buffer = _al.GenBuffer();
        fixed (short* ptr = pcm)
        {
            _al.BufferData(Buffer, format, ptr, pcm.Length * sizeof(short), sampleRate);
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

    public void SetDirection(float x, float y, float z = 0)
    {
        _al.SetSourceProperty(Source, SourceVector3.Direction, x, y, z);
    }

    public void SetDistanceAttenuation(float referenceDistance = 1.0f, float maxDistance = 100f, float rolloffFactor = 1.0f)
    {
        _al.SetSourceProperty(Source, SourceFloat.ReferenceDistance, Math.Max(0.001f, referenceDistance));
        _al.SetSourceProperty(Source, SourceFloat.MaxDistance, Math.Max(referenceDistance, maxDistance));
        _al.SetSourceProperty(Source, SourceFloat.RolloffFactor, Math.Max(0f, rolloffFactor));
    }

    public void SetCone(float innerAngle = 360f, float outerAngle = 360f, float outerGain = 0f)
    {
        _al.SetSourceProperty(Source, SourceFloat.ConeInnerAngle, Math.Clamp(innerAngle, 0f, 360f));
        _al.SetSourceProperty(Source, SourceFloat.ConeOuterAngle, Math.Clamp(outerAngle, 0f, 360f));
        _al.SetSourceProperty(Source, SourceFloat.ConeOuterGain, Math.Clamp(outerGain, 0f, 1f));
    }

    public void Dispose()
    {
        _al.DeleteSource(Source);
    }
}

public sealed class SpatialAudioSettings
{
    public float ReferenceDistance { get; set; } = 1.0f;
    public float MaxDistance { get; set; } = 100f;
    public float RolloffFactor { get; set; } = 1.0f;

    public Vector3 Direction { get; set; } = new Vector3(0, 0, -1);
    public float InnerAngle { get; set; } = 360f;
    public float OuterAngle { get; set; } = 360f;
    public float OuterGain { get; set; } = 0f;
}
