using System;
using ClassicUO.Assets;
using ClassicUO.Game;
using ClassicUO.Game.Data;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Input;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;
using System.Text.Json;

namespace ClassicUO.Game.UI.Gumps;

/// <summary>
/// Modern BalanceTest UI - Displays combat analysis data using modern UI components
/// </summary>
public class ModernBalanceTestGump : NineSliceGump
{
    private ModernScrollArea _scrollArea;
    private int _contentY = 0;
    private const int PADDING = 15;
    private const int SECTION_SPACING = 20;
    private const int BUTTON_HEIGHT = 30;
    private const int BUTTON_WIDTH = 120;

    public ModernBalanceTestGump(World world, int x, int y, BalanceTestData data)
        : base(world, x, y, 700, 600,
              ModernUIConstants.ModernUIPanel,
              ModernUIConstants.ModernUIPanel_BoderSize,
              resizable: true,
              minWidth: 500,
              minHeight: 400)
    {
        BuildUI(data);
    }

    private void BuildUI(BalanceTestData data)
    {
        // Title bar (fixed, not scrollable)
        var titleText = TextBox.GetOne(
            "/c[#FFD700]Combat Analysis System/c[#FFFFFF]",
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
            Height - 120,
            scrollMaxHeight: -1
        )
        {
            ScrollbarBehaviour = ScrollbarBehaviour.ShowWhenDataExceedFromView
        };
        Add(_scrollArea);

        _contentY = 0;

        if (!data.HasTarget)
        {
            // ============================================
            // Player Stats Only (No Target)
            // ============================================
            AddSectionHeader("Your Combat Statistics", Color.Gold);

            // HP Bar
            AddLabel("Health Points:", Color.White);
            double hpPercentage = data.PlayerHitsMax > 0 ? (double)data.PlayerHits / data.PlayerHitsMax : 0.0;
            var hpBar = new ProgressBarGump(
                World,
                "",
                hpPercentage,
                _scrollArea.Width - 20,
                25
            );
            hpBar.X = 10;
            hpBar.Y = _contentY;
            hpBar.ForegrouneColor = Color.Red;
            _scrollArea.Add(hpBar);
            _contentY += 35;

            AddText($"/c[#FFFFFF]HP: {data.PlayerHits}/{data.PlayerHitsMax} | Armor: {data.PlayerArmor}", Color.White);

            // Attributes
            AddLabel("Attributes:", Color.LightBlue);
            _contentY += 5;
            AddAttributeBox("STR", data.Str, Color.Red);
            AddAttributeBox("DEX", data.Dex, Color.Green);
            AddAttributeBox("INT", data.Int, Color.Blue);
            _contentY += SECTION_SPACING;

            // Weapon Info
            AddSectionHeader("Weapon Information", Color.Cyan);
            AddText($"/c[#FFFFFF]Weapon: /c[#00FF00]{data.WeaponName}", Color.White);
            AddText($"/c[#FFFFFF]Base Damage: /c[#FFFF00]{data.PlayerMinDamage}-{data.PlayerMaxDamage}", Color.White);
            AddText($"/c[#FFFFFF]Tactics: {data.PlayerTacticsSkill:F1} | Weapon Skill: {data.PlayerWeaponSkill:F1}", Color.White);
            AddText($"/c[#00FF00]Modified Damage: {data.PlayerEffectiveDamageMin:F1}-{data.PlayerEffectiveDamageMax:F1}", Color.White);
            _contentY += SECTION_SPACING;

            // Combat Skills
            AddSectionHeader("Combat Skills", Color.LightBlue);
            AddText($"/c[#FFFFFF]Swords: {data.PlayerTacticsSkill:F1} | Bludgeoning: {data.PlayerTacticsSkill:F1}", Color.White);
            AddText($"/c[#FFFFFF]Fencing: {data.PlayerTacticsSkill:F1} | Archery: {data.PlayerTacticsSkill:F1}", Color.White);
        }
        else
        {
            // ============================================
            // SECTION 1: Player Stats
            // ============================================
            AddSectionHeader("Your Data", Color.Gold);

            // HP Bar
            AddLabel("Health Points:", Color.White);
            double hpPercentage = data.PlayerHitsMax > 0 ? (double)data.PlayerHits / data.PlayerHitsMax : 0.0;
            var hpBar = new ProgressBarGump(
                World,
                "",
                hpPercentage,
                _scrollArea.Width - 20,
                25
            );
            hpBar.X = 10;
            hpBar.Y = _contentY;
            hpBar.ForegrouneColor = Color.Red;
            _scrollArea.Add(hpBar);
            _contentY += 35;

            AddText($"/c[#FFFFFF]HP: {data.PlayerHits}/{data.PlayerHitsMax} | Armor: {data.PlayerArmor}", Color.White);
            AddText($"/c[#FFFFFF]Weapon: {data.WeaponName} | Damage: {data.PlayerMinDamage}-{data.PlayerMaxDamage}", Color.White);
            AddText($"/c[#FFFFFF]Tactics: {data.PlayerTacticsSkill:F1} | Weapon Skill: {data.PlayerWeaponSkill:F1}", Color.White);

            // Damage formula
            var formulaBox = new ColorBox(
                _scrollArea.Width - 20,
                40,
                1 // Dark gray hue
            );
            formulaBox.X = 10;
            formulaBox.Y = _contentY;
            formulaBox.Alpha = 0.5f;
            _scrollArea.Add(formulaBox);

            var formulaText = TextBox.GetOne(
                $"/c[#CCCCCC]Base[{data.PlayerBaseDamage:F1}] × (1+Skill[{data.PlayerSkillModifier:F3}]+Attr[{data.PlayerAttributeModifier:F3}]) ÷ 2 - Armor[{data.PlayerArmorReduction:F1}]",
                TrueTypeLoader.EMBEDDED_FONT,
                12f,
                Color.LightGray,
                TextBox.RTLOptions.Default(_scrollArea.Width - 40)
            );
            formulaText.X = 15;
            formulaText.Y = _contentY + 5;
            _scrollArea.Add(formulaText);
            _contentY += 50;

            // Effective damage and hit chance
            AddText($"/c[#00FF00]Effective Physical Damage (Calculated): {data.PlayerEffectiveDamageMin:F1}-{data.PlayerEffectiveDamageMax:F1}", Color.White);
            AddText($"/c[#FFFF00]Hit Chance: {data.PlayerHitChance:F1}%", Color.White);

            // Hit chance details
            var hitChanceDetail = TextBox.GetOne(
                $"/c[#CCCCCC](Attack Skill: {data.PlayerAttackSkill:F1}, Defend Skill: {data.PlayerDefendSkill:F1}, Hit Bonus: {data.PlayerHitBonus}%)",
                TrueTypeLoader.EMBEDDED_FONT,
                11f,
                Color.LightGray,
                TextBox.RTLOptions.Default(_scrollArea.Width - 20)
            );
            hitChanceDetail.X = 10;
            hitChanceDetail.Y = _contentY;
            _scrollArea.Add(hitChanceDetail);
            _contentY += 18;

            // Real damage range (if available)
            if (data.RealPlayerDamageRange != null)
            {
                AddText($"/c[#00FFFF]Real Damage (Simulated {data.RealPlayerDamageRange.SampleCount} times): {data.RealPlayerDamageRange.Min}-{data.RealPlayerDamageRange.Max} (Average: {data.RealPlayerDamageRange.Average:F1})", Color.White);
            }

            // Player magic damage
            if (data.HasPlayerMagic)
            {
                AddText($"/c[#9370DB]Magery: {data.PlayerMagerySkill:F1} | Effective Magic Damage: {data.PlayerEffectiveMagicDamageMin:F1}-{data.PlayerEffectiveMagicDamageMax:F1}", Color.White);
            }

            _contentY += SECTION_SPACING;

            // ============================================
            // SECTION 2: Monster Stats
            // ============================================
            AddSectionHeader($"{data.MonsterName} Data", Color.OrangeRed);

            // Monster HP Bar
            AddLabel("Monster Health:", Color.White);
            var monsterHpBar = new ProgressBarGump(
                World,
                "",
                1.0, // 100% for full HP
                _scrollArea.Width - 20,
                25
            );
            monsterHpBar.X = 10;
            monsterHpBar.Y = _contentY;
            monsterHpBar.ForegrouneColor = Color.OrangeRed;
            _scrollArea.Add(monsterHpBar);
            _contentY += 35;

            AddText($"/c[#FFFFFF]HP: {data.MonsterHits} | Armor: {data.MonsterArmor}", Color.White);
            AddText($"/c[#FFFFFF]Physical Damage: {data.MonsterMinDamage}-{data.MonsterMaxDamage} | Tactics: {data.MonsterTacticsSkill:F1}", Color.White);

            // Monster damage formula
            var monsterFormulaBox = new ColorBox(
                _scrollArea.Width - 20,
                40,
                1 // Dark gray hue
            );
            monsterFormulaBox.X = 10;
            monsterFormulaBox.Y = _contentY;
            monsterFormulaBox.Alpha = 0.5f;
            _scrollArea.Add(monsterFormulaBox);

            var monsterFormulaText = TextBox.GetOne(
                $"/c[#CCCCCC]Base[{data.MonsterBaseDamage:F1}] × (1+Skill[{data.MonsterSkillModifier:F3}]+Attr[{data.MonsterAttributeModifier:F3}]) ÷ 2 - Armor[{data.MonsterArmorReduction:F1}]",
                TrueTypeLoader.EMBEDDED_FONT,
                12f,
                Color.LightGray,
                TextBox.RTLOptions.Default(_scrollArea.Width - 40)
            );
            monsterFormulaText.X = 15;
            monsterFormulaText.Y = _contentY + 5;
            _scrollArea.Add(monsterFormulaText);
            _contentY += 50;

            AddText($"/c[#FF6347]Effective Physical Damage (Calculated): {data.MonsterEffectiveDamageMin:F1}-{data.MonsterEffectiveDamageMax:F1}", Color.White);
            AddText($"/c[#FF6347]Hit Chance: {data.MonsterHitChance:F1}%", Color.White);

            // Monster hit chance details
            var monsterHitChanceDetail = TextBox.GetOne(
                $"/c[#CCCCCC](Attack Skill: {data.MonsterAttackSkill:F1}, Defend Skill: {data.MonsterDefendSkill:F1}, Hit Bonus: {data.MonsterHitBonus}%)",
                TrueTypeLoader.EMBEDDED_FONT,
                11f,
                Color.LightGray,
                TextBox.RTLOptions.Default(_scrollArea.Width - 20)
            );
            monsterHitChanceDetail.X = 10;
            monsterHitChanceDetail.Y = _contentY;
            _scrollArea.Add(monsterHitChanceDetail);
            _contentY += 18;

            // Real monster damage range (if available)
            if (data.RealMonsterDamageRange != null)
            {
                AddText($"/c[#FF69B4]Real Damage (Simulated {data.RealMonsterDamageRange.SampleCount} times): {data.RealMonsterDamageRange.Min}-{data.RealMonsterDamageRange.Max} (Average: {data.RealMonsterDamageRange.Average:F1})", Color.White);
            }

            // Monster magic damage
            if (data.HasMonsterMagic)
            {
                AddText($"/c[#FF69B4]Magery: {data.MonsterMagerySkill:F1} | Effective Magic Damage: {data.MonsterEffectiveMagicDamageMin:F1}-{data.MonsterEffectiveMagicDamageMax:F1}", Color.White);
            }

            _contentY += SECTION_SPACING;

            // ============================================
            // SECTION 3: Special Attacks
            // ============================================
            if (data.HasBreathAttack || data.HasPoisonAttack || data.HasLifeDrain)
            {
                AddSectionHeader("Special Attacks", Color.Orange);

                if (data.HasBreathAttack)
                {
                    AddText($"/c[#FFCC00]Breath Attack (Damage Scalar: {data.BreathDamageScalar:F2})", Color.White);

                    string breathDamages = "";
                    if (data.BreathPhysical > 0) breathDamages += $"Physical {data.BreathPhysical}% ";
                    if (data.BreathFire > 0) breathDamages += $"Fire {data.BreathFire}% ";
                    if (data.BreathCold > 0) breathDamages += $"Cold {data.BreathCold}% ";
                    if (data.BreathPoison > 0) breathDamages += $"Poison {data.BreathPoison}% ";
                    if (data.BreathEnergy > 0) breathDamages += $"Energy {data.BreathEnergy}% ";

                    AddText($"/c[#FFFFFF]Damage Types: {breathDamages}", Color.White);
                    AddText($"/c[#FF6347]Effective Damage Range: {data.BreathEffectiveDamageMin:F1}-{data.BreathEffectiveDamageMax:F1}", Color.White);
                }

                if (data.HasPoisonAttack)
                {
                    AddText($"/c[#00FF00]Poison Attack: {data.PoisonName} (Trigger Chance: {data.PoisonChance:F0}%)", Color.White);
                    AddText($"/c[#FF6347]Poison Damage: {data.PoisonDamagePerSecond:F1}/s × {data.PoisonDurationSeconds:F1}s = {data.PoisonTotalDamage:F1} Total Damage", Color.White);
                    AddText($"/c[#FFA500]Average Additional Damage Per Hit: {data.PoisonAverageDamagePerHit:F1}", Color.White);
                }

                if (data.HasLifeDrain)
                {
                    string triggerInfo = "";
                    // Note: We can't check monster type from client data, so use generic message
                    triggerInfo = $" (Trigger Chance: {data.LifeDrainChance * 100.0:F0}%)";

                    AddText($"/c[#FF0000]HP Drain Attack{triggerInfo}", Color.White);
                    AddText($"/c[#FF6347]Drain HP Range: {data.LifeDrainMin}-{data.LifeDrainMax} (Average: {data.LifeDrainAverage:F1})", Color.White);
                    double avgLifeDrainPerHit = data.LifeDrainAverage * data.LifeDrainChance;
                    AddText($"/c[#FFA500]Average HP Drained Per Hit: {avgLifeDrainPerHit:F1} HP (Damages player, heals monster)", Color.White);
                }

                _contentY += SECTION_SPACING;
            }

            // ============================================
            // SECTION 4: Combat Prediction
            // ============================================
            AddSectionHeader("Combat Prediction", Color.LightBlue);

            AddText($"/c[#FFFFFF]Hits to Kill {data.MonsterName}: /c[#00FF00]{data.HitsToKill} attacks", Color.White);
            AddText($"/c[#FFFFFF]Hits to be Killed (Physical Only): /c[#FF6347]{data.HitsToBeKilled} attacks", Color.White);

            if (data.HasPoisonAttack || data.HasMonsterMagic || data.HasLifeDrain)
            {
                AddText($"/c[#FFFFFF]Hits to be Killed (With Special Attacks): /c[#FF0000]{data.HitsToBeKilledWithDoT} attacks", Color.White);

                string damageComponents = "Physical";
                if (data.HasMonsterMagic) damageComponents += "+Magic";
                if (data.HasPoisonAttack) damageComponents += "+Poison";
                if (data.HasLifeDrain) damageComponents += "+HP Drain";

                AddText($"/c[#FFA500]Total Damage Per Hit: {data.TotalMonsterDamagePerHit:F1} ({damageComponents})", Color.White);

                if (data.HasLifeDrain)
                {
                    double avgLifeDrainPerHit = data.LifeDrainAverage * data.LifeDrainChance;
                    double estimatedHitsToKill = data.HitsToKill;
                    double totalLifeDrained = avgLifeDrainPerHit * estimatedHitsToKill;
                    double effectiveMonsterHits = Math.Min(data.MonsterHits + (int)totalLifeDrained, data.MonsterHits * 2); // Approximate max

                    AddText($"/c[#FF69B4]Monster Effective HP: {effectiveMonsterHits:F0} (Original: {data.MonsterHits}, Recovery: {totalLifeDrained:F0})", Color.White);
                    AddText($"/c[#FF69B4]Hits to Kill: {data.HitsToKill} attacks (Considering HP Recovery)", Color.White);
                }
            }

            _contentY += SECTION_SPACING;

            // ============================================
            // SECTION 5: Combat Assessment
            // ============================================
            AddSectionHeader("Combat Assessment", GetAssessmentColor(data.AssessmentLevel));

            ushort assessmentHue = data.AssessmentLevel switch
            {
                0 => 63, // Green
                1 => 37, // Yellow/Gold
                2 => 46, // Orange
                3 => 33, // Red
                _ => 1   // Gray
            };

            var assessmentBox = new ColorBox(
                _scrollArea.Width - 20,
                60,
                assessmentHue
            );
            assessmentBox.X = 10;
            assessmentBox.Y = _contentY;
            assessmentBox.Alpha = 0.3f;
            _scrollArea.Add(assessmentBox);

            var assessmentText = TextBox.GetOne(
                $"/c[#FFFFFF]{data.Assessment}/n/c[#CCCCCC]{data.AssessmentDetail}",
                TrueTypeLoader.EMBEDDED_FONT,
                16f,
                Color.White,
                TextBox.RTLOptions.Default(_scrollArea.Width - 40)
            );
            assessmentText.X = 15;
            assessmentText.Y = _contentY + 10;
            _scrollArea.Add(assessmentText);
            _contentY += 70;

            // Note about hit chance accuracy
            var noteText = TextBox.GetOne(
                "/c[#888888]Note: Hit chance is calculated based on skill values. Actual combat may vary due to temporary buffs/debuffs, status effects, etc.",
                TrueTypeLoader.EMBEDDED_FONT,
                10f,
                Color.Gray,
                TextBox.RTLOptions.Default(_scrollArea.Width - 20)
            );
            noteText.X = 10;
            noteText.Y = _contentY;
            _scrollArea.Add(noteText);
        }

        // Bottom buttons (fixed at bottom of gump, not scrollable)
        var closeButton = new NineSliceButton(
            BUTTON_WIDTH,
            BUTTON_HEIGHT,
            ModernUIConstants.ModernUIButtonUp,
            ModernUIConstants.ModernUIButton_BorderSize,
            ModernUIConstants.ModernUIButtonDown,
            ModernUIConstants.ModernUIButton_BorderSize,
            hoverHue: 37
        );
        closeButton.X = Width - BUTTON_WIDTH - PADDING;
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

    // Helper methods
    private void AddSectionHeader(string text, Color color)
    {
        var header = TextBox.GetOne(
            $"/c[{ColorToHex(color)}]{text}",
            TrueTypeLoader.EMBEDDED_FONT,
            18f,
            color,
            TextBox.RTLOptions.Default(_scrollArea.Width - 20)
        );
        header.X = 10;
        header.Y = _contentY;
        _scrollArea.Add(header);
        _contentY += 30;
    }

    private void AddLabel(string text, Color color)
    {
        var label = TextBox.GetOne(
            text,
            TrueTypeLoader.EMBEDDED_FONT,
            14f,
            color,
            TextBox.RTLOptions.Default(_scrollArea.Width - 20)
        );
        label.X = 10;
        label.Y = _contentY;
        _scrollArea.Add(label);
        _contentY += 20;
    }

    private void AddText(string text, Color defaultColor)
    {
        var textBox = TextBox.GetOne(
            text,
            TrueTypeLoader.EMBEDDED_FONT,
            13f,
            defaultColor,
            TextBox.RTLOptions.Default(_scrollArea.Width - 20)
        );
        textBox.X = 10;
        textBox.Y = _contentY;
        _scrollArea.Add(textBox);
        _contentY += 18;
    }

    private void AddAttributeBox(string name, int value, Color color)
    {
        ushort hue = 0;
        if (color == Color.Red) hue = 33;
        else if (color == Color.Green) hue = 63;
        else if (color == Color.Blue) hue = 88;

        var box = new ColorBox(80, 25, hue);
        box.X = 10 + ((name == "STR" ? 0 : name == "DEX" ? 90 : 180));
        box.Y = _contentY;
        box.Alpha = 0.3f;
        _scrollArea.Add(box);

        var text = TextBox.GetOne(
            $"{name}: {value}",
            TrueTypeLoader.EMBEDDED_FONT,
            12f,
            color,
            TextBox.RTLOptions.Default(75)
        );
        text.X = box.X + 5;
        text.Y = _contentY + 5;
        _scrollArea.Add(text);
        _contentY += 30;
    }

    private Color GetAssessmentColor(int level)
    {
        return level switch
        {
            0 => Color.Green,
            1 => Color.Yellow,
            2 => Color.Orange,
            3 => Color.Red,
            _ => Color.White
        };
    }

    private string ColorToHex(Color color)
    {
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    protected override void OnResize(int oldWidth, int oldHeight, int newWidth, int newHeight)
    {
        base.OnResize(oldWidth, oldHeight, newWidth, newHeight);
        if (_scrollArea != null)
        {
            _scrollArea.Width = newWidth - (PADDING * 2);
            _scrollArea.Height = newHeight - 120;
            _scrollArea.UpdateWidth(_scrollArea.Width);
        }
    }
}

// Data structure for BalanceTest (matches server-side CombatData)
public class BalanceTestData
{
    // Player stats
    public int PlayerHits { get; set; }
    public int PlayerHitsMax { get; set; }
    public int PlayerArmor { get; set; }
    public int Str { get; set; }
    public int Dex { get; set; }
    public int Int { get; set; }

    // Weapon info
    public string WeaponName { get; set; } = "";
    public int PlayerMinDamage { get; set; }
    public int PlayerMaxDamage { get; set; }
    public double PlayerTacticsSkill { get; set; }
    public double PlayerWeaponSkill { get; set; }
    public double PlayerBaseDamage { get; set; }
    public double PlayerSkillModifier { get; set; }
    public double PlayerAttributeModifier { get; set; }
    public double PlayerArmorReduction { get; set; }
    public double PlayerEffectiveDamageMin { get; set; }
    public double PlayerEffectiveDamageMax { get; set; }
    public double PlayerHitChance { get; set; }
    public double PlayerAttackSkill { get; set; }
    public double PlayerDefendSkill { get; set; }
    public int PlayerHitBonus { get; set; }

    // Player magic
    public bool HasPlayerMagic { get; set; }
    public double PlayerMagerySkill { get; set; }
    public double PlayerEffectiveMagicDamageMin { get; set; }
    public double PlayerEffectiveMagicDamageMax { get; set; }

    // Monster stats
    public bool HasTarget { get; set; }
    public string MonsterName { get; set; } = "";
    public int MonsterHits { get; set; }
    public int MonsterArmor { get; set; }
    public int MonsterMinDamage { get; set; }
    public int MonsterMaxDamage { get; set; }
    public double MonsterEffectiveDamageMin { get; set; }
    public double MonsterEffectiveDamageMax { get; set; }
    public double MonsterHitChance { get; set; }
    public double MonsterTacticsSkill { get; set; }
    public double MonsterBaseDamage { get; set; }
    public double MonsterSkillModifier { get; set; }
    public double MonsterAttributeModifier { get; set; }
    public double MonsterArmorReduction { get; set; }
    public double MonsterAttackSkill { get; set; }
    public double MonsterDefendSkill { get; set; }
    public int MonsterHitBonus { get; set; }

    // Monster magic
    public bool HasMonsterMagic { get; set; }
    public double MonsterMagerySkill { get; set; }
    public double MonsterEffectiveMagicDamageMin { get; set; }
    public double MonsterEffectiveMagicDamageMax { get; set; }

    // Special attacks
    public bool HasBreathAttack { get; set; }
    public int BreathPhysical { get; set; }
    public int BreathFire { get; set; }
    public int BreathCold { get; set; }
    public int BreathPoison { get; set; }
    public int BreathEnergy { get; set; }
    public double BreathDamageScalar { get; set; }
    public double BreathEffectiveDamageMin { get; set; }
    public double BreathEffectiveDamageMax { get; set; }

    public bool HasPoisonAttack { get; set; }
    public string PoisonName { get; set; } = "";
    public double PoisonChance { get; set; }
    public double PoisonTotalDamage { get; set; }
    public double PoisonAverageDamagePerHit { get; set; }
    public double PoisonDamagePerSecond { get; set; }
    public double PoisonDurationSeconds { get; set; }

    public bool HasLifeDrain { get; set; }
    public int LifeDrainMin { get; set; }
    public int LifeDrainMax { get; set; }
    public double LifeDrainAverage { get; set; }
    public double LifeDrainChance { get; set; }

    // Combat prediction
    public int HitsToKill { get; set; }
    public int HitsToBeKilled { get; set; }
    public int HitsToBeKilledWithDoT { get; set; }
    public double TotalMonsterDamagePerHit { get; set; }

    // Assessment
    public string Assessment { get; set; } = "";
    public string AssessmentDetail { get; set; } = "";
    public int AssessmentLevel { get; set; }

    // Real damage ranges
    public DamageRange RealPlayerDamageRange { get; set; }
    public DamageRange RealMonsterDamageRange { get; set; }
}

public class DamageRange
{
    public int Min { get; set; }
    public int Max { get; set; }
    public double Average { get; set; }
    public int SampleCount { get; set; }
}
