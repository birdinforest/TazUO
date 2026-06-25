using System;
using System.Text.Json;
using ClassicUO.Assets;
using ClassicUO.Game;
using ClassicUO.Game.Data;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Input;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.UI.Gumps;

/// <summary>
/// Modern Command UI - Displays all available commands as buttons
/// </summary>
public class CommandUIGump : NineSliceGump
{
    private ModernScrollArea _scrollArea;
    private int _contentY = 0;
    private const int PADDING = 15;
    private const int BUTTON_HEIGHT = 35;
    private const int BUTTON_WIDTH = 200;
    private const int BUTTON_SPACING = 10;
    private CommandUIData _data;

    public CommandUIGump(World world, int x, int y, CommandUIData data)
        : base(world, x, y, 600, 700,
              ModernUIConstants.ModernUIPanel,
              ModernUIConstants.ModernUIPanel_BorderSize,
              resizable: true,
              minWidth: 400,
              minHeight: 500)
    {
        _data = data;
        BuildUI();
    }

    private void BuildUI()
    {
        // Title bar (fixed, not scrollable)
        var titleText = TextBox.GetOne(
            "/c[#FFD700]Command Panel/c[#FFFFFF]",
            TrueTypeLoader.EMBEDDED_FONT,
            20f,
            Color.White,
            TextBox.RTLOptions.Default(Width - 100)
        );
        titleText.X = PADDING;
        titleText.Y = PADDING;
        Add(titleText);

        // Scrollable content area
        _scrollArea = new ModernScrollArea(
            PADDING,
            50,
            Width - (PADDING * 2),
            Height - 100,
            scrollMaxHeight: -1
        )
        {
            ScrollbarBehaviour = ScrollbarBehaviour.ShowWhenDataExceedFromView
        };
        Add(_scrollArea);

        _contentY = 0;

        // Add command buttons
        if (_data?.Commands != null && _data.Commands.Length > 0)
        {
            foreach (var cmd in _data.Commands)
            {
                AddCommandButton(cmd);
            }
        }
        else
        {
            var noCommandsText = TextBox.GetOne(
                "No commands available",
                TrueTypeLoader.EMBEDDED_FONT,
                14f,
                Color.Gray,
                TextBox.RTLOptions.Default(_scrollArea.Width - 20)
            );
            noCommandsText.X = 10;
            noCommandsText.Y = _contentY;
            _scrollArea.Add(noCommandsText);
        }

        // Close button (fixed at bottom of gump, not scrollable)
        var closeButton = new NineSliceButton(
            BUTTON_WIDTH,
            BUTTON_HEIGHT,
            ModernUIConstants.ModernUIButtonUp,
            ModernUIConstants.ModernUIButton_BorderSize,
            ModernUIConstants.ModernUIButtonDown,
            ModernUIConstants.ModernUIButton_BorderSize,
            hoverHue: 37
        );
        closeButton.X = Width / 2 - BUTTON_WIDTH / 2;
        closeButton.Y = Height - BUTTON_HEIGHT - PADDING;
        closeButton.MouseUp += (sender, e) =>
        {
            if (e.Button == MouseButtonType.Left)
            {
                Dispose();
            }
        };

        var closeButtonText = TextBox.GetOne(
            "Close",
            TrueTypeLoader.EMBEDDED_FONT,
            14f,
            Color.White,
            TextBox.RTLOptions.Default(BUTTON_WIDTH - 10)
        );
        closeButtonText.X = 5;
        closeButtonText.Y = 5;
        closeButton.Add(closeButtonText);
        Add(closeButton);
    }

    private void AddCommandButton(CommandButtonInfo cmd)
    {
        // Create button
        var button = new NineSliceButton(
            BUTTON_WIDTH,
            BUTTON_HEIGHT,
            ModernUIConstants.ModernUIButtonUp,
            ModernUIConstants.ModernUIButton_BorderSize,
            ModernUIConstants.ModernUIButtonDown,
            ModernUIConstants.ModernUIButton_BorderSize,
            hoverHue: 37
        );
        button.X = 10;
        button.Y = _contentY;
        button.MouseUp += (sender, e) =>
        {
            if (e.Button == MouseButtonType.Left)
            {
                ExecuteCommand(cmd.Command);
            }
        };

        // Button text (command name)
        var buttonText = TextBox.GetOne(
            $"/c[#FFFFFF][{cmd.Command}]",
            TrueTypeLoader.EMBEDDED_FONT,
            13f,
            Color.White,
            TextBox.RTLOptions.Default(BUTTON_WIDTH - 20)
        );
        buttonText.X = 10;
        buttonText.Y = 5;
        button.Add(buttonText);

        _scrollArea.Add(button);
        _contentY += BUTTON_HEIGHT + 5;

        // Description text below button
        if (!string.IsNullOrEmpty(cmd.Description))
        {
            var descText = TextBox.GetOne(
                $"/c[#CCCCCC]{cmd.Description}",
                TrueTypeLoader.EMBEDDED_FONT,
                11f,
                Color.LightGray,
                TextBox.RTLOptions.Default(_scrollArea.Width - 30)
            );
            descText.X = 15;
            descText.Y = _contentY;
            _scrollArea.Add(descText);
            _contentY += 18;
        }

        // Usage text (if different from command name)
        if (!string.IsNullOrEmpty(cmd.Usage) && cmd.Usage != cmd.Command)
        {
            var usageText = TextBox.GetOne(
                $"/c[#888888]Usage: {cmd.Usage}",
                TrueTypeLoader.EMBEDDED_FONT,
                10f,
                Color.Gray,
                TextBox.RTLOptions.Default(_scrollArea.Width - 30)
            );
            usageText.X = 15;
            usageText.Y = _contentY;
            _scrollArea.Add(usageText);
            _contentY += 16;
        }

        _contentY += BUTTON_SPACING;
    }

    private void ExecuteCommand(string command)
    {
        // Send command to server using standard command system
        // When using MessageType.Command, the server expects the command WITHOUT the prefix
        // The server will process it directly without stripping the prefix
        string commandText = command;

        // Use GameActions.Say to send command with MessageType.Command
        // This tells the server to process it as a command
        ClassicUO.Game.GameActions.Say(commandText, 0xFFFF, MessageType.Command);
    }

    protected override void OnResize(int oldWidth, int oldHeight, int newWidth, int newHeight)
    {
        base.OnResize(oldWidth, oldHeight, newWidth, newHeight);
        if (_scrollArea != null)
        {
            _scrollArea.Width = newWidth - (PADDING * 2);
            _scrollArea.Height = newHeight - 100;
            _scrollArea.UpdateWidth(_scrollArea.Width);
        }
    }
}

// Data structure for Command UI (matches server-side CommandUIData)
public class CommandUIData
{
    public CommandButtonInfo[] Commands { get; set; }
}

public class CommandButtonInfo
{
    public string Command { get; set; } = "";
    public string Description { get; set; } = "";
    public string Usage { get; set; } = "";
}

