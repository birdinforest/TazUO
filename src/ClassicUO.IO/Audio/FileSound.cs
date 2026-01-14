// SPDX-License-Identifier: BSD-2-Clause

using ClassicUO.Utility.Logging;
using Microsoft.Xna.Framework.Audio;
using MP3Sharp;
using System;
using System.IO;

namespace ClassicUO.IO.Audio
{
    /// <summary>
    /// Sound implementation that loads audio files from the file system.
    /// Supports WAV and MP3 formats.
    /// </summary>
    public class FileSound : Sound
    {
        private readonly byte[] _waveBuffer;
        private readonly string _filePath;

        /// <summary>
        /// Creates a FileSound instance by loading an audio file from the specified path.
        /// </summary>
        /// <param name="filePath">Full path to the audio file (WAV or MP3)</param>
        /// <param name="index">Index for the sound (used for comparison/sorting)</param>
        /// <exception cref="FileNotFoundException">Thrown when the file doesn't exist</exception>
        /// <exception cref="NotSupportedException">Thrown when the file format is not supported</exception>
        public FileSound(string filePath, int index = 0) : base(Path.GetFileNameWithoutExtension(filePath), index)
        {
            _filePath = filePath;

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Sound file not found: {filePath}");
            }

            string extension = Path.GetExtension(filePath).ToLowerInvariant();

            try
            {
                if (extension == ".wav")
                {
                    _waveBuffer = LoadWavFile(filePath);
                }
                else if (extension == ".mp3")
                {
                    _waveBuffer = LoadMp3File(filePath);
                }
                else
                {
                    throw new NotSupportedException($"Unsupported audio format: {extension}. Supported formats: .wav, .mp3");
                }

                if (_waveBuffer != null && _waveBuffer.Length > 0)
                {
                    // Calculate delay based on actual audio duration
                    // Duration (ms) = (bytes / (channels * bytes_per_sample)) / sample_rate * 1000
                    // For 16-bit PCM: bytes_per_sample = 2
                    int bytesPerSample = 2; // 16-bit audio
                    int channelCount = Channels == AudioChannels.Mono ? 1 : 2;
                    float durationSeconds = (float)_waveBuffer.Length / (channelCount * bytesPerSample * Frequency);
                    Delay = (uint)(durationSeconds * 1000); // Convert to milliseconds
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to load sound file '{filePath}': {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Loads a WAV file and returns its PCM data (without WAV headers).
        /// </summary>
        private byte[] LoadWavFile(string filePath)
        {
            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            using (BinaryReader reader = new BinaryReader(fs))
            {
                // Read and validate RIFF header
                string riffHeader = new string(reader.ReadChars(4)); // "RIFF"
                if (riffHeader != "RIFF")
                {
                    throw new NotSupportedException($"Invalid WAV file format: Expected 'RIFF', got '{riffHeader}'");
                }

                reader.ReadInt32(); // File size - 8 (not needed)

                string waveHeader = new string(reader.ReadChars(4)); // "WAVE"
                if (waveHeader != "WAVE")
                {
                    throw new NotSupportedException($"Invalid WAV file format: Expected 'WAVE', got '{waveHeader}'");
                }

                // Read fmt chunk
                string fmtHeader = new string(reader.ReadChars(4)); // "fmt "
                if (fmtHeader != "fmt ")
                {
                    throw new NotSupportedException($"Invalid WAV file format: Expected 'fmt ', got '{fmtHeader}'");
                }

                int fmtSize = reader.ReadInt32();
                long fmtEndPosition = reader.BaseStream.Position + fmtSize;

                reader.ReadInt16(); // Audio format (1 = PCM)
                short channels = reader.ReadInt16();
                int sampleRate = reader.ReadInt32();
                reader.ReadInt32(); // Byte rate (not needed)
                reader.ReadInt16(); // Block align (not needed)
                reader.ReadInt16(); // Bits per sample (not needed)

                // Set frequency and channels for Sound base class
                Frequency = sampleRate;
                Channels = channels == 1 ? AudioChannels.Mono : AudioChannels.Stereo;

                // Skip to end of fmt chunk (in case there are extra bytes)
                reader.BaseStream.Position = fmtEndPosition;

                // Find data chunk (skip any other chunks)
                string chunkId;
                int chunkSize;
                do
                {
                    chunkId = new string(reader.ReadChars(4));
                    chunkSize = reader.ReadInt32();

                    if (chunkId != "data")
                    {
                        // Skip this chunk
                        reader.BaseStream.Position += chunkSize;
                    }
                } while (chunkId != "data" && reader.BaseStream.Position < reader.BaseStream.Length);

                if (chunkId != "data")
                {
                    throw new NotSupportedException("WAV file does not contain 'data' chunk");
                }

                // Read the PCM audio data (without headers)
                byte[] pcmData = reader.ReadBytes(chunkSize);
                return pcmData;
            }
        }

        /// <summary>
        /// Loads an MP3 file and decodes it to PCM format.
        /// </summary>
        private byte[] LoadMp3File(string filePath)
        {
            const int bufferSize = 0x8000; // 32768 bytes, same as UOMusic uses
            using (var mp3Stream = new MP3Stream(filePath, bufferSize))
            {
                // Set frequency and channels based on MP3 stream before reading
                Frequency = mp3Stream.Frequency;
                // MP3Stream doesn't expose channel count, default to Stereo like UOMusic
                Channels = AudioChannels.Stereo;

                // Read all PCM data from the MP3 stream
                using (var memoryStream = new MemoryStream())
                {
                    byte[] buffer = new byte[bufferSize];
                    int bytesRead;

                    while ((bytesRead = mp3Stream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        memoryStream.Write(buffer, 0, bytesRead);
                    }

                    return memoryStream.ToArray();
                }
            }
        }

        protected override ArraySegment<byte> GetBuffer()
        {
            if (_waveBuffer != null && _waveBuffer.Length > 0)
            {
                return new ArraySegment<byte>(_waveBuffer);
            }

            return ArraySegment<byte>.Empty;
        }

        protected override void OnBufferNeeded(object sender, EventArgs e)
        {
            // FileSound loads the entire file into memory, so no additional buffering is needed
            // This is similar to UOSound's implementation
        }
    }
}

