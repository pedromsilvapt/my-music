namespace MyMusic.Common.Tests.AudioIntegrity;

/// <summary>
/// Generates minimal synthetic MP3 byte arrays for testing the Mp3IntegrityValidator.
/// The generated frames have valid MPEG1 Layer III stereo headers but empty payloads.
/// </summary>
public static class SyntheticMp3Generator
{
    private const int Mpeg1Layer3SamplesPerFrame = 1152;
    private const int StereoXingOffset = 36;

    public static byte[] CreateCleanFile(int frameCount, int bitrateKbps = 128, int sampleRate = 44100)
    {
        var frameSize = ComputeFrameSize(bitrateKbps, sampleRate, Mpeg1Layer3SamplesPerFrame);
        var frames = new List<byte[]>();

        for (int i = 0; i < frameCount; i++)
        {
            frames.Add(CreateFrame(bitrateKbps, sampleRate, padding: 0));
        }

        // Embed Xing header in the first frame
        EmbedXingHeader(frames[0], frameCount, frameCount * frameSize);

        return frames.SelectMany(f => f).ToArray();
    }

    public static byte[] CreateConcatenatedDifferentBitrate(
        int firstFrameCount, int firstBitrate,
        int secondFrameCount, int secondBitrate,
        int sampleRate = 44100)
    {
        var firstFrameSize = ComputeFrameSize(firstBitrate, sampleRate, Mpeg1Layer3SamplesPerFrame);
        var secondFrameSize = ComputeFrameSize(secondBitrate, sampleRate, Mpeg1Layer3SamplesPerFrame);

        var frames = new List<byte[]>();
        for (int i = 0; i < firstFrameCount; i++)
        {
            frames.Add(CreateFrame(firstBitrate, sampleRate, padding: 0));
        }

        // Insert a non-audio gap between sections to trigger large_gap + bitrate_cliff
        var gapSize = 3000; // > LargeGapThresholdBytes (2048)
        frames.Add(new byte[gapSize]);

        for (int i = 0; i < secondFrameCount; i++)
        {
            frames.Add(CreateFrame(secondBitrate, sampleRate, padding: 0));
        }

        // Xing header claims only the first section
        EmbedXingHeader(frames[0], firstFrameCount, firstFrameCount * firstFrameSize);

        return frames.SelectMany(f => f).ToArray();
    }

    public static byte[] CreateConcatenatedSameBitrateWithMidFileTag(
        int firstFrameCount, int secondFrameCount, int bitrateKbps = 128, int sampleRate = 44100)
    {
        var frames = new List<byte[]>();
        for (int i = 0; i < firstFrameCount; i++)
        {
            frames.Add(CreateFrame(bitrateKbps, sampleRate, padding: 0));
        }

        // Insert a mid-file ID3v1 tag (128 bytes starting with "TAG")
        var tag = new byte[128];
        tag[0] = (byte)'T';
        tag[1] = (byte)'A';
        tag[2] = (byte)'G';
        frames.Add(tag);

        for (int i = 0; i < secondFrameCount; i++)
        {
            frames.Add(CreateFrame(bitrateKbps, sampleRate, padding: 0));
        }

        // Xing header claims only the first section
        var firstFrameSize = ComputeFrameSize(bitrateKbps, sampleRate, Mpeg1Layer3SamplesPerFrame);
        EmbedXingHeader(frames[0], firstFrameCount, firstFrameCount * firstFrameSize);

        return frames.SelectMany(f => f).ToArray();
    }

    public static byte[] CreateTruncatedFile(int claimedFrames, int actualFrames, int bitrateKbps = 128, int sampleRate = 44100)
    {
        var frameSize = ComputeFrameSize(bitrateKbps, sampleRate, Mpeg1Layer3SamplesPerFrame);
        var frames = new List<byte[]>();

        for (int i = 0; i < actualFrames; i++)
        {
            frames.Add(CreateFrame(bitrateKbps, sampleRate, padding: 0));
        }

        // Xing header claims more frames than exist
        EmbedXingHeader(frames[0], claimedFrames, claimedFrames * frameSize);

        return frames.SelectMany(f => f).ToArray();
    }

    public static byte[] CreateWithLargeId3v2AndNoGap(int frameCount, int id3v2Size, int bitrateKbps = 128, int sampleRate = 44100)
    {
        // Build a fake ID3v2 tag
        var id3v2 = new byte[10 + id3v2Size];
        id3v2[0] = (byte)'I';
        id3v2[1] = (byte)'D';
        id3v2[2] = (byte)'3';
        id3v2[3] = 0x04; // version 2.4
        id3v2[4] = 0x00;
        id3v2[5] = 0x00; // flags
        // Syncsafe size
        id3v2[6] = (byte)((id3v2Size >> 21) & 0x7F);
        id3v2[7] = (byte)((id3v2Size >> 14) & 0x7F);
        id3v2[8] = (byte)((id3v2Size >> 7) & 0x7F);
        id3v2[9] = (byte)(id3v2Size & 0x7F);

        var frames = new List<byte[]> { id3v2 };
        for (int i = 0; i < frameCount; i++)
        {
            frames.Add(CreateFrame(bitrateKbps, sampleRate, padding: 0));
        }

        var frameSize = ComputeFrameSize(bitrateKbps, sampleRate, Mpeg1Layer3SamplesPerFrame);
        EmbedXingHeader(frames[1], frameCount, frameCount * frameSize);

        return frames.SelectMany(f => f).ToArray();
    }

    public static byte[] CreateFrame(int bitrateKbps, int sampleRate, int padding)
    {
        var frameSize = ComputeFrameSize(bitrateKbps, sampleRate, Mpeg1Layer3SamplesPerFrame, padding);
        var frame = new byte[frameSize];

        var header = BuildHeader(bitrateKbps, sampleRate, padding);
        header.CopyTo(frame, 0);

        // Fill remainder with dummy data
        for (int i = 4; i < frameSize; i++)
        {
            frame[i] = 0xAA;
        }

        return frame;
    }

    private static byte[] BuildHeader(int bitrateKbps, int sampleRate, int padding)
    {
        var bitrateIndex = GetBitrateIndex(bitrateKbps);
        var sampleRateIndex = GetSampleRateIndex(sampleRate);

        byte b0 = 0xFF;
        byte b1 = 0xFB; // MPEG1, LayerIII, no CRC
        byte b2 = (byte)((bitrateIndex << 4) | (sampleRateIndex << 2) | (padding << 1));
        byte b3 = 0x00; // stereo, no mode extension, no copyright, no original, no emphasis

        return [b0, b1, b2, b3];
    }

    private static void EmbedXingHeader(byte[] firstFrame, long frames, long bytes)
    {
        var offset = StereoXingOffset;
        if (offset + 16 > firstFrame.Length)
            return;

        // "Xing"
        firstFrame[offset + 0] = (byte)'X';
        firstFrame[offset + 1] = (byte)'i';
        firstFrame[offset + 2] = (byte)'n';
        firstFrame[offset + 3] = (byte)'g';

        // Flags: frames + bytes present
        firstFrame[offset + 4] = 0x00;
        firstFrame[offset + 5] = 0x00;
        firstFrame[offset + 6] = 0x00;
        firstFrame[offset + 7] = 0x03;

        // Frames (big-endian)
        firstFrame[offset + 8] = (byte)((frames >> 24) & 0xFF);
        firstFrame[offset + 9] = (byte)((frames >> 16) & 0xFF);
        firstFrame[offset + 10] = (byte)((frames >> 8) & 0xFF);
        firstFrame[offset + 11] = (byte)(frames & 0xFF);

        // Bytes (big-endian)
        firstFrame[offset + 12] = (byte)((bytes >> 24) & 0xFF);
        firstFrame[offset + 13] = (byte)((bytes >> 16) & 0xFF);
        firstFrame[offset + 14] = (byte)((bytes >> 8) & 0xFF);
        firstFrame[offset + 15] = (byte)(bytes & 0xFF);
    }

    private static int ComputeFrameSize(int bitrateKbps, int sampleRate, int samplesPerFrame, int padding = 0)
    {
        return (bitrateKbps * 1000 * samplesPerFrame) / (sampleRate * 8) + padding;
    }

    private static int GetBitrateIndex(int bitrateKbps)
    {
        return bitrateKbps switch
        {
            32 => 1,
            40 => 2,
            48 => 3,
            56 => 4,
            64 => 5,
            80 => 6,
            96 => 7,
            112 => 8,
            128 => 9,
            160 => 10,
            192 => 11,
            224 => 12,
            256 => 13,
            320 => 14,
            _ => 9, // default 128
        };
    }

    private static int GetSampleRateIndex(int sampleRate)
    {
        return sampleRate switch
        {
            44100 => 0,
            48000 => 1,
            32000 => 2,
            _ => 0,
        };
    }
}
