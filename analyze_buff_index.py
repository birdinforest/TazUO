#!/usr/bin/env python3
"""
Analyze BuffIcon index mechanism to determine proper range for custom icons.
"""

BUFF_ICON_START = 0x03E9  # 1001
BUFF_ICON_START_NEW = 0x466  # 1126

def calculate_index(buff_icon_value):
    """Calculate array index from BuffIconType value."""
    if buff_icon_value >= BUFF_ICON_START_NEW:
        return buff_icon_value - (BUFF_ICON_START_NEW - 125)
    else:
        return buff_icon_value - BUFF_ICON_START

def calculate_buff_icon_value(array_index):
    """Calculate BuffIconType value from array index."""
    if array_index >= 125:
        return array_index + (BUFF_ICON_START_NEW - 125)
    else:
        return array_index + BUFF_ICON_START

# Analyze current usage
print("=== BuffIcon Index Analysis ===\n")

print("Key Constants:")
print("  BUFF_ICON_START = 0x{:04X} ({})".format(BUFF_ICON_START, BUFF_ICON_START))
print("  BUFF_ICON_START_NEW = 0x{:04X} ({})".format(BUFF_ICON_START_NEW, BUFF_ICON_START_NEW))
print("  Index offset for NEW range: {} ({})\n".format(BUFF_ICON_START_NEW - 125, BUFF_ICON_START_NEW - 125))

print("Current Known Icons:")
known_icons = [
    ("CityTradeDeal", 0x466),
    ("HumilityDebuff", 0x467),
    ("BowCooldown (server)", 0x466),
]

for name, value in known_icons:
    idx = calculate_index(value)
    print("  {:25} = 0x{:04X} ({:4d}) -> Index {}".format(name, value, value, idx))

print("\n=== BuffTable Array Analysis ===")
print("Current array length: 443 elements (indices 0-442)")
print("Last index: 442")
last_value = calculate_buff_icon_value(442)
print("Last index corresponds to BuffIconType: 0x{:04X} ({})".format(last_value, last_value))

print("\n=== Recommended Custom Icon Range ===")
# Use 0x500 (1280) as starting point for custom icons
CUSTOM_ICON_START = 0x500
custom_start_idx = calculate_index(CUSTOM_ICON_START)
print("Recommended start: 0x{:04X} ({})".format(CUSTOM_ICON_START, CUSTOM_ICON_START))
print("  -> Array index: {}".format(custom_start_idx))
print("  -> Safe range: 0x{:04X} - 0x{:04X} ({} - {})".format(
    CUSTOM_ICON_START, CUSTOM_ICON_START + 100, CUSTOM_ICON_START, CUSTOM_ICON_START + 100))
print("  -> Array indices: {} - {}".format(custom_start_idx, custom_start_idx + 100))

print("\n=== For BowCooldown ===")
# Check if we can use 0x500 or need to extend array
bow_cooldown_value = 0x500
bow_cooldown_idx = calculate_index(bow_cooldown_value)
print("BowCooldown = 0x{:04X} -> Index {}".format(bow_cooldown_value, bow_cooldown_idx))
if bow_cooldown_idx >= 443:
    print("  WARNING: Index {} exceeds current array length (443)".format(bow_cooldown_idx))
    print("  Need to extend BuffTable array to at least {} elements".format(bow_cooldown_idx + 1))
else:
    print("  OK: Index {} is within current array bounds".format(bow_cooldown_idx))

