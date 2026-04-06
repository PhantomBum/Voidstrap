using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using OggVorbisEncoder;

namespace Voidstrap.Utility
{
    /// <summary>
    /// Imports a user-selected audio file as Roblox's <c>content/sounds/oof.ogg</c> (Ogg Vorbis).
    /// Non-OGG inputs are decoded with NAudio and re-encoded as Vorbis so the game always receives a compatible file.
    /// </summary>
    internal static class DeathSoundOggEncoder
    {
        private const int TargetSampleRate = 44100;
        private const int WriteBufferSize = 512;
        private const int MaxSeconds = 120;

        /// <summary>Returns true if the file can be copied to <c>oof.ogg</c> without transcoding.</summary>
        internal static bool IsOggContainer(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            return ext is ".ogg" or ".oga";
        }

        internal static bool TryImportAsOofOgg(string sourcePath, string destinationOofPath, [NotNullWhen(false)] out string? error)
        {
            error = null;

            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            {
                error = "The selected file could not be found.";
                return false;
            }

            try
            {
                if (IsOggContainer(sourcePath))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(destinationOofPath)!);
                    File.Copy(sourcePath, destinationOofPath, overwrite: true);
                    return true;
                }

                byte[] oggBytes = EncodeToOggVorbis(sourcePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destinationOofPath)!);
                File.WriteAllBytes(destinationOofPath, oggBytes);
                return true;
            }
            catch (Exception ex)
            {
                error = $"Could not convert the audio file to OGG Vorbis.\n\n{ex.Message}";
                return false;
            }
        }

        private static byte[] EncodeToOggVorbis(string sourcePath)
        {
            using var reader = new AudioFileReader(sourcePath);
            var sampleProvider = reader.ToSampleProvider();
            sampleProvider = new WdlResamplingSampleProvider(sampleProvider, TargetSampleRate);
            int channels = sampleProvider.WaveFormat.Channels;
            if (channels < 1)
                channels = 1;

            var mono = new List<float>(TargetSampleRate * 8);
            float[] buffer = new float[4096 * channels];
            int read;
            while ((read = sampleProvider.Read(buffer, 0, buffer.Length)) > 0)
            {
                int frames = read / channels;
                for (int i = 0; i < frames; i++)
                {
                    float sum = 0f;
                    for (int c = 0; c < channels; c++)
                        sum += buffer[i * channels + c];
                    mono.Add(sum / channels);
                    if (mono.Count >= TargetSampleRate * MaxSeconds)
                        goto done;
                }
            }

        done:
            if (mono.Count == 0)
                throw new InvalidOperationException("No audio samples could be read from the file.");

            float[][] floatSamples = { mono.ToArray() };
            return GenerateOggFile(floatSamples, TargetSampleRate, 1);
        }

        private static byte[] GenerateOggFile(float[][] floatSamples, int sampleRate, int channels)
        {
            using var outputData = new MemoryStream();

            var info = VorbisInfo.InitVariableBitRate(channels, sampleRate, 0.5f);
            var serial = Random.Shared.Next();
            var oggStream = new OggStream(serial);

            var comments = new Comments();
            comments.AddTag("ENCODER", "Voidstrap");

            OggPacket infoPacket = HeaderPacketBuilder.BuildInfoPacket(info);
            OggPacket commentsPacket = HeaderPacketBuilder.BuildCommentsPacket(comments);
            OggPacket booksPacket = HeaderPacketBuilder.BuildBooksPacket(info);

            oggStream.PacketIn(infoPacket);
            oggStream.PacketIn(commentsPacket);
            oggStream.PacketIn(booksPacket);

            FlushPages(oggStream, outputData, true);

            ProcessingState processingState = ProcessingState.Create(info);
            int total = floatSamples[0].Length;

            for (int readIndex = 0; readIndex < total; readIndex += WriteBufferSize)
            {
                int length = Math.Min(WriteBufferSize, total - readIndex);
                processingState.WriteData(floatSamples, length, readIndex);

                while (!oggStream.Finished && processingState.PacketOut(out OggPacket packet))
                {
                    oggStream.PacketIn(packet);
                    FlushPages(oggStream, outputData, false);
                }
            }

            processingState.WriteEndOfStream();
            while (!oggStream.Finished && processingState.PacketOut(out OggPacket packet))
            {
                oggStream.PacketIn(packet);
                FlushPages(oggStream, outputData, false);
            }

            FlushPages(oggStream, outputData, true);

            return outputData.ToArray();
        }

        private static void FlushPages(OggStream oggStream, Stream output, bool force)
        {
            while (oggStream.PageOut(out OggPage page, force))
            {
                output.Write(page.Header, 0, page.Header.Length);
                output.Write(page.Body, 0, page.Body.Length);
            }
        }
    }
}
