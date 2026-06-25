using System;
using System.Collections.Generic;
using System.Text.Json;
using ClassicUO.Configuration;
using ClassicUO.Game;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Gumps;
using ClassicUO.IO;
using ClassicUO.Utility.Logging;

namespace ClassicUO.Network;

public class EnhancedPacketHandler
{
    //Make sure all enhanced packets use BigEndian for numbers. Avoid LittleEndian.

    public const byte EPID = 0xCE;
    
    public static bool Enabled;

    static EnhancedPacketHandler()
    {
        Enabled = Client.Settings.Get(SettingsScope.Server, Constants.SqlSettings.ENABLE_ENHANCED_PACKETS, false);

        if (!Enabled)
            return;

        Handler.Add(EnhancedPacketType.EnableEnhancedPacket, EnableEnhancedPacket);
        Handler.Add(EnhancedPacketType.BalanceTestModernGump, HandleBalanceTestModernGump);
        Handler.Add(EnhancedPacketType.CommandUI, HandleCommandUI);
        Handler.Add(EnhancedPacketType.ModernDialogueGump, HandleModernDialogueGump);
    }

    /// <summary>
    /// Allow servers to enable specific enhanced packets.
    /// </summary>
    private static void EnableEnhancedPacket(ref StackDataReader p, int version)
    {
        EnhancedOutgoingPackets.EnabledPackets.Add(EnhancedPacketType.EnableEnhancedPacket);

        if (version >= 0)
        {
            ushort count = p.ReadUInt16BE(); //Number of enhanced packets to follow.

            for (int i = 0; i < count; i++)
            {
                ushort id = p.ReadUInt16BE();

                if (Enum.IsDefined(typeof(EnhancedPacketType), id))
                {
                    EnhancedOutgoingPackets.EnabledPackets.Add((EnhancedPacketType)id);
                }
            }
        }

        AsyncNetClient.Socket.SendEnhancedPacket(); //Confirm we are ready to receive enhanced packets.
    }

    /// <summary>
    /// Handle BalanceTest modern UI gump packet
    /// </summary>
    private static void HandleBalanceTestModernGump(ref StackDataReader p, int version)
    {
        try
        {
            // Read JSON data
            string json = p.ReadUTF8();

            // Deserialize to BalanceTestData
            var data = JsonSerializer.Deserialize<BalanceTestData>(json);

            if (data != null)
            {
                // Get world instance (World is a singleton)
                World world = World.Instance;
                if (world != null)
                {
                    // Create and display modern gump
                    var gump = new ModernBalanceTestGump(world, 100, 100, data);
                    UIManager.Add(gump);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error($"Error handling BalanceTest modern gump packet: {ex.Message}");
        }
    }

    /// <summary>
    /// Handle Command UI packet
    /// </summary>
    private static void HandleCommandUI(ref StackDataReader p, int version)
    {
        try
        {
            // Read JSON data
            string json = p.ReadUTF8();

            // Deserialize to CommandUIData
            var data = JsonSerializer.Deserialize<CommandUIData>(json);

            if (data != null)
            {
                // Get world instance (World is a singleton)
                World world = World.Instance;
                if (world != null)
                {
                    // Create and display command UI gump
                    var gump = new CommandUIGump(world, 100, 100, data);
                    UIManager.Add(gump);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error($"Error handling Command UI packet: {ex.Message}");
        }
    }

    /// <summary>
    /// Handle Modern Dialogue Gump packet
    /// </summary>
    private static void HandleModernDialogueGump(ref StackDataReader p, int version)
    {
        try
        {
            // Read JSON data
            string json = p.ReadUTF8();

            Log.Info($"[ModernDialogueGump] Received JSON: {json.Length} bytes");
            Log.Info($"[ModernDialogueGump] JSON preview (first 300 chars): {(json.Length > 300 ? json.Substring(0, 300) + "..." : json)}");

            // Deserialize to ModernDialogueGumpData
            // Configure JsonSerializerOptions to match server's PascalCase property names
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true // Allow case-insensitive matching
            };
            var data = JsonSerializer.Deserialize<ModernDialogueGumpData>(json, options);

            if (data != null)
            {
                Log.Info($"[ModernDialogueGump] Deserialized data - NpcTitle: '{data.NpcTitle}', DialogueText: '{data.DialogueText?.Substring(0, Math.Min(50, data.DialogueText?.Length ?? 0))}...', Options count: {data.Options?.Count ?? 0}");

                if (data.Options != null)
                {
                    for (int i = 0; i < data.Options.Count; i++)
                    {
                        var opt = data.Options[i];
                        Log.Info($"[ModernDialogueGump] Option {i}: Id={opt.Id}, Text='{opt.Text}', NextNodeId='{opt.NextNodeId}'");
                    }
                }

                Log.Info($"[ModernDialogueGump] BackButtonText: '{data.BackButtonText}', HasBackButton: {data.HasBackButton}");

                // Get world instance (World is a singleton)
                World world = World.Instance;
                if (world != null)
                {
                    // Check if singleton instance already exists and is not disposed
                    var existingGump = ModernDialogueGump.Instance;

                    if (existingGump != null && !existingGump.IsDisposed)
                    {
                        // Existing instance - update content
                        existingGump.UpdateContent(data);
                        Log.Info($"[ModernDialogueGump] Existing singleton instance updated with new content");
                    }
                    else
                    {
                        // Create new singleton instance
                        var gump = ModernDialogueGump.GetOrCreateInstance(world, data);
                        UIManager.Add(gump);
                        Log.Info($"[ModernDialogueGump] New singleton instance created and added to UI manager");
                    }
                }
                else
                {
                    Log.Error("[ModernDialogueGump] World instance is null");
                }
            }
            else
            {
                Log.Error("[ModernDialogueGump] Failed to deserialize JSON data");
            }
        }
        catch (Exception ex)
        {
            Log.Error($"[ModernDialogueGump] Error handling Modern Dialogue Gump packet: {ex.Message}");
            Log.Error($"[ModernDialogueGump] Stack trace: {ex.StackTrace}");
        }
    }

    private readonly Dictionary<ushort, EnhancedOnPacketBufferReader> _handlers = new();
    public delegate void EnhancedOnPacketBufferReader(ref StackDataReader p, int version);
    public static EnhancedPacketHandler Handler { get; } = new();

    public static void Handle(World world, ref StackDataReader p)
    {
        ushort id = p.ReadUInt16BE();
        ushort ver = p.ReadUInt16BE();
        Handler.HandlePacket(id, ref p, ver);
    }

    public void Add(EnhancedPacketType type, EnhancedOnPacketBufferReader handler) => _handlers[(ushort)type] = handler;

    public void HandlePacket(ushort packetID, ref StackDataReader p, int version)
    {
        if (!Enabled)
            return;

        if (_handlers.ContainsKey(packetID))
        {
            _handlers[packetID].Invoke(ref p, version);
        }
        else
        {
            Log.Error($"Received invalid enhanced packet {packetID} (0x{packetID:X}) len={p.Length}");
        }
    }
}
