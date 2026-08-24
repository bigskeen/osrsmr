# OSRS Bridge - Development Notes & Reverse Engineering Findings
**Date:** 2026-07-17
**Scope:** RuneLite / OSRS Modern Client (64-bit JVM)

## 1. Memory Architecture Findings
*   **Architecture:** Confirmed 64-bit (x64). 
*   **Java Object Layout:** Confirmed the standard 64-bit JVM array header offset is **16 (0x10)**. 
    *   *Evidence:* Every `mov` instruction for array access (Skills, Inventory) used `[base + index*4 + 10]`.
*   **Array Expansion:** Jagex has expanded core data structures.
    *   *Skills Array:* Now indexed up to **33 (0x21)** and beyond, suggesting a length of 35-40 to accommodate new/internal skills.
    *   *Inventory:* Standard length 28 remains, but the backing arrays are often larger (up to 32-40) in recent client builds.

## 2. Field Obfuscation (Modular Multipliers)
*   **The Mechanism:** Most `int` fields (GameState, Skills, Worlds) are encoded using modular arithmetic.
*   **Discovered Multipliers:**
    *   Significant new multipliers found: `-102938475`, `762384913`, `1140066225`.
*   **Decoding Formula:** `RealValue = MemoryValue * Multiplier`.
*   **Update Detection:** The client uses `mov [rdx+rbx*4+10],eax` for updating skills, where `EAX` is the already-encoded value.

## 3. World & Server Data
*   **World Storage:** The client uses both "Absolute" and "Relative" world IDs.
    *   *Relative (Sub-World):* Stored as `WorldID - 300` (e.g., World 301 is stored as `1`).
*   **Indexing:** World data is accessed via dynamic offsets (e.g., `mov eax, [rax+rbx]` with `rbx=0x14`). This indicates the current world is likely a field within a global `World` or `Client` object rather than a simple static.

## 4. Player & NPC Tracking
*   **Coordinates:**
    *   **Fine Coords:** Stored as `float` fields in the `Player` class (observed in `XMM` registers).
    *   **Coarse Coords:** Stored as `int` fields, often with large multipliers (range 1,000 to 20,000 for region-based absolute coords).
*   **NPC IDs:** Identified as `int` fields within the NPC object, usually ranging from `1` to `15,000`.

## 5. Implementation Roadmap Updates
*   **Agent Scanning:** Heuristics updated to scan for arrays of length **28-50** instead of strictly 28.
*   **Normalization:** Added a "+300" offset handler for world fields that report in the 1-200 range.
*   **Player Class:** LocalPlayer is identified by looking for a class with >50 integer fields (heavy Actor/Player characteristics).

---
*Note: These findings were derived from real-time memory disassembly and register analysis of the active RuneLite process.*
