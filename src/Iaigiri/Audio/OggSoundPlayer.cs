// ----------------------------
// OGG 効果音を読み込み再生する。
// ----------------------------
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using StardewModdingAPI;
using VorbisReader = NVorbis.VorbisReader;

namespace Iaigiri;

internal sealed class OggSoundPlayer
{
    private readonly IMonitor monitor;
    private readonly string modDirectory;
    private readonly Dictionary<string, CachedDecodedOgg> decodedCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SoundEffect> soundCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> missingPathLogged = new(StringComparer.OrdinalIgnoreCase);

    // ----------------------------
    // 必要な初期値を設定してインスタンスを初期化する。
    // ----------------------------
    public OggSoundPlayer(IMonitor monitor, string modDirectory)
    {
        this.monitor = monitor;
        this.modDirectory = modDirectory;
    }

    // ----------------------------
    // 設定された OGG ファイルをそのまま再生する。
    // ----------------------------
    public void Play(string relativePath, float volume = 1f)
    {
        this.Play(relativePath, null, volume);
    }

    // ----------------------------
    // 設定された OGG ファイルを指定時間の長さ変換して再生する。
    // ----------------------------
    public void Play(string relativePath, int? targetDurationMilliseconds, float volume = 1f)
    {
        try
        {
            string fullPath = Path.Combine(this.modDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(fullPath))
            {
                if (this.missingPathLogged.Add(fullPath))
                    this.monitor.Log($"Iaigiri: 効果音ファイルが見つかりませんでした: {fullPath}", LogLevel.Warn);

                return;
            }

            DateTime lastWriteUtc = File.GetLastWriteTimeUtc(fullPath);
            string cacheKey = targetDurationMilliseconds is > 0
                ? $"{fullPath}|{targetDurationMilliseconds.Value}"
                : fullPath;

            if (!this.soundCache.TryGetValue(cacheKey, out SoundEffect? sound))
            {
                CachedDecodedOgg decoded = this.GetDecoded(fullPath, lastWriteUtc);
                sound = this.CreateSoundEffect(decoded, targetDurationMilliseconds);
                this.soundCache[cacheKey] = sound;
            }

            this.missingPathLogged.Remove(fullPath);
            sound.Play(Math.Clamp(volume, 0f, 1f), 0f, 0f);
        }
        catch (Exception ex)
        {
            this.monitor.Log($"Iaigiri: 効果音の再生に失敗しました。{ex}", LogLevel.Warn);
        }
    }

    // ----------------------------
    // OGG をデコードした波形データをキャッシュする。
    // ----------------------------
    private CachedDecodedOgg GetDecoded(string fullPath, DateTime lastWriteUtc)
    {
        if (this.decodedCache.TryGetValue(fullPath, out CachedDecodedOgg? cached) && cached.LastWriteUtc == lastWriteUtc)
            return cached;

        this.decodedCache.Remove(fullPath);
        this.RemoveDerivedSounds(fullPath);

        CachedDecodedOgg decoded = this.LoadOgg(fullPath, lastWriteUtc);
        this.decodedCache[fullPath] = decoded;
        return decoded;
    }

    // ----------------------------
    // 同じ元ファイルから生成した派生 SoundEffect を破棄する。
    // ----------------------------
    private void RemoveDerivedSounds(string fullPath)
    {
        List<string> keysToRemove = this.soundCache.Keys
            .Where(key => key.Equals(fullPath, StringComparison.OrdinalIgnoreCase) || key.StartsWith(fullPath + "|", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (string key in keysToRemove)
        {
            this.soundCache[key].Dispose();
            this.soundCache.Remove(key);
        }
    }

    // ----------------------------
    // OGG を PCM にデコードして保持する。
    // ----------------------------
    private CachedDecodedOgg LoadOgg(string fullPath, DateTime lastWriteUtc)
    {
        using VorbisReader reader = new(fullPath);
        int channels = Math.Max(1, reader.Channels);
        int sampleRate = Math.Max(8000, reader.SampleRate);
        List<float> samples = new();
        float[] buffer = new float[4096];
        int read;
        while ((read = reader.ReadSamples(buffer, 0, buffer.Length)) > 0)
        {
            for (int i = 0; i < read; i++)
                samples.Add(Math.Clamp(buffer[i], -1f, 1f));
        }

        return new CachedDecodedOgg(lastWriteUtc, samples.ToArray(), channels, sampleRate);
    }

    // ----------------------------
    // デコード済み PCM から必要な長さの SoundEffect を作成する。
    // ----------------------------
    private SoundEffect CreateSoundEffect(CachedDecodedOgg decoded, int? targetDurationMilliseconds)
    {
        float[] outputSamples = targetDurationMilliseconds is > 0
            ? this.ResampleToDuration(decoded, targetDurationMilliseconds.Value)
            : decoded.Samples;

        byte[] pcm = this.ConvertToPcm16(outputSamples);
        AudioChannels audioChannels = decoded.Channels > 1 ? AudioChannels.Stereo : AudioChannels.Mono;
        return new SoundEffect(pcm, decoded.SampleRate, audioChannels);
    }

    // ----------------------------
    // 指定時間になるように波形をリサンプルする。
    // ----------------------------
    private float[] ResampleToDuration(CachedDecodedOgg decoded, int targetDurationMilliseconds)
    {
        int channels = decoded.Channels;
        int sourceFrames = Math.Max(1, decoded.Samples.Length / channels);
        int targetFrames = Math.Max(1, (int)Math.Round(decoded.SampleRate * (targetDurationMilliseconds / 1000.0)));
        if (sourceFrames == targetFrames)
            return decoded.Samples;

        float[] resampled = new float[targetFrames * channels];
        if (targetFrames == 1)
        {
            for (int channel = 0; channel < channels; channel++)
                resampled[channel] = decoded.Samples[channel];

            return resampled;
        }

        for (int frame = 0; frame < targetFrames; frame++)
        {
            float sourcePosition = frame * (sourceFrames - 1f) / (targetFrames - 1f);
            int leftFrame = Math.Clamp((int)MathF.Floor(sourcePosition), 0, sourceFrames - 1);
            int rightFrame = Math.Clamp(leftFrame + 1, 0, sourceFrames - 1);
            float blend = sourcePosition - leftFrame;
            for (int channel = 0; channel < channels; channel++)
            {
                float leftSample = decoded.Samples[leftFrame * channels + channel];
                float rightSample = decoded.Samples[rightFrame * channels + channel];
                resampled[frame * channels + channel] = MathHelper.Lerp(leftSample, rightSample, blend);
            }
        }

        return resampled;
    }

    // ----------------------------
    // float PCM を 16bit PCM に変換する。
    // ----------------------------
    private byte[] ConvertToPcm16(float[] samples)
    {
        byte[] pcm = new byte[samples.Length * sizeof(short)];
        for (int i = 0; i < samples.Length; i++)
        {
            short value = (short)Math.Round(Math.Clamp(samples[i], -1f, 1f) * short.MaxValue);
            pcm[i * 2] = (byte)(value & 0xFF);
            pcm[i * 2 + 1] = (byte)((value >> 8) & 0xFF);
        }

        return pcm;
    }
}

internal sealed class CachedDecodedOgg
{
    public CachedDecodedOgg(DateTime lastWriteUtc, float[] samples, int channels, int sampleRate)
    {
        this.LastWriteUtc = lastWriteUtc;
        this.Samples = samples;
        this.Channels = channels;
        this.SampleRate = sampleRate;
    }

    public DateTime LastWriteUtc { get; }

    public float[] Samples { get; }

    public int Channels { get; }

    public int SampleRate { get; }
}
