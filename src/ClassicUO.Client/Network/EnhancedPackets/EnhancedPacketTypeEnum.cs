namespace ClassicUO.Network;

public enum EnhancedPacketType : ushort
{
    None,
    EnableEnhancedPacket,
    TazUO_Identifier,
    BalanceTestModernGump = 100, // Custom packet type for BalanceTest modern UI
    CommandUI = 101, // Custom packet type for Command UI
    CommandExecute = 102, // Custom packet type for command execution request
    ModernDialogueGump = 103, // Custom packet type for Modern Dialogue Gump with NPC portraits
}
