// SPDX-License-Identifier: BSD-2-Clause

using Microsoft.Xna.Framework.Audio;
using System;
using System.IO;
using System.Reflection;
using static System.String;

namespace ClassicUO.IO.Audio
{
    public abstract class Sound : IComparable<Sound>, IDisposable
    {
        private uint _lastPlayedTime;
        private string m_Name;
        private float m_volume = 1.0f;
        private float m_volumeFactor;

        /// <summary>
        /// [DEPRECATED] Loads a sound effect from a file in the audioassets directory by filename.
        ///
        /// <para>
        /// <strong>Migration:</strong> Use <c>Client.Game.UO.Sounds.GetSoundFromFile(fileName)</c> instead.
        /// This provides centralized loading through SoundsLoader with proper caching.
        /// </para>
        ///
        /// <para>
        /// This method is kept for backward compatibility. When Client.Game is initialized,
        /// it automatically delegates to the centralized SoundsLoader. Otherwise, it falls
        /// back to the legacy implementation.
        /// </para>
        /// </summary>
        /// <param name="fileName">Name of the audio file (e.g., "sword_hit.wav" or "arrow_shot.mp3")</param>
        /// <param name="index">Optional index for the sound (defaults to 0)</param>
        /// <returns>A FileSound instance if the file exists, null otherwise</returns>
        [Obsolete("Use Client.Game.UO.Sounds.GetSoundFromFile() instead. This method will delegate automatically when available.")]
        public static Sound LoadFromFile(string fileName, int index = 0)
        {
            // Try to delegate to centralized SoundsLoader first
            Sound delegatedSound = TryDelegateToSoundsLoader(fileName);
            if (delegatedSound != null)
                return delegatedSound;

            // Fallback to current implementation (existing embedded resource + file system logic)
            // First, try embedded resources (from assembly manifest)
            Assembly assembly = typeof(FileSound).Assembly;
            // Try to find ClassicUO.Assets assembly for embedded resources
            Assembly assetsAssembly = null;
            try
            {
                System.Reflection.Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
                foreach (System.Reflection.Assembly asm in assemblies)
                {
                    if (asm.GetName().Name == "ClassicUO.Assets")
                    {
                        assetsAssembly = asm;
                        break;
                    }
                }
            }
            catch
            {
                // Ignore assembly resolution errors
            }

            Stream embeddedStream = null;
            if (assetsAssembly != null)
            {
                string assemblyName = assetsAssembly.GetName().Name;

                // Try both forward slash and backslash path separators (Windows vs Unix)
                string[] resourcePaths = {
                    $"{assemblyName}.audioassets.{fileName}",
                    $"{assemblyName}.audioassets\\{fileName}",
                    $"{assemblyName}.audioassets/{fileName}"
                };

                foreach (string resourcePath in resourcePaths)
                {
                    try
                    {
                        embeddedStream = assetsAssembly.GetManifestResourceStream(resourcePath);
                        if (embeddedStream != null)
                        {
                            break;
                        }
                    }
                    catch
                    {
                        // Continue to next path
                    }
                }
            }

            // If embedded resource found, create FileSound from stream
            if (embeddedStream != null)
            {
                try
                {
                    using (var memoryStream = new MemoryStream())
                    {
                        embeddedStream.CopyTo(memoryStream);
                        byte[] audioData = memoryStream.ToArray();

                        // Create a temporary file and use FileSound
                        string tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + Path.GetExtension(fileName));
                        File.WriteAllBytes(tempPath, audioData);

                        var sound = new FileSound(tempPath, index);
                        // Note: temp file will be cleaned up when FileSound is disposed
                        return sound;
                    }
                }
                catch
                {
                    // Log error if needed
                    return null;
                }
                finally
                {
                    embeddedStream?.Dispose();
                }
            }

            // If embedded resource not found, check external files
            string exePath = AppContext.BaseDirectory;

            // Try multiple external file paths
            string[] possiblePaths = {
                Path.Combine(exePath, "audioassets", fileName),
                Path.Combine(exePath, "ExternalAudio", fileName),
                // Try to find ClassicUO.Assets directory
                GetAudioAssetsPathFromAssembly(fileName)
            };

            string filePath = null;
            foreach (string path in possiblePaths)
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    filePath = path;
                    break;
                }
            }

            // If not found with exact name, try case-insensitive search
            if (filePath == null)
            {
                foreach (string basePath in new[] {
                    Path.Combine(exePath, "audioassets"),
                    Path.Combine(exePath, "ExternalAudio"),
                    GetAudioAssetsDirectory()
                })
                {
                    if (!string.IsNullOrEmpty(basePath) && Directory.Exists(basePath))
                    {
                        string[] files = Directory.GetFiles(basePath, "*", SearchOption.TopDirectoryOnly);
                        string foundFile = Array.Find(files, f =>
                            string.Equals(Path.GetFileName(f), fileName, StringComparison.OrdinalIgnoreCase));

                        if (foundFile != null)
                        {
                            filePath = foundFile;
                            break;
                        }
                    }
                }
            }

            if (filePath == null || !File.Exists(filePath))
            {
                return null;
            }

            try
            {
                return new FileSound(filePath, index);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Attempts to locate the audioassets directory relative to the ClassicUO.Assets assembly.
        /// </summary>
        private static string GetAudioAssetsPathFromAssembly(string fileName)
        {
            string dir = GetAudioAssetsDirectory();
            if (!string.IsNullOrEmpty(dir))
            {
                return Path.Combine(dir, fileName);
            }
            return null;
        }

        /// <summary>
        /// Gets the audioassets directory path.
        /// </summary>
        private static string GetAudioAssetsDirectory()
        {
            try
            {
                // Try to find ClassicUO.Assets assembly
                System.Reflection.Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
                foreach (System.Reflection.Assembly assembly in assemblies)
                {
                    if (assembly.GetName().Name == "ClassicUO.Assets")
                    {
                        string assemblyLocation = assembly.Location;
                        if (!string.IsNullOrEmpty(assemblyLocation))
                        {
                            string assemblyDir = Path.GetDirectoryName(assemblyLocation);
                            if (!string.IsNullOrEmpty(assemblyDir))
                            {
                                return Path.Combine(assemblyDir, "audioassets");
                            }
                        }
                    }
                }
            }
            catch
            {
                // Ignore errors in assembly resolution
            }

            return null;
        }

        /// <summary>
        /// Attempts to delegate sound loading to the centralized SoundsLoader via Client.Game.UO.Sounds.
        /// Uses reflection to avoid tight coupling between low-level IO assembly and high-level Client assembly.
        /// </summary>
        /// <param name="fileName">Name of the audio file to load</param>
        /// <returns>The loaded sound if successful, null otherwise</returns>
        private static Sound TryDelegateToSoundsLoader(string fileName)
        {
            try
            {
                // Try to find Client class using reflection
                System.Reflection.Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
                Type clientType = null;

                foreach (System.Reflection.Assembly assembly in assemblies)
                {
                    if (assembly.GetName().Name == "ClassicUO.Client")
                    {
                        clientType = assembly.GetType("ClassicUO.Client");
                        if (clientType != null)
                            break;
                    }
                }

                if (clientType == null)
                    return null;

                // Get Client.Game property
                PropertyInfo gameProperty = clientType.GetProperty("Game", BindingFlags.Public | BindingFlags.Static);
                if (gameProperty == null)
                    return null;

                object game = gameProperty.GetValue(null);
                if (game == null)
                    return null;

                // Get Game.UO property
                PropertyInfo uoProperty = game.GetType().GetProperty("UO", BindingFlags.Public | BindingFlags.Instance);
                if (uoProperty == null)
                    return null;

                object uo = uoProperty.GetValue(game);
                if (uo == null)
                    return null;

                // Get UO.Sounds property
                PropertyInfo soundsProperty = uo.GetType().GetProperty("Sounds", BindingFlags.Public | BindingFlags.Instance);
                if (soundsProperty == null)
                    return null;

                object sounds = soundsProperty.GetValue(uo);
                if (sounds == null)
                    return null;

                // Call GetSoundFromFile method
                MethodInfo getSoundFromFileMethod = sounds.GetType().GetMethod(
                    "GetSoundFromFile",
                    BindingFlags.Public | BindingFlags.Instance,
                    null,
                    new[] { typeof(string) },
                    null);

                if (getSoundFromFileMethod != null)
                {
                    object result = getSoundFromFileMethod.Invoke(sounds, new object[] { fileName });
                    return result as Sound;
                }
            }
            catch
            {
                // Silently fail and fall back to current implementation
            }

            return null;
        }


        protected Sound(string name, int index)
        {
            Name = name;
            Index = index;
        }

        public string Name
        {
            get => m_Name;
            private set
            {
                if (!IsNullOrEmpty(value))
                {
                    string[] extensions = { ".mp3", ".wav" };
                    string result = value;

                    foreach (string ext in extensions)
                    {
                        int index = value.IndexOf(ext, StringComparison.InvariantCultureIgnoreCase);
                        if (index != -1)
                        {
                            result = value.Substring(0, index + ext.Length);
                            break;
                        }
                    }

                    m_Name = result;
                }
                else
                {
                    m_Name = Empty;
                }
            }
        }

        public int Index { get; }
        public double DurationTime { get; private set; }

        public float Volume
        {
            get => m_volume;
            set
            {
                if (value < 0.0f)
                {
                    value = 0f;
                }
                else if (value > 1f)
                {
                    value = 1f;
                }

                m_volume = value;

                float instanceVolume = Math.Max(value - VolumeFactor, 0.0f);

                if (SoundInstance != null && !SoundInstance.IsDisposed)
                {
                    SoundInstance.Volume = instanceVolume;
                }
            }
        }

        public float VolumeFactor
        {
            get => m_volumeFactor;
            set
            {
                m_volumeFactor = value;
                Volume = m_volume;
            }
        }

        public bool IsPlaying(uint curTime) => SoundInstance != null && SoundInstance.State == SoundState.Playing && DurationTime > curTime;

        public int CompareTo(Sound other) => other == null ? -1 : Index.CompareTo(other.Index);

        public void Dispose()
        {
            if (SoundInstance != null)
            {
                SoundInstance.BufferNeeded -= OnBufferNeeded;

                if (!SoundInstance.IsDisposed)
                {
                    SoundInstance.Stop();
                    SoundInstance.Dispose();
                }

                SoundInstance = null;
            }
        }

        protected DynamicSoundEffectInstance SoundInstance;
        protected AudioChannels Channels = AudioChannels.Mono;
        protected uint Delay = 250;

        protected int Frequency = 22050;

        protected abstract ArraySegment<byte> GetBuffer();
        protected abstract void OnBufferNeeded(object sender, EventArgs e);

        protected virtual void AfterStop()
        {
        }

        protected virtual void BeforePlay()
        {
        }

        /// <summary>
        ///     Plays the effect.
        /// </summary>
        /// <param name="asEffect">Set to false for music, true for sound effects.</param>
        public bool Play(uint curTime, float volume = 1.0f, float volumeFactor = 0.0f, bool spamCheck = false)
        {
            if (_lastPlayedTime > curTime)
            {
                return false;
            }

            BeforePlay();

            if (SoundInstance != null && !SoundInstance.IsDisposed)
            {
                SoundInstance.Stop();
            }
            else
            {
                SoundInstance = new DynamicSoundEffectInstance(Frequency, Channels);
            }


            ArraySegment<byte> buffer = GetBuffer();

            if (buffer.Count > 0)
            {
                _lastPlayedTime = curTime + Delay;

                SoundInstance.BufferNeeded += OnBufferNeeded;
                SoundInstance.SubmitBuffer(buffer.Array, buffer.Offset, buffer.Count);
                VolumeFactor = volumeFactor;
                Volume = volume;

                DurationTime = curTime + SoundInstance.GetSampleDuration(buffer.Count).TotalMilliseconds;

                SoundInstance.Play();

                return true;
            }

            return false;
        }

        public void Stop()
        {
            if (SoundInstance != null)
            {
                SoundInstance.BufferNeeded -= OnBufferNeeded;
                SoundInstance.Stop();
            }

            // Reset spam prevention cooldown when manually stopped
            // This allows the sound to be played again immediately if needed
            _lastPlayedTime = 0;

            AfterStop();
        }
    }
}
