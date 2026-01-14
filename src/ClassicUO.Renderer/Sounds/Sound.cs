using ClassicUO.Assets;
using ClassicUO.IO.Audio;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClassicUO.Renderer.Sounds
{
    public sealed class Sound
    {
        const int MAX_SOUND_DATA_INDEX_COUNT = 0xFFFF;

        private readonly IO.Audio.Sound[] _musics = new IO.Audio.Sound[MAX_SOUND_DATA_INDEX_COUNT];
        private readonly IO.Audio.Sound[] _sounds = new IO.Audio.Sound[MAX_SOUND_DATA_INDEX_COUNT];
        private readonly bool _useDigitalMusicFolder;
        private readonly SoundsLoader _soundsLoader;
        private readonly Dictionary<string, IO.Audio.Sound> _soundsFromFile = new Dictionary<string, IO.Audio.Sound>(StringComparer.OrdinalIgnoreCase);

        public Sound(SoundsLoader soundsLoader)
        {
            _soundsLoader = soundsLoader;
            _useDigitalMusicFolder = Directory.Exists(Path.Combine(soundsLoader.FileManager.BasePath, "Music", "Digital"));
        }

        public IO.Audio.Sound GetSound(int index)
        {
            if (index >= 0 && index < MAX_SOUND_DATA_INDEX_COUNT)
            {
                ref IO.Audio.Sound sound = ref _sounds[index];

                if (sound == null && _soundsLoader.TryGetSound(index, out byte[] data, out string name))
                {
                    sound = new UOSound(name, index, data);
                }

                return sound;
            }

            return null;
        }

        public IO.Audio.Sound GetMusic(int index)
        {
            if (index >= 0 && index < MAX_SOUND_DATA_INDEX_COUNT)
            {
                ref IO.Audio.Sound music = ref _musics[index];

                if (music == null && _soundsLoader.TryGetMusicData(index, out string name, out bool loop))
                {
                    string path = _useDigitalMusicFolder ? $"Music/Digital/{name}" : $"Music/{name}";
                    if (!path.EndsWith(".mp3", StringComparison.InvariantCultureIgnoreCase))
                    {
                        path += ".mp3";
                    }

                    music = new UOMusic(index, name, loop, _soundsLoader.FileManager.GetUOFilePath(path));
                }

                return music;
            }

            return null;
        }

        /// <summary>
        /// Gets a sound by filename from the audioassets directory.
        /// Implements caching to avoid reloading the same file multiple times.
        /// </summary>
        /// <param name="fileName">Name of the audio file (e.g., "bow_draw.wav" or "arrow_shot.mp3")</param>
        /// <returns>The Sound instance if found, null otherwise</returns>
        public IO.Audio.Sound GetSoundFromFile(string fileName)
        {
            Console.WriteLine($"Getting sound from file: {fileName} {string.IsNullOrEmpty(fileName)}");
            if (string.IsNullOrEmpty(fileName))
            {
                Console.WriteLine($"Returning null for empty file name");
                return null;
            }

            // Check cache first (case-insensitive)
            if (_soundsFromFile.TryGetValue(fileName, out IO.Audio.Sound cachedSound))
            {
                Console.WriteLine($"Returning cached sound: {cachedSound?.Name}");
                return cachedSound;
            }

            // Try to load from SoundsLoader
            if (_soundsLoader.TryGetSoundFromFile(fileName, out IO.Audio.Sound sound))
            {
                Console.WriteLine($"Loaded sound from file: {fileName} {sound?.Name}");
                // Cache the loaded sound
                _soundsFromFile[fileName] = sound;
                return sound;
            }

            return null;
        }
    }
}
