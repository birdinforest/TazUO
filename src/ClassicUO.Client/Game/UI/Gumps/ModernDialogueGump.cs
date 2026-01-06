using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text;
using ClassicUO.Assets;
using ClassicUO.Configuration;
using ClassicUO.Game;
using ClassicUO.Game.Data;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Input;
using ClassicUO.Network;
using ClassicUO.Renderer;
using ClassicUO.Utility.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.UI.Gumps;

/// <summary>
/// Modern Dialogue Gump - Displays NPC dialogue with portraits using modern UI components
/// Singleton pattern: Only one instance exists at a time, reused for all dialogue updates
/// </summary>
public class ModernDialogueGump : Gump
{
    // Singleton instance
    private static ModernDialogueGump s_Instance = null;
    private static readonly object s_Lock = new object();

    /// <summary>
    /// Get or create the singleton instance
    /// </summary>
    public static ModernDialogueGump GetOrCreateInstance(World world, ModernDialogueGumpData data)
    {
        lock (s_Lock)
        {
            // If instance exists and is not disposed, reuse it
            if (s_Instance != null && !s_Instance.IsDisposed)
            {
                Log.Info("[ModernDialogueGump] Reusing existing singleton instance");
                return s_Instance;
            }

            // Create new instance
            Log.Info("[ModernDialogueGump] Creating new singleton instance");
            s_Instance = new ModernDialogueGump(world, data);
            return s_Instance;
        }
    }

    /// <summary>
    /// Get the current singleton instance (may be null or disposed)
    /// </summary>
    public static ModernDialogueGump Instance
    {
        get
        {
            lock (s_Lock)
            {
                if (s_Instance != null && s_Instance.IsDisposed)
                {
                    // Clear disposed instance
                    s_Instance = null;
                }
                return s_Instance;
            }
        }
    }

    private ModernDialogueGumpData m_Data;
    private Texture2D m_PortraitTexture;
    private const int PADDING = 30;
    private const int PORTRAIT_SIZE = 128;
    private const int BUTTON_HEIGHT = 30; // Traditional UO buttons are more compact
    private const int BUTTON_SPACING = 8;
    private const int TEXT_SPACING = 15;
    private const int BUTTON_SHADOW_OFFSET = 2; // Shadow offset for traditional UO look

    // Cache for embedded portrait textures (not external files, to allow hot-swapping during development)
    private static System.Collections.Generic.Dictionary<string, Texture2D> s_PortraitCache =
        new System.Collections.Generic.Dictionary<string, Texture2D>();

    // Base font sizes (for English)
    private const float TITLE_FONT_SIZE = 26f;
    private const float DIALOGUE_FONT_SIZE = 22f;
    private const float BUTTON_FONT_SIZE = 20f;

    // Font size scaling factors for different languages
    // Chinese fonts often need to be larger to be readable
    private const float CHINESE_FONT_SCALE = 1f; // 20% larger for Chinese

    private const int GUMAP_BACKGOUND_TEXTURE = 0x0A28;
    private const int OPTIONS_BACKGOUND_TEXTURE = 0xA2C;

    private int x = Constants.MODERN_DIALOGUE_GUMP_INITIAL_LOCATION_X;
    private int y = Constants.MODERN_DIALOGUE_GUMP_INITIAL_LOCATION_Y;

    /// <summary>
    /// Get scaled font size based on language
    /// Chinese fonts typically need to be larger for readability
    /// </summary>
    private float GetScaledFontSize(float baseSize, string languageCode)
    {
        if (string.IsNullOrEmpty(languageCode))
            return baseSize;

        string lang = languageCode.ToLower();
        if (lang == "zh" || lang == "zh-cn" || lang == "zh-tw" || lang == "zh-hans" || lang == "zh-hant")
        {
            return baseSize * CHINESE_FONT_SCALE;
        }

        return baseSize;
    }

    /// <summary>
    /// Calculate adjusted text width for Chinese characters
    /// Chinese characters are typically wider, so we need more space
    /// </summary>
    private static int GetAdjustedTextWidth(string text, string languageCode, int baseWidth)
    {
        languageCode = languageCode.ToLower();
        if (languageCode.Contains("zh"))
        {
            // Chinese characters are approximately 1.5-2x wider than English characters
            // Add extra width based on the number of Chinese characters
            int chineseCharCount = 0;
            foreach (char c in text)
            {
                if (c >= 0x4E00 && c <= 0x9FFF)
                    chineseCharCount++;
            }
            // Add extra width: approximately 10 pixels per Chinese character
            return baseWidth + (chineseCharCount * 20);
        }
        return baseWidth;
    }

    /// <summary>
    /// Select appropriate font based on language code
    /// Returns font name that supports the language, with fallback to embedded font
    /// </summary>
    private static string GetFontForLanguage(string languageCode)
    {
        if (string.IsNullOrEmpty(languageCode))
            return TrueTypeLoader.EMBEDDED_FONT;

        string lang = languageCode.ToLower();

        // Check if a Chinese-supporting font is available
        if (lang == "zh" || lang == "zh-cn" || lang == "zh-tw" || lang == "zh-hans" || lang == "zh-hant")
        {
            return "AlibabaPuHuiTi-3-55-Regular";
        }

        return "Kingthings Exeter";
    }

    /// <summary>
    /// Private constructor - use GetOrCreateInstance() to get the singleton
    /// </summary>
    private ModernDialogueGump(World world, ModernDialogueGumpData data)
        : base(world, (uint)data.GumpSerial, (uint)data.GumpTypeID)
    {
        m_Data = data;
        CanMove = true;
        CanCloseWithRightClick = true;
        AcceptMouseInput = true;

        // Try to load saved position from profile
        if (ProfileManager.CurrentProfile != null)
        {
            Point savedPosition = ProfileManager.CurrentProfile.DialogueGumpPosition;
            X = savedPosition.X;
            Y = savedPosition.Y;
            this.x = savedPosition.X;
            this.y = savedPosition.Y;
            Log.Info($"[ModernDialogueGump] Loaded saved position from profile: X={X}, Y={Y}");
        }
        else
        {
            // Use default position from constants if profile not available
            X = Constants.MODERN_DIALOGUE_GUMP_INITIAL_LOCATION_X;
            Y = Constants.MODERN_DIALOGUE_GUMP_INITIAL_LOCATION_Y;
            this.x = X;
            this.y = Y;
            Log.Info($"[ModernDialogueGump] Profile not available, using default position: X={X}, Y={Y}");
        }

        // Width and Height will be calculated after building the gump
        Width = 0;
        Height = 0;

        // Log received data
        Log.Info($"[ModernDialogueGump] Constructor START -------------------------------");
        Log.Info($"[ModernDialogueGump] Constructor - Npc Name: {m_Data.NpcName}");
        Log.Info($"[ModernDialogueGump] Constructor - Npc Title: {m_Data.NpcTitle}");
        Log.Info($"[ModernDialogueGump] Constructor - Portrait File Name: {m_Data.PortraitFileName}");
        Log.Info($"[ModernDialogueGump] Constructor - Portrait Size: {m_Data.PortraitSize}");
        Log.Info($"[ModernDialogueGump] Constructor - Dialogue Text: {m_Data.DialogueText}");
        Log.Info($"[ModernDialogueGump] Constructor - Current Node Id: {m_Data.CurrentNodeId}");
        Log.Info($"[ModernDialogueGump] Constructor - Has Back Button: {m_Data.HasBackButton}");
        Log.Info($"[ModernDialogueGump] Constructor - Back Button Text: {m_Data.BackButtonText}");
        Log.Info($"[ModernDialogueGump] Constructor - Options count: {m_Data.Options?.Count ?? 0}");
        Log.Info($"[ModernDialogueGump] Constructor - Language Code: {m_Data.LanguageCode}");

        if (m_Data.Options != null)
        {
            for (int i = 0; i < m_Data.Options.Count; i++)
            {
                var opt = m_Data.Options[i];
                Log.Info($"[ModernDialogueGump] Constructor - Option {i}: Id={opt.Id}, Text='{opt.Text}', NextNodeId='{opt.NextNodeId}'");
            }
        }
        Log.Info($"[ModernDialogueGump] Constructor END --------------------------------");

        BuildGump();

        // Calculate width and height after building all UI elements
        CalculateAndSetSize();

        // Ensure position is within window bounds after initial size calculation
        ClampToWindowBounds();
    }

    /// <summary>
    /// Update gump content with new data from server
    /// </summary>
    public void UpdateContent(ModernDialogueGumpData newData)
    {
        if (newData == null)
        {
            Log.Warn("[ModernDialogueGump] UpdateContent called with null data");
            return;
        }

        Log.Info($"[ModernDialogueGump] Updating content - NodeId: {newData.CurrentNodeId}, Options: {newData.Options?.Count ?? 0}");

        // Preserve current position (user may have moved the gump)
        int savedX = X;
        int savedY = Y;

        // Update data
        m_Data = newData;

        // Update serials if they changed (server may create new gump)
        if (newData.GumpSerial != 0)
        {
            LocalSerial = (uint)newData.GumpSerial;
        }
        if (newData.GumpTypeID != 0)
        {
            ServerSerial = (uint)newData.GumpTypeID;
        }

        // Reset size before clearing (prevents accumulation)
        Width = 0;
        Height = 0;

        // Clear existing children (dispose them)
        // Note: Clear() disposes children but doesn't remove them from the list
        // So we need to manually remove them to prevent duplicates
        // Iterate backwards to safely remove items during iteration
        for (int i = Children.Count - 1; i >= 0; i--)
        {
            var child = Children[i];
            if (child != null)
            {
                Remove(child);
                child.Dispose();
            }
        }

        // Clear portrait texture reference (will be reloaded if needed)
        m_PortraitTexture = null;

        // Rebuild gump with new content
        BuildGump();

        // Recalculate size
        CalculateAndSetSize();

        // Restore position (preserve user's drag position)
        X = savedX;
        Y = savedY;
        this.x = savedX;
        this.y = savedY;

        // Ensure position is still within window bounds after size change
        ClampToWindowBounds();

        // Save position to profile for next time
        if (ProfileManager.CurrentProfile != null)
        {
            ProfileManager.CurrentProfile.DialogueGumpPosition = new Point(X, Y);
            Log.Info($"[ModernDialogueGump] Saved position to profile: X={X}, Y={Y}");
        }

        Log.Info($"[ModernDialogueGump] Content updated - New size: Width={Width}, Height={Height}, Position: X={X}, Y={Y}");
    }

    private void BuildGump()
    {
        // Use a default width for initial layout calculations
        // Will be recalculated after all elements are added
        const int DEFAULT_WIDTH = 600;
        const int DEFAULT_HEIGHT = 100; // Minimum height to ensure background renders

        // Reset size to defaults before building
        Width = DEFAULT_WIDTH;
        Height = DEFAULT_HEIGHT;

        // Ensure no existing background before creating a new one
        // Clear() disposes children but doesn't remove them from the list
        // So we need to explicitly remove any existing ResizePic backgrounds
        for (int i = Children.Count - 1; i >= 0; i--)
        {
            if (Children[i] is ResizePic bg && bg.Graphic == GUMAP_BACKGOUND_TEXTURE)
            {
                Remove(bg);
                bg.Dispose();
            }
        }

        var background = new ResizePic(GUMAP_BACKGOUND_TEXTURE)
        {
            Width = Width,
            Height = Height
        };
        Add(background);

        // Title (NPC name) - centered at top
        string titleFont = GetFontForLanguage(m_Data.LanguageCode);
        float titleSize = GetScaledFontSize(TITLE_FONT_SIZE, m_Data.LanguageCode);
        var title = TextBox.GetOne(
            m_Data.NpcTitle,
            titleFont,
            titleSize,
            Color.Gold,
            TextBox.RTLOptions.Default(Width - (PADDING * 2))
        );
        title.X = PADDING + 20;
        title.Y = PADDING + 10;
        Add(title);

        // Separator line
        var separator = new SimpleBorder
        {
            X = PADDING,
            Y = PADDING + 35,
            Width = Width - (PADDING * 2),
            Height = 2,
            Hue = 0x3B2 // Gray color (hue value)
        };
        Add(separator);

        // Load NPC Portrait
        int portraitX = PADDING;
        int portraitY = PADDING + 50;

        if (!string.IsNullOrEmpty(m_Data.PortraitFileName))
        {
            // Log.Info($"[ModernDialogueGump] Attempting to load portrait: '{m_Data.PortraitFileName}'");

            // PNGLoader stores embedded resources with dot separators (e.g., "npc.lord-of-britain-256-256.png")
            // but the server sends paths with forward slashes (e.g., "npc/lord-of-britain-256-256.png")
            // Try multiple path formats
            string[] embeddedPaths = {
                m_Data.PortraitFileName,                              // Original: "npc/lord-of-britain-256-256.png"
                m_Data.PortraitFileName.Replace("/", "."),            // Dots: "npc.lord-of-britain-256-256.png" (PNGLoader format)
                m_Data.PortraitFileName.Replace("\\", "."),           // Backslashes to dots
                Path.GetFileName(m_Data.PortraitFileName),            // Just filename: "lord-of-britain-256-256.png"
            };

            // Try to load portrait from embedded resources first (with caching)
            bool foundEmbedded = false;

            foreach (string embeddedPath in embeddedPaths)
            {
                if (s_PortraitCache.TryGetValue(embeddedPath, out Texture2D cachedTexture))
                {
                    if (cachedTexture != null && !cachedTexture.IsDisposed &&
                        (cachedTexture.Width > 1 || cachedTexture.Height > 1))
                    {
                        m_PortraitTexture = cachedTexture;
                        foundEmbedded = true;
                        break;
                    }
                    else
                    {
                        s_PortraitCache.Remove(embeddedPath);
                        Log.Warn($"[ModernDialogueGump] Removed invalid cached texture for: '{embeddedPath}'");
                    }
                }

                // Try to load from embedded resources
                if (PNGLoader.Instance.TryGetEmbeddedTexture(embeddedPath, out Texture2D embeddedTexture))
                {
                    // Check if we got a valid texture (not the empty placeholder)
                    // The empty texture is 1x1 transparent, so check dimensions
                    if (embeddedTexture != null && (embeddedTexture.Width > 1 || embeddedTexture.Height > 1))
                    {
                        m_PortraitTexture = embeddedTexture;
                        foundEmbedded = true;

                        // Cache the embedded texture for future use
                        s_PortraitCache[embeddedPath] = embeddedTexture;
                        Log.Info($"[ModernDialogueGump] Successfully loaded and cached embedded portrait: '{embeddedPath}' (Size: {embeddedTexture.Width}x{embeddedTexture.Height})");
                        break;
                    }
                    else
                    {
                        Log.Warn($"[ModernDialogueGump] Embedded texture returned empty/placeholder texture for: '{embeddedPath}' (Size: {embeddedTexture?.Width}x{embeddedTexture?.Height})");
                    }
                }
                else
                {
                    Log.Warn($"[ModernDialogueGump] Embedded texture not found for: '{embeddedPath}'");
                }
            }

            // Debug: List available embedded resources if all attempts failed
            if (!foundEmbedded)
            {
                Log.Warn($"[ModernDialogueGump] All embedded portrait paths failed. Listing available embedded resources containing 'npc', 'lord', or 'portrait':");
                try
                {
                    System.Reflection.Assembly assembly = typeof(ClassicUO.Assets.PNGLoader).Assembly;
                    string[] allResources = assembly.GetManifestResourceNames();
                    var relevantResources = System.Array.FindAll(allResources, r =>
                        (r.Contains("npc", StringComparison.OrdinalIgnoreCase) ||
                         r.Contains("portrait", StringComparison.OrdinalIgnoreCase) ||
                         r.Contains("lord", StringComparison.OrdinalIgnoreCase)) &&
                        r.EndsWith(".png", StringComparison.OrdinalIgnoreCase));

                    if (relevantResources.Length > 0)
                    {
                        foreach (string resource in relevantResources)
                        {
                            // Extract the key that would be used in EmbeddedArt dictionary
                            string key = resource;
                            string prefix = assembly.GetName().Name + ".gumpartassets.";
                            if (key.StartsWith(prefix))
                            {
                                key = key.Substring(prefix.Length);
                            }
                            Log.Info($"[ModernDialogueGump]   Resource: {resource}");
                            Log.Info($"[ModernDialogueGump]   -> EmbeddedArt key would be: '{key}'");
                        }
                    }
                    else
                    {
                        Log.Warn($"[ModernDialogueGump]   No NPC/portrait/lord resources found in embedded resources");
                        Log.Info($"[ModernDialogueGump]   Total embedded resources: {allResources.Length}");
                        if (allResources.Length > 0 && allResources.Length <= 20)
                        {
                            Log.Info($"[ModernDialogueGump]   All embedded resources:");
                            foreach (string res in allResources)
                            {
                                Log.Info($"[ModernDialogueGump]     - {res}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warn($"[ModernDialogueGump] Error listing embedded resources: {ex.Message}");
                }
            }

            // If embedded failed, try external files
            if (!foundEmbedded)
            {
                string exePath = System.AppContext.BaseDirectory;
                string originalPath = m_Data.PortraitFileName;

                // Try in gumpartassets directory (matches embedded structure)
                string externalPath1 = Path.Combine(exePath, "gumpartassets", originalPath);

                if (File.Exists(externalPath1))
                {
                    m_PortraitTexture = PNGLoader.Instance.GetImageTexture(externalPath1);
                    if (m_PortraitTexture != null)
                    {
                        Log.Info($"[ModernDialogueGump] Successfully loaded external portrait: '{externalPath1}' (Size: {m_PortraitTexture.Width}x{m_PortraitTexture.Height})");
                    }
                }
                else
                {
                    // Try with just filename (in case directory structure doesn't match)
                    string fileName = Path.GetFileName(originalPath);
                    string externalPath1b = Path.Combine(exePath, "gumpartassets", "npc", fileName);
                    Log.Info($"[ModernDialogueGump] Trying external file path 1b: '{externalPath1b}'");

                    if (File.Exists(externalPath1b))
                    {
                        m_PortraitTexture = PNGLoader.Instance.GetImageTexture(externalPath1b);
                        if (m_PortraitTexture != null)
                        {
                            Log.Info($"[ModernDialogueGump] Successfully loaded external portrait: '{externalPath1b}' (Size: {m_PortraitTexture.Width}x{m_PortraitTexture.Height})");
                        }
                    }
                }

                if (m_PortraitTexture == null)
                {
                    // Try in ExternalImages directory (ClassicUO standard)
                    string externalPath2 = Path.Combine(exePath, "ExternalImages", originalPath);
                    Log.Info($"[ModernDialogueGump] Trying external file path 2: '{externalPath2}'");

                    if (File.Exists(externalPath2))
                    {
                        m_PortraitTexture = PNGLoader.Instance.GetImageTexture(externalPath2);
                        if (m_PortraitTexture != null)
                        {
                            Log.Info($"[ModernDialogueGump] Successfully loaded external portrait: '{externalPath2}' (Size: {m_PortraitTexture.Width}x{m_PortraitTexture.Height})");
                        }
                    }
                }
            }
        }

        int portraitSize = 0;
        int textX = PADDING;
        int textWidth = (Width - (PADDING * 2));
        int textY = PADDING + 50;

        // Display portrait if loaded
        if (m_PortraitTexture != null)
        {
            portraitSize = Math.Min(m_Data.PortraitSize, PORTRAIT_SIZE);
            textX = (portraitX + portraitSize + TEXT_SPACING);
            textWidth = (Width - textX - PADDING);
            textY = portraitY;
            var portrait = new EmbeddedGumpPic(portraitX, portraitY, m_PortraitTexture, 0);
            portrait.Width = portraitSize;
            portrait.Height = portraitSize;
            Add(portrait);
        }
        else
        {
            Log.Warn($"[ModernDialogueGump] Failed to load portrait: '{m_Data.PortraitFileName}'. Portrait will not be displayed.");
        }

        string dialogueFont = GetFontForLanguage(m_Data.LanguageCode);
        float dialogueSize = GetScaledFontSize(DIALOGUE_FONT_SIZE, m_Data.LanguageCode);
        var dialogueOptions = TextBox.RTLOptions.Default(textWidth);

        var dialogueText = TextBox.GetOne(
            m_Data.DialogueText,
            dialogueFont,
            dialogueSize,
            Color.White,
            dialogueOptions
        );
        dialogueText.X = textX;
        dialogueText.Y = textY;
        dialogueText.MultiLine = true;
        Add(dialogueText);

        // Calculate starting Y position for options (below portrait and text)
        int portraitSizeForLayout = m_PortraitTexture != null ? Math.Min(m_Data.PortraitSize, PORTRAIT_SIZE) : 0;
        int contentHeight = m_PortraitTexture != null ? Math.Max(portraitSizeForLayout, dialogueText.Height) : dialogueText.Height;
        int optionY = portraitY + contentHeight + TEXT_SPACING + 10;

        // Options (buttons)
        if (m_Data.Options == null)
        {
            Log.Warn("[ModernDialogueGump] Options list is null! Creating empty list.");
            m_Data.Options = new List<ModernDialogueOptionData>();
        }

        if (m_Data.Options.Count > 0)
        {
            foreach (var option in m_Data.Options)
            {
                // Traditional UO button - using GumpPicTiled for simple tiled background
                // GumpPicTiled tiles a single texture without 9-slice corner logic
                // This is appropriate when the texture has no corner patterns (like 0xA2C)
                var buttonBg = new GumpPicTiled(textX, optionY, textWidth - 20, BUTTON_HEIGHT, OPTIONS_BACKGOUND_TEXTURE);
                Add(buttonBg);

                // Create a clickable area using HitBox (invisible but clickable)
                var buttonHitBox = new HitBox(textX, optionY, textWidth - 20, BUTTON_HEIGHT)
                {
                    LocalSerial = (uint)option.Id
                };
                buttonHitBox.MouseUp += (sender, e) => OnOptionClick(option);
                Add(buttonHitBox);

                // Button text - add after button so it renders on top
                string optionTextToDisplay = option.Text ?? "";

                // Adjust text width for Chinese characters
                string optionFont = GetFontForLanguage(m_Data.LanguageCode);
                float optionSize = GetScaledFontSize(BUTTON_FONT_SIZE, m_Data.LanguageCode);
                var optionOptions = TextBox.RTLOptions.Default(textWidth - 40);

                // Traditional UO button text color - aligned with traditional UO gump theme
                // Using dark brown/black color for readability on buttons (matches traditional UO button text)
                var buttonText = TextBox.GetOne(
                    optionTextToDisplay,
                    optionFont,
                    optionSize,
                    new Color(0x3A, 0x2A, 0x1A), // Dark brown/black color (traditional UO button text - hue 0x0386 equivalent, darker for buttons)
                    optionOptions
                );
                buttonText.X = textX + 15; // Position text on button background
                buttonText.Y = optionY + 8; // Center text vertically in button
                buttonText.AcceptMouseInput = false; // Allow clicks to pass through to button
                Add(buttonText);
                optionY += BUTTON_HEIGHT + BUTTON_SPACING;
            }
        }
        else
        {
            Log.Warn("[ModernDialogueGump] No options to display!");
        }

        // Back button (if available)
        if (m_Data.HasBackButton)
        {
            // Traditional UO button background - using GumpPicTiled for simple tiled background
            // GumpPicTiled tiles a single texture without 9-slice corner logic
            var backButtonBg = new GumpPicTiled(textX, optionY, 120, BUTTON_HEIGHT, OPTIONS_BACKGOUND_TEXTURE);
            Add(backButtonBg);

            // Create a clickable area using HitBox (invisible but clickable)
            var backButtonHitBox = new HitBox(textX, optionY, 120, BUTTON_HEIGHT)
            {
                LocalSerial = 1 // BUTTON_BACK
            };
            backButtonHitBox.MouseUp += (sender, e) => OnBackClick();
            Add(backButtonHitBox);

            string backFont = GetFontForLanguage(m_Data.LanguageCode);
            float backSize = GetScaledFontSize(BUTTON_FONT_SIZE, m_Data.LanguageCode);
            var backOptions = TextBox.RTLOptions.Default(100);

            // Traditional UO button text color - aligned with traditional UO gump theme
            var backText = TextBox.GetOne(
                m_Data.BackButtonText,
                backFont,
                backSize,
                new Color(0x3A, 0x2A, 0x1A), // Dark brown/black color (traditional UO button text - hue 0x0386 equivalent, darker for buttons)
                backOptions
            );
            backText.X = textX + 15; // Position text on button background
            backText.Y = optionY + 8; // Center text vertically in button
            backText.AcceptMouseInput = false; // Allow clicks to pass through to button
            Add(backText);
            optionY += BUTTON_HEIGHT + BUTTON_SPACING;
        }
    }

    /// <summary>
    /// Calculate and set the actual width and height based on rendered content
    /// </summary>
    private void CalculateAndSetSize()
    {
        int maxX = 0;
        int maxY = 0;

        // Find the rightmost and bottommost elements
        // IMPORTANT: Skip the background ResizePic (first child) as it will resize to match content
        // Including it would cause circular dependency (background size depends on content, content size includes background)
        foreach (var control in Children)
        {
            // Skip the background ResizePic as it will resize to match
            if (control is ResizePic && control == Children[0])
                continue;

            int right = control.X + control.Width;
            int bottom = control.Y + control.Height;

            if (right > maxX)
                maxX = right;
            if (bottom > maxY)
                maxY = bottom;
        }

        // Calculate width: use the maximum content width or minimum width, whichever is larger
        int calculatedWidth = Math.Max(maxX + PADDING, 500); // Minimum width of 500px

        // Calculate height: add bottom padding
        int calculatedHeight = maxY + PADDING;

        // Update gump size
        Width = calculatedWidth;
        Height = calculatedHeight;

        // Update background to match new size
        // Find the background ResizePic (should be first child)
        if (Children.Count > 0)
        {
            var background = Children[0] as ResizePic;
            if (background != null)
            {
                // Simply update the existing background's size
                // ResizePic uses Width/Height directly in DrawInternal, so updating should work
                background.Width = Width;
                background.Height = Height;

                Log.Info($"[ModernDialogueGump] Background updated: Width={background.Width}, Height={background.Height}");
            }
            else
            {
                Log.Warn($"[ModernDialogueGump] First child is not a ResizePic! Type: {Children[0]?.GetType().Name}");
            }
        }
        else
        {
            Log.Warn("[ModernDialogueGump] No children found when trying to update background!");
        }

        Log.Info($"[ModernDialogueGump] Calculated size: Width={Width}, Height={Height} (maxX={maxX}, maxY={maxY})");
    }

    private void OnOptionClick(ModernDialogueOptionData option)
    {
        // Send gump response to server
        // Server will send new content via enhanced packet, which will update this gump
        SendGumpResponse(option.Id);
        // Don't dispose - wait for server to send updated content
    }

    private void OnBackClick()
    {
        // Send back button response
        // Server will send new content via enhanced packet, which will update this gump
        SendGumpResponse(1); // BUTTON_BACK
        // Don't dispose - wait for server to send updated content
    }

    private void SendGumpResponse(int buttonId)
    {
        // Send standard gump response packet (0xB1)
        // The server's DialogueGump.OnResponse() will handle this
        // Use the gump serial and type ID from the server
        AsyncNetClient.Socket.Send_GumpResponse(
            World,
            LocalSerial,
            ServerSerial,
            buttonId,
            Array.Empty<uint>(), // No switches
            Array.Empty<Tuple<ushort, string>>() // No text entries
        );
    }

    public override void Dispose()
    {
        // Send close response to server when user closes the gump
        // Button ID 0 typically means close/cancel
        if (World != null && LocalSerial != 0 && ServerSerial != 0)
        {
            try
            {
                SendGumpResponse(0); // 0 = Close button
                Log.Info($"[ModernDialogueGump] Sent close response to server (Serial: {LocalSerial}, ServerSerial: {ServerSerial})");
            }
            catch (Exception ex)
            {
                Log.Warn($"[ModernDialogueGump] Error sending close response: {ex.Message}");
            }
        }

        // Clear singleton instance when disposed
        lock (s_Lock)
        {
            if (s_Instance == this)
            {
                s_Instance = null;
                Log.Info("[ModernDialogueGump] Singleton instance cleared on dispose");
            }
        }

        // Note: We don't dispose m_PortraitTexture as it may be cached/shared
        // The PNGLoader manages texture lifetime
        base.Dispose();
    }

    /// <summary>
    /// Clamp gump position to ensure it stays within window bounds
    /// </summary>
    private void ClampToWindowBounds()
    {
        if (Client.Game?.Window == null)
            return;

        Rectangle windowBounds = Client.Game.Window.ClientBounds;

        // Ensure gump doesn't go off-screen
        // At least 100px of the gump should be visible on each edge
        // minX: allows gump to be mostly off left, but 100px visible on right
        // maxX: allows gump to be mostly off right, but 100px visible on left
        int minX = -Width + 100;
        int minY = -Height + 100;
        int maxX = windowBounds.Width - 100; // At least 100px should be visible on right
        int maxY = windowBounds.Height - 100; // At least 100px should be visible on bottom

        // Clamp position
        int clampedX = Math.Clamp(X, minX, maxX);
        int clampedY = Math.Clamp(Y, minY, maxY);

        if (clampedX != X || clampedY != Y)
        {
            Log.Info($"[ModernDialogueGump] Clamped position from ({X}, {Y}) to ({clampedX}, {clampedY}) to fit window bounds");
            X = clampedX;
            Y = clampedY;
            this.x = clampedX;
            this.y = clampedY;
        }
    }

    protected override void OnDragEnd(int x, int y)
    {
        // Note: x and y are relative coordinates, not absolute
        // The base class has already updated X and Y to the correct absolute position
        // We just need to save the current position and clamp it if needed

        // Clamp to window bounds to ensure gump stays visible
        ClampToWindowBounds();

        // Save position to profile for next time (after clamping)
        if (ProfileManager.CurrentProfile != null)
        {
            ProfileManager.CurrentProfile.DialogueGumpPosition = new Point(X, Y);
            Log.Info($"[ModernDialogueGump] Saved position to profile after drag: X={X}, Y={Y}");
        }

        base.OnDragEnd(x, y);
    }
}

/// <summary>
/// Data structure for Modern Dialogue Gump (matches server-side)
/// </summary>
public class ModernDialogueGumpData
{
    public string NpcName { get; set; } = "";
    public string NpcTitle { get; set; } = "";
    public string PortraitFileName { get; set; } = "";
    public int PortraitSize { get; set; } = 64;
    public string DialogueText { get; set; } = "";
    public string CurrentNodeId { get; set; } = "";
    public List<ModernDialogueOptionData> Options { get; set; } = new();
    public bool HasBackButton { get; set; }
    public string BackButtonText { get; set; } = "Back";
    public string LanguageCode { get; set; } = "en";
    public int GumpSerial { get; set; }
    public int GumpTypeID { get; set; }
}

/// <summary>
/// Modern Dialogue option data structure (matches server-side)
/// </summary>
public class ModernDialogueOptionData
{
    public int Id { get; set; }
    public string Text { get; set; } = "";
    public string NextNodeId { get; set; } = "";
}

