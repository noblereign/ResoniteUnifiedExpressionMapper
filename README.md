# UnifiedExpressionMapper

A [ResoniteModLoader](https://github.com/resonite-modding-group/ResoniteModLoader) mod for [Resonite](https://resonite.com/) that adds support for UE, ARKit, SRanipal, and FACS Reduced face tracking to the auto-assigner.
It also adds an auto-assigner for eye tracking. In theory, you should be able to import an avatar and have it fully face tracked from the get-go!

**Requires [CustomUILib](https://github.com/art0007i/CustomUILib).**

> [!IMPORTANT]
> If you think this should be a part of the base game, **please [thumbs-up the issue](https://github.com/Yellow-Dog-Man/Resonite-Issues/issues/2292)** to let the Resonite Team know to prioritize it!

## Comparison

Unlike vanilla, the mod...
- Ensures that if a blendshape has a split variant alongside a combined variant, it only drives the split ones. (Notice how vanilla drives `NoseSneer`, `LipPucker`, `MouthLowerDown`...)
- Detects and properly remaps more shapes, e.g. `CheekSquint`, `LipSuck`, `LipFunnel`, `MouthStretch`, `MouthTightener`, `MouthPress`.
- Automatically assigns eye and brow expressions. (Vanilla only does this for blink)
<img width="1411" height="1086" alt="comparison" src="https://github.com/user-attachments/assets/521359e1-c9b8-4713-835f-6a25348b21ff" />

## Installation
1. Install [ResoniteModLoader](https://github.com/resonite-modding-group/ResoniteModLoader).
2. Place [UnifiedExpressionMapper.dll](https://github.com/noblereign/UnifiedExpressionMapper/releases/latest/download/UnifiedExpressionMapper.dll) into your `rml_mods` folder. This folder should be at `C:\Program Files (x86)\Steam\steamapps\common\Resonite\rml_mods` for a default install. You can create it if it's missing, or if you launch the game once with ResoniteModLoader installed it will create this folder for you.
3. Place [CustomUILib.dll](https://github.com/art0007i/CustomUILib/releases/latest/download/CustomUILib.dll) into your `rml_mods` folder as well.
4. Start the game. If you want to verify that the mod is working you can check your Resonite logs.
