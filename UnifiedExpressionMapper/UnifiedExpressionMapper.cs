using FrooxEngine;
using FrooxEngine.CommonAvatar;
using Renderite.Shared;
using HarmonyLib;
using ResoniteModLoader;

namespace UnifiedExpressionMapper;
//More info on creating mods can be found https://github.com/resonite-modding-group/ResoniteModLoader/wiki/Creating-Mods
public class UnifiedExpressionMapper : ResoniteMod {
	internal const string VERSION_CONSTANT = "1.0.0";
	public override string Name => "UnifiedExpressionMapper";
	public override string Author => "Noble";
	public override string Version => VERSION_CONSTANT;
	public override string Link => "https://github.com/resonite-modding-group/UnifiedExpressionMapper/";

	public static ModConfiguration? Config;

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<bool> Enabled = new("Enabled", "Enables the mod, pretty self-explanatory.", () => true);


	static readonly Dictionary<string, AvatarExpression> UnifiedExpressionMap = new(StringComparer.OrdinalIgnoreCase)
	{
		["MouthSmileLeft"] = AvatarExpression.SmileLeft,
		["MouthSmileRight"] = AvatarExpression.SmileRight,
		["MouthSadLeft"] = AvatarExpression.FrownLeft,
		["MouthSadRight"] = AvatarExpression.FrownRight,
		["MouthDimpleLeft"] = AvatarExpression.MouthDimpleLeft,
		["MouthDimpleRight"] = AvatarExpression.MouthDimpleRight,
		["MouthUpperUpLeft"] = AvatarExpression.LipRaiseUpperLeft,
		["MouthUpperUpRight"] = AvatarExpression.LipRaiseUpperRight,
		["MouthLowerDownLeft"] = AvatarExpression.LipRaiseLowerLeft,
		["MouthLowerDownRight"] = AvatarExpression.LipRaiseLowerRight,
		["MouthUpperLeft"] = AvatarExpression.LipMoveLeftUpper,
		["MouthUpperRight"] = AvatarExpression.LipMoveRightUpper,
		["MouthLowerLeft"] = AvatarExpression.LipMoveLeftLower,
		["MouthLowerRight"] = AvatarExpression.LipMoveRightLower,
		["MouthPressLeft"] = AvatarExpression.LipsPressLeft,
		["MouthPressRight"] = AvatarExpression.LipsPressRight,
		["MouthRaiserUpper"] = AvatarExpression.LipOverlayUpper,
		["MouthRaiserLower"] = AvatarExpression.LipOverlayLower,
		// ["LipFunnelUpper"] = AvatarExpression.LipTopOverturn, // WHILE Mouth_Upper_Up_Left, WHILE Mouth_Upper_Up_Right 
		// ["LipFunnelLower"] = AvatarExpression.LipBottomOverturn, // WHILE Mouth_Lower_Down_Left, Mouth_Lower_Down_Right 
		["LipSuckUpper"] = AvatarExpression.LipUnderlayUpper,
		["LipSuckLower"] = AvatarExpression.LipUnderlayLower,
		["LipPucker"] = AvatarExpression.Pout,
		["MouthStretchLeft"] = AvatarExpression.LipStretchLeft,
		["MouthStretchRight"] = AvatarExpression.LipStretchRight,
		["JawOpen"] = AvatarExpression.JawOpen,
		// ["MouthClosed"] This SRanipal expression controls Unified's MouthClosed expression. To get the intended tracking on this SRanipal shape, MouthClosed must negate JawOpen in the animation proportional to the amount MouthClosed is active. Alternatively, this blendshape can be turned into MouthClosed by negating JawOpen in the blendshape itself.
		["JawLeft"] = AvatarExpression.JawLeft,
		["JawRight"] = AvatarExpression.JawRight,
		["JawForward"] = AvatarExpression.JawForward,
		["CheekPuffLeft"] = AvatarExpression.CheekPuffLeft,
		["CheekPuffRight"] = AvatarExpression.CheekPuffRight,
		["CheekSuck"] = AvatarExpression.CheekSuck,
		["CheekSquintLeft"] = AvatarExpression.CheekRaiseLeft,
		["CheekSquintRight"] = AvatarExpression.CheekRaiseRight,
		["NoseSneerLeft"] = AvatarExpression.NoseWrinkleLeft,
		["NoseSneerRight"] = AvatarExpression.NoseWrinkleRight,
		["TongueOut"] = AvatarExpression.TongueOut,
		["TongueUp"] = AvatarExpression.TongueUp,
		["TongueDown"] = AvatarExpression.TongueDown,
		["TongueLeft"] = AvatarExpression.TongueLeft,
		["TongueRight"] = AvatarExpression.TongueRight,
		["TongueRoll"] = AvatarExpression.TongueRoll
	};
	
	public override void OnEngineInit() {
		Config = GetConfiguration()!;
		Config!.Save(true);

		Harmony harmony = new("dog.glacier.UnifiedExpressionMapper");
		harmony.PatchAll();
	}

	[HarmonyPatch(typeof(AvatarExpressionDriver), "DetermineExpression")]
	class AvatarExpressionDriver_DetermineExpression_Patch {
		static void Postfix(AvatarExpressionDriver __instance, ref AvatarExpression? __result, List<string> names, string name, Chirality? chirality) {
			if (!Config!.GetValue(Enabled)) return;
			if (__result == null && UnifiedExpressionMap.TryGetValue(name, out var expression)) {
				__result = expression;
			}
		}
	}
}
