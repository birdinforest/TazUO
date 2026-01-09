// SPDX-License-Identifier: BSD-2-Clause

using ClassicUO.IO;
using ClassicUO.Utility;
using ClassicUO.Utility.Logging;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Assets
{
    public sealed class GumpsLoader : UOFileLoader
    {
        public const int MAX_GUMP_DATA_INDEX_COUNT = 0x10000;

        // Custom icon range: graphic IDs >= CUSTOM_ICON_START are considered custom icons
        // Graphic ID 0x0000 is used as a placeholder to trigger custom icon loading
        private const ushort CUSTOM_ICON_START = 0x500;
        private const ushort CUSTOM_ICON_PLACEHOLDER = 0x0000;

        // Debug flag: controlled by DEBUG_GUMP_LOADING environment variable
        private static readonly bool _debugGumpLoading =
            System.Environment.GetEnvironmentVariable("DEBUG_GUMP_LOADING")?.ToLower() == "true";

        private UOFile _file;
        // Cache structure: stores both pixels and dimensions for embedded icons
        private struct CachedIcon
        {
            public uint[] Pixels;
            public int Width;
            public int Height;
        }
        private static System.Collections.Generic.Dictionary<ushort, CachedIcon> _customIconCache = new System.Collections.Generic.Dictionary<ushort, CachedIcon>();

        public GumpsLoader(UOFileManager fileManager) : base(fileManager) { }


        public bool UseUOPGumps = false;
        public UOFile File => _file;

        public override void Load()
        {
            string path = FileManager.GetUOFilePath("gumpartLegacyMUL.uop");

            if (FileManager.IsUOPInstallation && System.IO.File.Exists(path))
            {
                _file = new UOFileUop(path, "build/gumpartlegacymul/{0:D8}.tga", true);
                UseUOPGumps = true;
            }
            else
            {
                path = FileManager.GetUOFilePath("gumpart.mul");
                string pathidx = FileManager.GetUOFilePath("gumpidx.mul");

                if (!System.IO.File.Exists(path))
                {
                    path = FileManager.GetUOFilePath("Gumpart.mul");
                }

                if (!System.IO.File.Exists(pathidx))
                {
                    pathidx = FileManager.GetUOFilePath("Gumpidx.mul");
                }

                _file = new UOFileMul(path, pathidx);

                UseUOPGumps = false;
            }

            _file.FillEntries();

            string pathdef = FileManager.GetUOFilePath("gump.def");

            if (!System.IO.File.Exists(pathdef))
            {
                return;
            }

            using (var defReader = new DefReader(pathdef, 3))
            {
                while (defReader.Next())
                {
                    int ingump = defReader.ReadInt();

                    if (
                        ingump < 0
                        || ingump >= MAX_GUMP_DATA_INDEX_COUNT
                        || ingump >= _file.Entries.Length
                        || _file.Entries[ingump].Length > 0
                    )
                    {
                        continue;
                    }

                    int[] group = defReader.ReadGroup();

                    if (group == null)
                    {
                        continue;
                    }

                    for (int i = 0; i < group.Length; i++)
                    {
                        int checkIndex = group[i];

                        if (
                            checkIndex < 0
                            || checkIndex >= MAX_GUMP_DATA_INDEX_COUNT
                            || checkIndex >= _file.Entries.Length
                            || _file.Entries[checkIndex].Length <= 0
                        )
                        {
                            continue;
                        }

                        _file.Entries[ingump] = _file.Entries[checkIndex];
                        _file.Entries[ingump].Hue = (ushort)defReader.ReadInt();

                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Checks if a graphic ID represents a custom icon that should be loaded from external files.
        /// Custom icons are identified by:
        /// 1. Graphic ID 0x0000 (placeholder used in BuffTable to trigger custom loading)
        /// 2. Graphic IDs >= CUSTOM_ICON_START (0x500) that are in the custom icon range
        /// </summary>
        private bool IsCustomIcon(uint graphicId)
        {
            return graphicId == CUSTOM_ICON_PLACEHOLDER || graphicId >= CUSTOM_ICON_START;
        }

        public GumpInfo GetGump(uint index)
        {
            // Check if this is a custom icon that should be loaded from external files
            bool isCustomIcon = IsCustomIcon(index);

            // For custom icons (0x0000 placeholder or >= 0x500), try loading custom icon first
            if (isCustomIcon)
            {
                if (_debugGumpLoading)
                    Log.Debug($"[GumpsLoader] GetGump called for custom icon 0x{index:X4}, attempting to load custom buff icon");

                GumpInfo customGump = TryLoadCustomBuffIcon((ushort)index);
                if (customGump.Width > 0 && customGump.Height > 0)
                {
                    if (_debugGumpLoading)
                        Log.Debug($"[GumpsLoader] Successfully loaded custom icon 0x{index:X4}: {customGump.Width}x{customGump.Height}");
                    return customGump;
                }
                else
                {
                    if (_debugGumpLoading)
                        Log.Debug($"[GumpsLoader] Failed to load custom icon 0x{index:X4} (Width={customGump.Width}, Height={customGump.Height}), will check file entry");
                    // Continue to check file entry as fallback
                }
            }

            // Try to load from standard gump files first
            ref UOFileIndex entry = ref _file.GetValidRefEntry((int)index);

            // If gump file entry is missing or invalid, try custom icon as fallback
            if (entry.CompressionFlag != CompressionType.ZlibBwt && entry.Width <= 0 && entry.Height <= 0)
            {
                // Try to load custom icon as fallback for missing gump
                GumpInfo customGump = TryLoadCustomBuffIcon((ushort)index);
                if (customGump.Width > 0 && customGump.Height > 0)
                {
                    if (_debugGumpLoading)
                        Log.Debug($"[GumpsLoader] Custom icon fallback succeeded for gump 0x{index:X4}: {customGump.Width}x{customGump.Height}");
                    return customGump;
                }
                else
                {
                    if (_debugGumpLoading)
                        Log.Debug($"[GumpsLoader] Custom icon fallback failed for gump 0x{index:X4}");
                }
                return default;
            }

            ushort color = entry.Hue;

            UOFile file = _file;
            if (entry.File != null)
                file = entry.File;

            file.Seek(entry.Offset, SeekOrigin.Begin);

            byte[] buf = new byte[entry.Length];
            file.Read(buf);

            var reader = new StackDataReader(buf);
            uint w = (uint)entry.Width;
            uint h = (uint)entry.Height;

            if (entry.CompressionFlag >= CompressionType.Zlib)
            {
                byte[] dbuf = new byte[entry.DecompressedLength];
                ZLib.ZLibError result = ClassicUO.Utility.ZLib.Decompress(reader.Buffer.Slice(reader.Position), dbuf);
                if (result != Utility.ZLib.ZLibError.Ok)
                {
                    return default;
                }

                if (entry.CompressionFlag == CompressionType.ZlibBwt)
                {
                    dbuf = ClassicUO.Utility.BwtDecompress.Decompress(dbuf);
                }

                reader = new StackDataReader(dbuf);
                w = reader.ReadUInt32LE();
                h = reader.ReadUInt32LE();

                if (entry.Width <= 0)
                    entry.Width = (int)w;
                if (entry.Height <= 0)
                    entry.Height = (int)h;
            }

            Span<uint> pixels = new uint[w * h];
            int len = reader.Remaining;
            int halfLen = len >> 2;

            int start = reader.Position;
            int[] rowLookup = new int[h];
            reader.Read(MemoryMarshal.AsBytes<int>(rowLookup.AsSpan()));

            for (int y = 0; y < h; ++y)
            {
                reader.Seek(start + (rowLookup[y] << 2));
                int pixelIndex = (int)(y * w);
                int gsize = (y < h - 1) ? rowLookup[y + 1] - rowLookup[y] : halfLen - rowLookup[y];
                for (int i = 0; i < gsize; ++i)
                {
                    ushort value = reader.ReadUInt16LE();
                    ushort run = reader.ReadUInt16LE();
                    uint rbga = 0u;

                    if (color != 0 && value != 0)
                    {
                        value = FileManager.Hues.GetColor16(value, color);
                    }

                    if (value != 0)
                    {
                        rbga = HuesHelper.Color16To32(value) | 0xFF_00_00_00;
                    }

                    pixels.Slice(pixelIndex, run).Fill(rbga);
                    pixelIndex += run;
                }
            }

            return new GumpInfo()
            {
                Pixels = pixels,
                Width = (int)w,
                Height = (int)h
            };
        }

        /// <summary>
        /// Tries to load a custom buff icon from gumpartassets/icons/ when a gump is missing.
        /// Uses the graphic ID directly as the filename (e.g., icon-0x7560.png or icon-30048.png).
        ///
        /// Loading order:
        /// 1. Check cache (for embedded resources)
        /// 2. Try embedded resources (from assembly manifest)
        /// 3. Try external files (from executable directory)
        /// </summary>
        private GumpInfo TryLoadCustomBuffIcon(ushort gumpId)
        {
            bool isCustomIcon = IsCustomIcon(gumpId);
            if (_debugGumpLoading)
                Log.Debug($"[GumpsLoader] TryLoadCustomBuffIcon: gumpId={gumpId} (0x{gumpId:X4})");

            // Check cache first (only for embedded resources)
            // External files are not cached to allow hot-swapping during visual testing
            if (_customIconCache.TryGetValue(gumpId, out CachedIcon cachedIcon))
            {
                if (_debugGumpLoading)
                    Log.Debug($"[GumpsLoader] Using cached embedded icon for gumpId 0x{gumpId:X4}: {cachedIcon.Width}x{cachedIcon.Height}");
                // Return cached icon immediately - no need to reload
                return new GumpInfo()
                {
                    Pixels = new System.Span<uint>(cachedIcon.Pixels),
                    Width = cachedIcon.Width,
                    Height = cachedIcon.Height
                };
            }

            // Try to load custom icon using graphic ID directly as filename
            // Support both hexadecimal (0x7560) and decimal (30048) formats, and both .bmp and .png
            string[] filenameFormats = {
                $"icon-0x{gumpId:X4}.png",  // icon-0x7560.png
                $"icon-{gumpId}.png",       // icon-30048.png
                $"icon-0x{gumpId:X4}.bmp",  // icon-0x7560.bmp
                $"icon-{gumpId}.bmp"        // icon-30048.bmp
            };

            string exePath = System.AppContext.BaseDirectory;
            if (_debugGumpLoading)
                Log.Debug($"[GumpsLoader] Executable path: {exePath}");

            string iconPath = null;
            string iconFileName = null;
            Stream embeddedStream = null;

            // First, try embedded resources (from assembly manifest)
            Assembly assembly = typeof(GumpsLoader).Assembly;
            string assemblyName = assembly.GetName().Name;

            // Debug: List all embedded resources containing "icon" or "gumpartassets" to help diagnose
            bool debugLogged = false;
            foreach (string filename in filenameFormats)
            {
                iconFileName = filename;
                try
                {
                    // Try both forward slash and backslash path separators (Windows vs Unix)
                    string[] resourcePaths = {
                        $"{assemblyName}.gumpartassets.icons.{iconFileName}",
                        $"{assemblyName}.gumpartassets\\icons.{iconFileName}",
                        $"{assemblyName}.gumpartassets/icons.{iconFileName}"
                    };

                    foreach (string resourcePath in resourcePaths)
                    {
                        if (!debugLogged)
                        {
                            // Log all embedded resources containing "icon" or "gumpartassets" once
                            string[] allResources = assembly.GetManifestResourceNames();
                            var relevantResources = System.Array.FindAll(allResources, r =>
                                r.Contains("icon", StringComparison.OrdinalIgnoreCase) ||
                                r.Contains("gumpartassets", StringComparison.OrdinalIgnoreCase));
                            if (relevantResources.Length > 0 && _debugGumpLoading)
                            {
                                Log.Debug($"[GumpsLoader] Available embedded resources (icon/gumpartassets): {string.Join(", ", relevantResources)}");
                            }
                            debugLogged = true;
                        }

                        if (_debugGumpLoading)
                            Log.Debug($"[GumpsLoader] Checking embedded resource: {resourcePath}");
                        embeddedStream = assembly.GetManifestResourceStream(resourcePath);
                        if (embeddedStream != null)
                        {
                            if (_debugGumpLoading)
                                Log.Debug($"[GumpsLoader] Found embedded resource: {resourcePath}");
                            break;
                        }
                    }

                    if (embeddedStream != null)
                        break;
                }
                catch (Exception ex)
                {
                    if (_debugGumpLoading)
                        Log.Debug($"[GumpsLoader] Error checking embedded resource {iconFileName}: {ex.Message}");
                }
            }

            // If embedded resource found, use it; otherwise check external files
            if (embeddedStream == null)
            {
                foreach (string filename in filenameFormats)
                {
                    iconFileName = filename;
                    string embeddedPath = System.IO.Path.Combine(exePath, "gumpartassets", "icons", iconFileName);
                    string externalPath = System.IO.Path.Combine(exePath, "ExternalImages", "icons", iconFileName);

                    if (_debugGumpLoading)
                    {
                        Log.Debug($"[GumpsLoader] Checking external file: {embeddedPath} (exists: {System.IO.File.Exists(embeddedPath)})");
                        Log.Debug($"[GumpsLoader] Checking external file: {externalPath} (exists: {System.IO.File.Exists(externalPath)})");
                    }

                    if (System.IO.File.Exists(embeddedPath))
                    {
                        iconPath = embeddedPath;
                        if (_debugGumpLoading)
                            Log.Debug($"[GumpsLoader] Found icon at external path: {iconPath}");
                        break;
                    }
                    else if (System.IO.File.Exists(externalPath))
                    {
                        iconPath = externalPath;
                        if (_debugGumpLoading)
                            Log.Debug($"[GumpsLoader] Found icon at external path: {iconPath}");
                        break;
                    }
                }

                if (iconPath == null)
                {
                    if (_debugGumpLoading)
                        Log.Debug($"[GumpsLoader] Icon file not found. Tried: {string.Join(", ", filenameFormats)}");
                    return default;
                }
            }

            var pngLoader = PNGLoader.Instance;
            if (pngLoader == null)
            {
                if (_debugGumpLoading)
                    Log.Debug($"[GumpsLoader] PNGLoader.Instance is null");
                if (embeddedStream != null) embeddedStream.Dispose();
                return default;
            }

            if (pngLoader.GraphicsDevice == null)
            {
                if (_debugGumpLoading)
                    Log.Debug($"[GumpsLoader] PNGLoader.GraphicsDevice is null");
                if (embeddedStream != null) embeddedStream.Dispose();
                return default;
            }

            Texture2D texture = null;

            // Load from embedded stream or external file
            if (embeddedStream != null)
            {
                try
                {
                    if (_debugGumpLoading)
                        Log.Debug($"[GumpsLoader] Loading texture from embedded stream: {iconFileName}");
                    texture = Texture2D.FromStream(pngLoader.GraphicsDevice, embeddedStream);
                    if (texture != null)
                    {
                        // Fix alpha channel
                        var buffer = new Color[texture.Width * texture.Height];
                        texture.GetData(buffer);
                        for (int i = 0; i < buffer.Length; i++)
                            buffer[i] = Color.FromNonPremultiplied(buffer[i].R, buffer[i].G, buffer[i].B, buffer[i].A);
                        texture.SetData(buffer);
                        if (_debugGumpLoading)
                            Log.Debug($"[GumpsLoader] Successfully loaded texture from embedded stream: {texture.Width}x{texture.Height}");
                    }
                }
                catch (Exception ex)
                {
                    Log.Warn($"[GumpsLoader] Failed to load texture from embedded stream: {ex.Message}");
                }
                finally
                {
                    embeddedStream.Dispose();
                }
            }
            else if (iconPath != null)
            {
                if (_debugGumpLoading)
                    Log.Debug($"[GumpsLoader] Loading texture from external file: {iconPath}");
                texture = pngLoader.GetImageTexture(iconPath);
                if (texture != null && _debugGumpLoading)
                {
                    Log.Debug($"[GumpsLoader] Successfully loaded texture from external file: {texture.Width}x{texture.Height}");
                }
            }

            if (texture == null)
            {
                if (_debugGumpLoading || isCustomIcon)
                {
                    Log.Warn($"[GumpsLoader] Failed to load custom icon for gumpId 0x{gumpId:X4}. Tried files: {string.Join(", ", filenameFormats)}");
                    if (gumpId == CUSTOM_ICON_PLACEHOLDER)
                    {
                        Log.Warn($"[GumpsLoader] Expected icon file: icon-0x0000.png in gumpartassets/icons/ (or embedded as resource)");
                    }
                }
                return default;
            }

            if (texture.IsDisposed)
            {
                if (_debugGumpLoading)
                    Log.Debug($"[GumpsLoader] Texture is disposed");
                return default;
            }

            if (_debugGumpLoading || isCustomIcon)
            {
                Log.Debug($"[GumpsLoader] Texture loaded successfully for gumpId 0x{gumpId:X4}: {texture.Width}x{texture.Height} from {(embeddedStream != null ? "embedded resource" : iconPath ?? "unknown")}");
            }

            // Convert Texture2D to GumpInfo pixel data
            int width = texture.Width;
            int height = texture.Height;
            Color[] colors = new Color[width * height];
            texture.GetData(colors);

            // Create pixel array
            uint[] pixels = new uint[width * height];
            for (int i = 0; i < colors.Length; i++)
            {
                pixels[i] = colors[i].PackedValue;
            }

            // Only cache embedded resources, not external files
            // This allows hot-swapping external images for visual testing
            bool isExternalFile = (embeddedStream == null && iconPath != null);
            if (!isExternalFile)
            {
                // Cache embedded resources with dimensions so they can be reused
                _customIconCache[gumpId] = new CachedIcon
                {
                    Pixels = pixels,
                    Width = width,
                    Height = height
                };
                if (_debugGumpLoading)
                    Log.Debug($"[GumpsLoader] Cached embedded icon for gumpId {gumpId}: {width}x{height}");
            }
            else
            {
                if (_debugGumpLoading)
                    Log.Debug($"[GumpsLoader] Not caching external file icon for gumpId {gumpId} (allows hot-swapping for testing)");
            }

            if (_debugGumpLoading)
                Log.Debug($"[GumpsLoader] Successfully created GumpInfo: {width}x{height}, {pixels.Length} pixels");

            return new GumpInfo()
            {
                Pixels = new System.Span<uint>(pixels),
                Width = width,
                Height = height
            };
        }


    }

    public ref struct GumpInfo
    {
        public Span<uint> Pixels;
        public int Width;
        public int Height;
    }
}
