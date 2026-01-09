using ClassicUO.Configuration;
using ClassicUO.Game.Data;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;
using System;

namespace ClassicUO.Game.UI.Gumps
{
    public class CoolDownBar : Gump
    {
        public const int COOL_DOWN_WIDTH = 180, COOL_DOWN_HEIGHT = 30;
        public static int DEFAULT_X => ProfileManager.CurrentProfile.CoolDownX;
        public static int DEFAULT_Y => ProfileManager.CurrentProfile.CoolDownY;

        private AlphaBlendControl background, foreground;
        public readonly Label textLabel, cooldownLabel;
        private DateTime expire;
        private TimeSpan duration;
        private int startX, startY;
        private readonly bool isBuffBar;

        private GumpPic gumpPic;

        public BuffIconType buffIconType;

        public CoolDownBar(World world, TimeSpan _duration, string _name, ushort _hue, int x, int y, ushort graphic = ushort.MaxValue, BuffIconType type = BuffIconType.Unknown2, bool isBuffBar = false) : base(world, 0, 0)
        {
            #region VARS
            Width = COOL_DOWN_WIDTH;
            Height = COOL_DOWN_HEIGHT;
            X = x;
            startX = x;
            Y = y;
            startY = y;
            expire = DateTime.Now + _duration;
            duration = _duration;
            CanCloseWithRightClick = true;
            CanMove = true;
            AcceptMouseInput = true;
            buffIconType = type;
            this.isBuffBar = isBuffBar;
            #endregion

            #region BACK/FORE GROUND
            background = new AlphaBlendControl(0.3f);
            background.Width = COOL_DOWN_WIDTH;
            background.Height = COOL_DOWN_HEIGHT;
            background.Hue = _hue;

            foreground = new AlphaBlendControl(0.8f);
            foreground.Width = COOL_DOWN_WIDTH;
            foreground.Height = COOL_DOWN_HEIGHT;
            foreground.Hue = _hue;
            #endregion

            if (graphic != ushort.MaxValue)
            {
                // Create gumpPic for any valid graphic (including 0x0000 for custom icons)
                // GumpPic will handle loading custom icons via GumpsLoader.TryLoadCustomBuffIcon
                try
                {
                    gumpPic = new GumpPic(0, 2, graphic, 0);
                    int picWidth = Math.Max(gumpPic.Width, 0);

                    // If picWidth is 0, it means the icon failed to load, use default icon size
                    if (picWidth == 0 && graphic == 0x0000)
                    {
                        // Custom icon failed to load, use a default size (typically 24x24 for buff icons)
                        picWidth = 24;
                    }

                    background.X = picWidth;
                    background.Width = Math.Max(COOL_DOWN_WIDTH - picWidth, 50); // Ensure minimum width

                    foreground.X = picWidth;
                    foreground.Width = Math.Max(COOL_DOWN_WIDTH - picWidth, 50);
                }
                catch (Exception ex)
                {
                    // Continue without gumpPic, use full width
                    background.X = 0;
                    background.Width = COOL_DOWN_WIDTH;
                    foreground.X = 0;
                    foreground.Width = COOL_DOWN_WIDTH;
                    gumpPic = null; // Ensure gumpPic is null if creation failed
                }
            }

            #region LABELS
            // Safety checks for text rendering
            if (string.IsNullOrWhiteSpace(_name))
            {
                _name = "Buff"; // Fallback for empty names
            }

            // Limit text length to prevent overflow in font rendering
            const int MAX_TEXT_LENGTH = 17;
            if (_name.Length > MAX_TEXT_LENGTH)
            {
                _name = _name.Substring(0, MAX_TEXT_LENGTH - 2) + "..";
            }

            // Ensure background width is valid (must be positive and reasonable)
            int labelWidth = Math.Max(Math.Min(background.Width, 500), 50); // Clamp between 50 and 500

            try
            {
                textLabel = new Label(_name, true, _hue, labelWidth, style: FontStyle.BlackBorder, align: Assets.TEXT_ALIGN_TYPE.TS_CENTER)
                {
                    X = background.X
                };
            }
            catch (Exception ex)
            {
                // Fallback to safe text if rendering fails
                try
                {
                    textLabel = new Label("Buff", true, _hue, labelWidth, style: FontStyle.BlackBorder, align: Assets.TEXT_ALIGN_TYPE.TS_CENTER)
                    {
                        X = background.X
                    };
                }
                catch
                {
                    // Last resort: create minimal label
                    textLabel = new Label("B", true, _hue, 50, style: FontStyle.BlackBorder, align: Assets.TEXT_ALIGN_TYPE.TS_CENTER)
                    {
                        X = background.X
                    };
                }
            }

            // Create cooldownLabel with same safety checks
            try
            {
                int cooldownWidth = Math.Max(Math.Min(background.Width, 500), 50);
                cooldownLabel = new Label("------", true, _hue, cooldownWidth, style: FontStyle.BlackBorder, align: Assets.TEXT_ALIGN_TYPE.TS_CENTER)
                {
                    X = background.X,
                    Y = 0
                };
                cooldownLabel.Y = COOL_DOWN_HEIGHT - cooldownLabel.Height - 2;
                cooldownLabel.Text = "";
            }
            catch (Exception ex)
            {
                try
                {
                    cooldownLabel = new Label("", true, _hue, 50, style: FontStyle.BlackBorder, align: Assets.TEXT_ALIGN_TYPE.TS_CENTER)
                    {
                        X = background.X,
                        Y = COOL_DOWN_HEIGHT - 15
                    };
                }
                catch
                {
                    // Last resort: create minimal label
                    cooldownLabel = new Label("", true, _hue, 50, style: FontStyle.BlackBorder, align: Assets.TEXT_ALIGN_TYPE.TS_CENTER)
                    {
                        X = 0,
                        Y = 0
                    };
                }
            }
            #endregion

            #region ADD CONTROLS
            if (gumpPic != null)
                Add(gumpPic);
            Add(background);
            Add(foreground);
            Add(textLabel);
            Add(cooldownLabel);
            #endregion
        }

        public override void Update()
        {
            base.Update();

            if (
                !isBuffBar &&
                (ProfileManager.CurrentProfile?.UseLastMovedCooldownPosition ?? false) &&
                (X != startX || Y != startY)
                )
            {
                ProfileManager.CurrentProfile.CoolDownX = X;
                ProfileManager.CurrentProfile.CoolDownY = Y;
                startX = X;
                startY = Y;
            }
        }

        public override bool Draw(UltimaBatcher2D batcher, int x, int y)
        {
            if (IsDisposed)
                return false;

            if (DateTime.Now >= expire)
                Dispose();

            TimeSpan remaing = expire - DateTime.Now;

            if (remaing < TimeSpan.FromMinutes(60))
            {
                int offset = 0;
                if (gumpPic != null)
                    offset = gumpPic.Width;
                foreground.Width = (int)((remaing.TotalSeconds / duration.TotalSeconds) * (COOL_DOWN_WIDTH - offset));
                cooldownLabel.Text = ((int)remaing.TotalSeconds).ToString();
            }

            base.Draw(batcher, x, y);

            batcher.DrawRectangle(
                    SolidColorTextureCache.GetTexture(Color.Black),
                    x, y,
                    COOL_DOWN_WIDTH,
                    COOL_DOWN_HEIGHT,
                    ShaderHueTranslator.GetHueVector(background.Hue, false, 1f)
                );
            batcher.DrawRectangle(
                SolidColorTextureCache.GetTexture(Color.Black),
                x + 1, y + 1,
                COOL_DOWN_WIDTH - 2,
                COOL_DOWN_HEIGHT - 2,
                ShaderHueTranslator.GetHueVector(background.Hue, false, 1f)
            );

            return true;
        }

        public class CoolDownConditionData
        {
            public ushort hue;
            public string label;
            public string trigger;
            public int cooldown;
            public int message_type;
            public bool replace_if_exists;

            private CoolDownConditionData(ushort hue = 42, string label = "Label", string trigger = "Text to trigger", int cooldown = 10, int message_type = (int)MESSAGE_TYPE.ALL, bool replace_if_exists = false)
            {
                this.hue = hue;
                this.label = label;
                this.trigger = trigger;
                this.cooldown = cooldown;
                this.message_type = message_type;
                this.replace_if_exists = replace_if_exists;
            }

            public static CoolDownConditionData GetConditionData(int key, bool createIfNotExist)
            {
                var data = new CoolDownConditionData();
                if (ProfileManager.CurrentProfile.CoolDownConditionCount > key)
                {
                    data.hue = ProfileManager.CurrentProfile.Condition_Hue[key];
                    data.label = ProfileManager.CurrentProfile.Condition_Label[key];
                    data.trigger = ProfileManager.CurrentProfile.Condition_Trigger[key];
                    data.cooldown = ProfileManager.CurrentProfile.Condition_Duration[key];
                    data.message_type = ProfileManager.CurrentProfile.Condition_Type[key];

                    if (ProfileManager.CurrentProfile.Condition_ReplaceIfExists.Count > key) //Remove me after a while to prevent index not found
                        data.replace_if_exists = ProfileManager.CurrentProfile.Condition_ReplaceIfExists[key];
                    else
                    {
                        while (ProfileManager.CurrentProfile.Condition_ReplaceIfExists.Count <= key)
                        {
                            ProfileManager.CurrentProfile.Condition_ReplaceIfExists.Add(false);
                        }
                    }
                }
                else if (createIfNotExist)
                {
                    ProfileManager.CurrentProfile.Condition_Hue.Add(data.hue);
                    ProfileManager.CurrentProfile.Condition_Label.Add(data.label);
                    ProfileManager.CurrentProfile.Condition_Trigger.Add(data.trigger);
                    ProfileManager.CurrentProfile.Condition_Duration.Add(data.cooldown);
                    ProfileManager.CurrentProfile.Condition_Type.Add(data.message_type);
                    ProfileManager.CurrentProfile.Condition_ReplaceIfExists.Add(data.replace_if_exists);
                }
                return data;
            }

            public static void SaveCondition(int key, ushort hue, string label, string trigger, int cooldown, bool createIfNotExist, int message_type, bool replace_if_exists)
            {
                if (ProfileManager.CurrentProfile.CoolDownConditionCount > key)
                {
                    ProfileManager.CurrentProfile.Condition_Hue[key] = hue;
                    ProfileManager.CurrentProfile.Condition_Label[key] = label;
                    ProfileManager.CurrentProfile.Condition_Trigger[key] = trigger;
                    ProfileManager.CurrentProfile.Condition_Duration[key] = cooldown;
                    ProfileManager.CurrentProfile.Condition_Type[key] = message_type;

                    if (ProfileManager.CurrentProfile.Condition_ReplaceIfExists.Count > key) //Remove me after a while to prevent index not found
                        ProfileManager.CurrentProfile.Condition_ReplaceIfExists[key] = replace_if_exists;
                    else
                    {
                        while (ProfileManager.CurrentProfile.Condition_ReplaceIfExists.Count <= key)
                        {
                            ProfileManager.CurrentProfile.Condition_ReplaceIfExists.Add(false);
                        }
                        ProfileManager.CurrentProfile.Condition_ReplaceIfExists[key] = replace_if_exists;
                    }
                }
                else if (createIfNotExist)
                {
                    ProfileManager.CurrentProfile.Condition_Hue.Add(hue);
                    ProfileManager.CurrentProfile.Condition_Label.Add(label);
                    ProfileManager.CurrentProfile.Condition_Trigger.Add(trigger);
                    ProfileManager.CurrentProfile.Condition_Duration.Add(cooldown);
                    ProfileManager.CurrentProfile.Condition_Type.Add(message_type);
                    ProfileManager.CurrentProfile.Condition_ReplaceIfExists.Add(createIfNotExist);
                }
            }

            public static void RemoveCondition(int key)
            {
                if (ProfileManager.CurrentProfile.CoolDownConditionCount > key)
                {
                    ProfileManager.CurrentProfile.Condition_Hue.RemoveAt(key);
                    ProfileManager.CurrentProfile.Condition_Label.RemoveAt(key);
                    ProfileManager.CurrentProfile.Condition_Trigger.RemoveAt(key);
                    ProfileManager.CurrentProfile.Condition_Duration.RemoveAt(key);
                    ProfileManager.CurrentProfile.Condition_Type.RemoveAt(key);
                    ProfileManager.CurrentProfile.Condition_ReplaceIfExists.RemoveAt(key);
                }
            }

            public enum MESSAGE_TYPE
            {
                ALL,
                SELF,
                OTHER
            }

        }
    }
}
