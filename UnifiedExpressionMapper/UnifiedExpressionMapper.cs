using FrooxEngine;
using FrooxEngine.CommonAvatar;
using FrooxEngine.UIX;
using HarmonyLib;
using Renderite.Shared;
using ResoniteModLoader;
using CustomUILib;

namespace UnifiedExpressionMapper;

internal class AutoAssignContext {
	public HashSet<string> BlendshapeNames = new(StringComparer.OrdinalIgnoreCase);
	public HashSet<string> SuppressedShapes = new(StringComparer.OrdinalIgnoreCase);
	public Dictionary<string, AvatarExpression> MappedShapes = new(StringComparer.OrdinalIgnoreCase);
}
internal enum FallbackStrategy {
	NoStandardDetected,
	Never,
	Always
}

//More info on creating mods can be found https://github.com/resonite-modding-group/ResoniteModLoader/wiki/Creating-Mods
public class UnifiedExpressionMapper : ResoniteMod {
	internal const string VERSION_CONSTANT = "1.1.1";
	public override string Name => "UnifiedExpressionMapper";
	public override string Author => "Noble";
	public override string Version => VERSION_CONSTANT;
	public override string Link => "https://github.com/resonite-modding-group/UnifiedExpressionMapper/";

	public static ModConfiguration? Config;

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<bool> Enabled = new("Enabled", "Enables the mod, pretty self-explanatory.", () => true);

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<bool> AssignEyesOnCreation = new("Auto-assign eye shapes on avatar creation", "When finalizing the avatar in the Avatar Creator, should eyetracking blendshapes be auto-assigned?", () => true);

	[AutoRegisterConfigKey]
	private static readonly ModConfigurationKey<FallbackStrategy> UseHeurstics = new("Fallback Strategy", "When should blendshapes be assigned based on vanilla heuristics? The available options are:\n\n<color=hero.yellow>NoStandardDetected</color> - Only when no other blendshapes were matched by the mod; the avatar's shape names are unconventional, or maybe has no true face-tracking at all.\n\n<color=hero.yellow>Never</color> - Never use heurstics, leave unknown blendshapes unassigned.\n\n<color=hero.yellow>Always</color> - Always fall back if the blendshape doesn't match any standardized naming.", () => FallbackStrategy.NoStandardDetected);

	[ThreadStatic]
	internal static AutoAssignContext? CurrentContext;

	static readonly Dictionary<string, AvatarExpression> BaseUnifiedExpressionMap = new(StringComparer.OrdinalIgnoreCase) { // really hope these are right, i don't have an avatar with base shapes 😭
		["NoseSneerRight"] = AvatarExpression.NoseWrinkleRight,
		["NoseSneerLeft"] = AvatarExpression.NoseWrinkleLeft,

		["CheekSquintRight"] = AvatarExpression.CheekRaiseRight,
		["CheekSquintLeft"] = AvatarExpression.CheekRaiseLeft,
		["CheekPuffRight"] = AvatarExpression.CheekPuffRight,
		["CheekPuffLeft"] = AvatarExpression.CheekPuffLeft,
		["CheekSuckRight"] = AvatarExpression.CheekSuckRight,
		["CheekSuckLeft"] = AvatarExpression.CheekSuckLeft,

		["JawOpen"] = AvatarExpression.JawOpen,
		["JawRight"] = AvatarExpression.JawRight,
		["JawLeft"] = AvatarExpression.JawLeft,
		["JawForward"] = AvatarExpression.JawForward,

		["LipSuckUpperRight"] = AvatarExpression.LipUnderlayUpperRight,
		["LipSuckUpperLeft"] = AvatarExpression.LipUnderlayUpperLeft,
		["LipSuckLowerRight"] = AvatarExpression.LipUnderlayLowerRight,
		["LipSuckLowerLeft"] = AvatarExpression.LipUnderlayLowerLeft,

		["LipFunnelUpperRight"] = AvatarExpression.LipTopRightOverturn,
		["LipFunnelUpperLeft"] = AvatarExpression.LipTopLeftOverturn,
		["LipFunnelLowerRight"] = AvatarExpression.LipBottomRightOverturn,
		["LipFunnelLowerLeft"] = AvatarExpression.LipBottomLeftOverturn,

		["LipPuckerUpperRight"] = AvatarExpression.PoutRight,
		["LipPuckerUpperLeft"] = AvatarExpression.PoutLeft,
		["LipPuckerLowerRight"] = AvatarExpression.PoutRight,
		["LipPuckerLowerLeft"] = AvatarExpression.PoutLeft,

		["MouthUpperUpRight"] = AvatarExpression.LipRaiseUpperRight,
		["MouthUpperUpLeft"] = AvatarExpression.LipRaiseUpperLeft,
		["MouthLowerDownRight"] = AvatarExpression.LipRaiseLowerRight,
		["MouthLowerDownLeft"] = AvatarExpression.LipRaiseLowerLeft,
		["MouthUpperRight"] = AvatarExpression.LipMoveRightUpper,
		["MouthUpperLeft"] = AvatarExpression.LipMoveLeftUpper,
		["MouthLowerRight"] = AvatarExpression.LipMoveRightLower,
		["MouthLowerLeft"] = AvatarExpression.LipMoveLeftLower,

		["MouthCornerPullRight"] = AvatarExpression.SmileRight,
		["MouthCornerPullLeft"] = AvatarExpression.SmileLeft,

		["MouthFrown"] = AvatarExpression.Frown, // since this is combined it wouldn't be a base shape, but maybe someone will misname it one day and this will save them 😭
		["MouthFrownLeft"] = AvatarExpression.FrownLeft,
		["MouthFrownRight"] = AvatarExpression.FrownRight,

		["MouthStretchRight"] = AvatarExpression.LipStretchRight,
		["MouthStretchLeft"] = AvatarExpression.LipStretchLeft,
		["MouthDimpleRight"] = AvatarExpression.MouthDimpleRight,
		["MouthDimpleLeft"] = AvatarExpression.MouthDimpleLeft,
		["MouthRaiserUpper"] = AvatarExpression.LipRaiseUpper,
		["MouthRaiserLower"] = AvatarExpression.LipRaiseLower,
		["MouthPressRight"] = AvatarExpression.LipsPressRight,
		["MouthPressLeft"] = AvatarExpression.LipsPressLeft,
		["MouthTightenerRight"] = AvatarExpression.LipTightenRight,
		["MouthTightenerLeft"] = AvatarExpression.LipTightenLeft,

		["TongueOut"] = AvatarExpression.TongueOut,
		["TongueUp"] = AvatarExpression.TongueUp,
		["TongueDown"] = AvatarExpression.TongueDown,
		["TongueRight"] = AvatarExpression.TongueRight,
		["TongueLeft"] = AvatarExpression.TongueLeft,
		["TongueRoll"] = AvatarExpression.TongueRoll,

		//approximations
		["JawMandibleRaise"] = AvatarExpression.ChinRaiseBottom, // just from reading the docs i thiiiink this is a good substitute?????

		["MouthCornerSlantRight"] = AvatarExpression.SmileRight, // This SRanipal expression directly controls Unified's MouthCornerPullerRight and MouthCornerSlantRight, creating the MouthSmileRight Blended shape. On many avatars and on the SRanipal reference avatar, the right cheek is squinting when smiling. It is perfectly acceptable to have MouthSmileRight to have this behavior, especially when using SRanipal-compatible tracking with this avatar.
		["MouthCornerSlantLeft"] = AvatarExpression.SmileLeft, // supposedly this means they can be "coupled" i guess??? maybe???? idk i hope i understood it right

		["LipSuckCornerRight"] = AvatarExpression.LipUnderlayUpperRight,
		["LipSuckCornerLeft"] = AvatarExpression.LipUnderlayUpperLeft,

		//maybe one day resonite will have these tracked...
		//["NasalDilationRight"] = null,
		//["NasalDilationLeft"] = null,
		//["NasalConstrictRight"] = null,
		//["NasalConstrictLeft"] = null,
		//["MouthClosed"] = null,
		//["JawBackward"] = null,
		//["JawClench"] = null,
		//["MouthUpperDeepenRight"] = null,
		//["MouthUpperDeepenLeft"] = null,
		//["TongueBendDown"] = null,
		//["TongueCurlUp"] = null,
		//["TongueSquish"] = null,
		//["TongueFlat"] = null,
		//["TongueTwistRight"] = null,
		//["TongueTwistLeft"] = null,
		//["SoftPalateClose"] = null,
		//["ThroatSwallow"] = null,
		//["NeckFlexRight"] = null,
		//["NeckFlexLeft"] = null
	};

	static readonly Dictionary<string, AvatarExpression> UnifiedExpressionMap = new(StringComparer.OrdinalIgnoreCase) {
		["NoseSneer"] = AvatarExpression.NoseWrinkle,
		["NoseSneerLeft"] = AvatarExpression.NoseWrinkleLeft,
		["NoseSneerRight"] = AvatarExpression.NoseWrinkleRight,
		["CheekSquint"] = AvatarExpression.CheekRaise,
		["CheekSquintLeft"] = AvatarExpression.CheekRaiseLeft,
		["CheekSquintRight"] = AvatarExpression.CheekRaiseRight,
		["CheekPuff"] = AvatarExpression.CheekPuff,
		["CheekPuffLeft"] = AvatarExpression.CheekPuffLeft,
		["CheekPuffRight"] = AvatarExpression.CheekPuffRight,
		["CheekSuck"] = AvatarExpression.CheekSuck,
		["CheekSuckLeft"] = AvatarExpression.CheekSuckLeft,
		["CheekSuckRight"] = AvatarExpression.CheekSuckRight,

		["JawOpen"] = AvatarExpression.JawOpen,
		["MouthApeShape"] = AvatarExpression.JawDown,
		// ["MouthClosed"] This SRanipal expression controls Unified's MouthClosed expression. To get the intended tracking on this SRanipal shape, MouthClosed must negate JawOpen in the animation proportional to the amount MouthClosed is active. Alternatively, this blendshape can be turned into MouthClosed by negating JawOpen in the blendshape itself.
		["JawLeft"] = AvatarExpression.JawLeft,
		["JawRight"] = AvatarExpression.JawRight,
		["JawForward"] = AvatarExpression.JawForward,

		["LipSuck"] = AvatarExpression.LipUnderlayUpper,
		["LipSuckUpper"] = AvatarExpression.LipUnderlayUpper,
		["LipSuckUpperLeft"] = AvatarExpression.LipUnderlayUpperLeft,
		["LipSuckUpperRight"] = AvatarExpression.LipUnderlayUpperRight,
		["LipSuckLower"] = AvatarExpression.LipUnderlayLower,
		["LipSuckLowerLeft"] = AvatarExpression.LipUnderlayLowerLeft,
		["LipSuckLowerRight"] = AvatarExpression.LipUnderlayLowerRight,

		["LipFunnel"] = AvatarExpression.LipTopOverturn,
		["LipFunnelUpper"] = AvatarExpression.LipTopOverturn,
		["LipFunnelUpperLeft"] = AvatarExpression.LipTopLeftOverturn,
		["LipFunnelUpperRight"] = AvatarExpression.LipTopRightOverturn,
		["LipFunnelLower"] = AvatarExpression.LipBottomOverturn,
		["LipFunnelLowerLeft"] = AvatarExpression.LipBottomLeftOverturn,
		["LipFunnelLowerRight"] = AvatarExpression.LipBottomRightOverturn,

		["LipPucker"] = AvatarExpression.Pout,
		["LipPuckerLeft"] = AvatarExpression.PoutLeft, // THESE TWO...
		["LipPuckerRight"] = AvatarExpression.PoutRight, // ...ARENT STANDARD!!
		["LipPuckerUpper"] = AvatarExpression.Pout, // for some reason, resonite has left and right splits for pucker, but not upper and lower like the blended shapes do!
		["LipPuckerLower"] = AvatarExpression.Pout, // which means sadly, we have to just bind these both to combined pout, otherwise it'd make no sense... :(

		["MouthUpperUp"] = AvatarExpression.LipRaiseUpper,
		["MouthUpperUpLeft"] = AvatarExpression.LipRaiseUpperLeft,
		["MouthUpperUpRight"] = AvatarExpression.LipRaiseUpperRight,
		["MouthLowerDown"] = AvatarExpression.LipRaiseLower,
		["MouthLowerDownLeft"] = AvatarExpression.LipRaiseLowerLeft,
		["MouthLowerDownRight"] = AvatarExpression.LipRaiseLowerRight,

		["MouthUpperLeft"] = AvatarExpression.LipMoveLeftUpper,
		["MouthLowerLeft"] = AvatarExpression.LipMoveLeftLower,
		["MouthUpperRight"] = AvatarExpression.LipMoveRightUpper,
		["MouthLowerRight"] = AvatarExpression.LipMoveRightLower,

		["MouthSmile"] = AvatarExpression.Smile,
		["MouthSmileLeft"] = AvatarExpression.SmileLeft,
		["MouthSmileRight"] = AvatarExpression.SmileRight,

		["MouthSad"] = AvatarExpression.Frown,
		["MouthSadLeft"] = AvatarExpression.FrownLeft,
		["MouthSadRight"] = AvatarExpression.FrownRight,

		["MouthStretch"] = AvatarExpression.LipStretch,
		["MouthStretchLeft"] = AvatarExpression.LipStretchLeft,
		["MouthStretchRight"] = AvatarExpression.LipStretchRight,
		["MouthDimple"] = AvatarExpression.MouthDimple,
		["MouthDimpleLeft"] = AvatarExpression.MouthDimpleLeft,
		["MouthDimpleRight"] = AvatarExpression.MouthDimpleRight,
		["MouthRaiserUpper"] = AvatarExpression.LipOverlayUpper,
		["MouthRaiserLower"] = AvatarExpression.LipOverlayLower,
		["MouthPress"] = AvatarExpression.LipsPress,
		["MouthPressLeft"] = AvatarExpression.LipsPressLeft,
		["MouthPressRight"] = AvatarExpression.LipsPressRight,
		["MouthTightener"] = AvatarExpression.LipTighten,
		["MouthTightenerLeft"] = AvatarExpression.LipTightenLeft,
		["MouthTightenerRight"] = AvatarExpression.LipTightenRight,

		["TongueOut"] = AvatarExpression.TongueOut,
		["TongueOutStep1"] = AvatarExpression.TongueOut,
		["TongueOutStep2"] = AvatarExpression.TongueExtend,
		["TongueDown"] = AvatarExpression.TongueDown,
		["TongueUp"] = AvatarExpression.TongueUp,
		["TongueLeft"] = AvatarExpression.TongueLeft,
		["TongueRight"] = AvatarExpression.TongueRight,
		["TongueUpLeftMorph"] = AvatarExpression.TongueUpLeft,
		["TongueUpRightMorph"] = AvatarExpression.TongueUpRight,
		["TongueDownLeftMorph"] = AvatarExpression.TongueDownLeft,
		["TongueDownRightMorph"] = AvatarExpression.TongueDownRight,
		["TongueRoll"] = AvatarExpression.TongueRoll
	};

	static readonly Dictionary<string, AvatarExpression> SRanipalExpressionMap = new(StringComparer.OrdinalIgnoreCase) {
		["Mouth_Ape_Shape"] = AvatarExpression.JawDown,
		["Jaw_Open"] = AvatarExpression.JawOpen,
		["Jaw_Left"] = AvatarExpression.JawLeft,
		["Jaw_Right"] = AvatarExpression.JawRight,
		["Jaw_Forward"] = AvatarExpression.JawForward,
		["Mouth_Upper_Inside"] = AvatarExpression.LipUnderlayUpper,
		["Mouth_Lower_Inside"] = AvatarExpression.LipUnderlayLower,
		["Mouth_Upper_Overturn"] = AvatarExpression.LipTopOverturn,
		["Mouth_Lower_Overturn"] = AvatarExpression.LipBottomOverturn,
		["Mouth_Pout"] = AvatarExpression.Pout,
		["Mouth_Upper_UpLeft"] = AvatarExpression.LipRaiseUpperLeft,
		["Mouth_Upper_UpRight"] = AvatarExpression.LipRaiseUpperRight,
		["Mouth_Lower_DownLeft"] = AvatarExpression.LipRaiseLowerLeft,
		["Mouth_Lower_DownRight"] = AvatarExpression.LipRaiseLowerRight,
		["Mouth_Upper_Left"] = AvatarExpression.LipMoveLeftUpper,
		["Mouth_Lower_Left"] = AvatarExpression.LipMoveLeftLower,
		["Mouth_Upper_Right"] = AvatarExpression.LipMoveRightUpper,
		["Mouth_Lower_Right"] = AvatarExpression.LipMoveRightLower,
		["Mouth_Smile_Left"] = AvatarExpression.SmileLeft,
		["Mouth_Smile_Right"] = AvatarExpression.SmileRight,
		["Mouth_Sad_Left"] = AvatarExpression.FrownLeft,
		["Mouth_Sad_Right"] = AvatarExpression.FrownRight,
		["Mouth_Lower_Overlay"] = AvatarExpression.LipOverlayLower,
		["Tongue_LongStep1"] = AvatarExpression.TongueOut,
		["Tongue_LongStep2"] = AvatarExpression.TongueExtend,
		["Tongue_Down"] = AvatarExpression.TongueDown,
		["Tongue_Up"] = AvatarExpression.TongueUp,
		["Tongue_Left"] = AvatarExpression.TongueLeft,
		["Tongue_Right"] = AvatarExpression.TongueRight,
		["Tongue_UpLeft_Morph"] = AvatarExpression.TongueUpLeft,
		["Tongue_UpRight_Morph"] = AvatarExpression.TongueUpRight,
		["Tongue_DownLeft_Morph"] = AvatarExpression.TongueDownLeft,
		["Tongue_DownRight_Morph"] = AvatarExpression.TongueDownRight,
		["Tongue_Roll"] = AvatarExpression.TongueRoll,
		["Cheek_Puff_Left"] = AvatarExpression.CheekPuffLeft,
		["Cheek_Puff_Right"] = AvatarExpression.CheekPuffRight,
		["Cheek_Suck"] = AvatarExpression.CheekSuck,
		["Mouth_O_Shape"] = AvatarExpression.LipTopOverturn,
	};

	static readonly Dictionary<string, AvatarExpression> ARKitExpressionMap = new(StringComparer.OrdinalIgnoreCase) {
		["jawOpen"] = AvatarExpression.JawOpen,
		["jawLeft"] = AvatarExpression.JawLeft,
		["jawRight"] = AvatarExpression.JawRight,
		["jawForward"] = AvatarExpression.JawForward,
		//["mouthClose"] = AvatarExpression.MouthClosed,
		["mouthRollUpper"] = AvatarExpression.LipUnderlayUpper,
		["mouthRollLower"] = AvatarExpression.LipUnderlayLower,
		["mouthPucker"] = AvatarExpression.Pout,
		["mouthFunnel"] = AvatarExpression.LipTopOverturn,
		["mouthUpperUp"] = AvatarExpression.LipRaiseUpper,
		["mouthUpperUpLeft"] = AvatarExpression.LipRaiseUpperLeft,
		["mouthUpperUpRight"] = AvatarExpression.LipRaiseUpperRight,
		["mouthLowerDown"] = AvatarExpression.LipRaiseLower,
		["mouthLowerDownLeft"] = AvatarExpression.LipRaiseLowerLeft,
		["mouthLowerDownRight"] = AvatarExpression.LipRaiseLowerRight,
		["mouthLeft"] = AvatarExpression.LipMoveLeftLower,
		["mouthRight"] = AvatarExpression.LipMoveRightLower,
		["mouthSmile"] = AvatarExpression.Smile,
		["mouthSmileLeft"] = AvatarExpression.SmileLeft,
		["mouthSmileRight"] = AvatarExpression.SmileRight,
		["mouthFrown"] = AvatarExpression.Frown,
		["mouthFrownLeft"] = AvatarExpression.FrownLeft,
		["mouthFrownRight"] = AvatarExpression.FrownRight,
		["mouthStretch"] = AvatarExpression.LipStretch,
		["mouthStretchLeft"] = AvatarExpression.LipStretchLeft,
		["mouthStretchRight"] = AvatarExpression.LipStretchRight,
		["mouthDimple"] = AvatarExpression.MouthDimple,
		["mouthDimpleLeft"] = AvatarExpression.MouthDimpleLeft,
		["mouthDimpleRight"] = AvatarExpression.MouthDimpleRight,
		["mouthShrug"] = AvatarExpression.LipOverlayUpper,
		["mouthShrugUpper"] = AvatarExpression.LipOverlayUpper,
		["mouthShrugLower"] = AvatarExpression.LipOverlayLower,
		["mouthPress"] = AvatarExpression.LipsPress,
		["mouthPressLeft"] = AvatarExpression.LipsPressLeft,
		["mouthPressRight"] = AvatarExpression.LipsPressRight,
		["tongueOut"] = AvatarExpression.TongueOut,
		["cheekPuff"] = AvatarExpression.CheekPuff,
		["cheekSquint"] = AvatarExpression.CheekRaise,
		["cheekSquintLeft"] = AvatarExpression.CheekRaiseLeft,
		["cheekSquintRight"] = AvatarExpression.CheekRaiseRight,
		["noseSneer"] = AvatarExpression.NoseWrinkle,
		["noseSneerLeft"] = AvatarExpression.NoseWrinkleLeft,
		["noseSneerRight"] = AvatarExpression.NoseWrinkleRight
	};

	static readonly Dictionary<string, AvatarExpression> FACSExpressionMap = new(StringComparer.OrdinalIgnoreCase) {
		["Jaw_Drop"] = AvatarExpression.JawOpen,
		["Jaw_Sideways_Left"] = AvatarExpression.JawLeft,
		["Jaw_Sideways_Right"] = AvatarExpression.JawRight,
		["Jaw_Thrust"] = AvatarExpression.JawForward,
		["Lip_Suck"] = AvatarExpression.LipUnderlayUpper,
		["Lip_Suck_LT"] = AvatarExpression.LipUnderlayUpperLeft,
		["Lip_Suck_RT"] = AvatarExpression.LipUnderlayUpperRight,
		["Lip_Suck_LB"] = AvatarExpression.LipUnderlayLowerLeft,
		["Lip_Suck_RB"] = AvatarExpression.LipUnderlayLowerRight,
		["Lip_Funneler"] = AvatarExpression.LipTopOverturn,
		["Lip_Funneler_LT"] = AvatarExpression.LipTopLeftOverturn,
		["Lip_Funneler_RT"] = AvatarExpression.LipTopRightOverturn,
		["Lip_Funneler_LB"] = AvatarExpression.LipBottomLeftOverturn,
		["Lip_Funneler_RB"] = AvatarExpression.LipBottomRightOverturn,
		["Lip_Pucker"] = AvatarExpression.Pout,
		["Lip_Pucker_L"] = AvatarExpression.PoutLeft,
		["Lip_Pucker_R"] = AvatarExpression.PoutRight,
		["Upper_Lip_Raiser"] = AvatarExpression.LipRaiseUpper,
		["Upper_Lip_Raiser_L"] = AvatarExpression.LipRaiseUpperLeft,
		["Upper_Lip_Raiser_R"] = AvatarExpression.LipRaiseUpperRight,
		["Lower_Lip_Depressor"] = AvatarExpression.LipRaiseLower,
		["Lower_Lip_Depressor_L"] = AvatarExpression.LipRaiseLowerLeft,
		["Lower_Lip_Depressor_R"] = AvatarExpression.LipRaiseLowerRight,
		["Mouth_Left"] = AvatarExpression.LipMoveLeftLower,
		["Mouth_Right"] = AvatarExpression.LipMoveRightLower,
		["Lip_Corner_Puller"] = AvatarExpression.Smile,
		["Lip_Corner_Puller_L"] = AvatarExpression.SmileLeft,
		["Lip_Corner_Puller_R"] = AvatarExpression.SmileRight,
		["Lip_Corner_Depressor"] = AvatarExpression.Frown,
		["Lip_Corner_Depressor_L"] = AvatarExpression.FrownLeft,
		["Lip_Corner_Depressor_R"] = AvatarExpression.FrownRight,
		["Lip_Stretcher"] = AvatarExpression.LipStretch,
		["Lip_Stretcher_L"] = AvatarExpression.LipStretchLeft,
		["Lip_Stretcher_R"] = AvatarExpression.LipStretchRight,
		["Dimpler"] = AvatarExpression.MouthDimple,
		["Dimpler_L"] = AvatarExpression.MouthDimpleLeft,
		["Dimpler_R"] = AvatarExpression.MouthDimpleRight,
		["Chin_Raiser"] = AvatarExpression.LipOverlayUpper,
		["Chin_Raiser_T"] = AvatarExpression.LipOverlayUpper,
		["Chin_Raiser_B"] = AvatarExpression.LipOverlayLower,
		["Lip_Pressor"] = AvatarExpression.LipsPress,
		["Lip_Pressor_L"] = AvatarExpression.LipsPressLeft,
		["Lip_Pressor_R"] = AvatarExpression.LipsPressRight,
		["Lip_Tightener"] = AvatarExpression.LipTighten,
		["Lip_Tightener_L"] = AvatarExpression.LipTightenLeft,
		["Lip_Tightener_R"] = AvatarExpression.LipTightenRight,
		["Cheek_Puff"] = AvatarExpression.CheekPuff,
		["Cheek_Puff_L"] = AvatarExpression.CheekPuffLeft,
		["Cheek_Puff_R"] = AvatarExpression.CheekPuffRight,
		["Cheek_Suck"] = AvatarExpression.CheekSuck,
		["Cheek_Suck_L"] = AvatarExpression.CheekSuckLeft,
		["Cheek_Suck_R"] = AvatarExpression.CheekSuckRight,
		["Cheek_Raiser"] = AvatarExpression.CheekRaise,
		["Cheek_Raiser_L"] = AvatarExpression.CheekRaiseLeft,
		["Cheek_Raiser_R"] = AvatarExpression.CheekRaiseRight,
		["Nose_Wrinkler"] = AvatarExpression.NoseWrinkle,
		["Nose_Wrinkler_L"] = AvatarExpression.NoseWrinkleLeft,
		["Nose_Wrinkler_R"] = AvatarExpression.NoseWrinkleRight
	};
	public override void OnEngineInit() {
		Config = GetConfiguration()!;
		Config!.Save(true);

		Harmony harmony = new("dog.glacier.UnifiedExpressionMapper");
		harmony.PatchAll();

		CustomUILib.CustomUILib.AddCustomInspectorAfter<EyeLinearDriver>(BuildInspectorUI);
	}

	[HarmonyPatch(typeof(AvatarExpressionDriver), "DetermineExpression")]
	class AvatarExpressionDriver_DetermineExpression_Patch {
		static bool Prefix(ref AvatarExpression? __result, List<string> names, string name, Chirality? chirality) {
			if (!Config!.GetValue(Enabled)) return true;

			if (CurrentContext != null) {
				// if the shape has split versions, then don't assign the combined one
				if (CurrentContext.SuppressedShapes.Contains(name)) {
					__result = null;
					return false;
				}

				if (CurrentContext.MappedShapes.TryGetValue(name, out var winnerExpr)) {
					__result = winnerExpr;
					return false;
				}

				// if the shape is part of a map but isn't the 'winner', then there's already another shape driving that expression
				if (BaseUnifiedExpressionMap.ContainsKey(name) ||
					UnifiedExpressionMap.ContainsKey(name) ||
					ARKitExpressionMap.ContainsKey(name) ||
					SRanipalExpressionMap.ContainsKey(name) ||
					FACSExpressionMap.ContainsKey(name)) {
					__result = null;
					return false;
				}
			} else {
				// fallback just in case autoassign has no context somehow
				if (BaseUnifiedExpressionMap.TryGetValue(name, out var baseUnifiedConversion)) {
					__result = baseUnifiedConversion; return false;
				}
				if (UnifiedExpressionMap.TryGetValue(name, out var unifiedConversion)) {
					__result = unifiedConversion; return false;
				}
				if (ARKitExpressionMap.TryGetValue(name, out var ARKitConversion)) {
					__result = ARKitConversion; return false;
				}
				if (SRanipalExpressionMap.TryGetValue(name, out var SRanipalConversion)) {
					__result = SRanipalConversion; return false;
				}
				if (FACSExpressionMap.TryGetValue(name, out var FACSConversion)) {
					__result = FACSConversion; return false;
				}
			}

			FallbackStrategy CurrentStrategy = Config!.GetValue(UseHeurstics);
			if (CurrentStrategy == FallbackStrategy.Always) {
				return true; // frooxengine heuristics time
			} else if (CurrentStrategy == FallbackStrategy.Never) {
				__result = null;
				return false;
			} else {
				if (CurrentContext != null && CurrentContext.MappedShapes.Count > 0) {
					__result = null;
					return false;
				} else {
					return true;
				}
			}
		}
	}

	[HarmonyPatch(typeof(AvatarExpressionDriver), "AutoAssign", typeof(SkinnedMeshRenderer))]
	class AvatarExpressionDriver_AutoAssign_Patch {
		static void Prefix(SkinnedMeshRenderer skin) {
			if (!Config!.GetValue(Enabled)) return;

			var ctx = new AutoAssignContext();
			CurrentContext = ctx;

			for (int i = 0; i < skin.MeshBlendshapeCount; i++) {
				ctx.BlendshapeNames.Add(skin.BlendShapeName(i));
			}

			// checking for any combined shapes that also have split shapes, so we can ignore them later
			foreach (var bName in ctx.BlendshapeNames) {
				string lowerName = bName.ToLowerInvariant();

				bool hasLeft = ctx.BlendshapeNames.Contains(bName + "Left") ||
							   ctx.BlendshapeNames.Contains(bName + "_L") ||
							   ctx.BlendshapeNames.Contains(bName + "_Left");

				bool hasRight = ctx.BlendshapeNames.Contains(bName + "Right") ||
								ctx.BlendshapeNames.Contains(bName + "_R") ||
								ctx.BlendshapeNames.Contains(bName + "_Right");

				if (hasLeft && hasRight) {
					ctx.SuppressedShapes.Add(bName);
				}

				bool hasUpper = ctx.BlendshapeNames.Contains(bName + "Upper") ||
								ctx.BlendshapeNames.Contains(bName + "Top") ||
								ctx.BlendshapeNames.Contains(bName + "Up");

				bool hasLower = ctx.BlendshapeNames.Contains(bName + "Lower") ||
								ctx.BlendshapeNames.Contains(bName + "Bottom") ||
								ctx.BlendshapeNames.Contains(bName + "Down");

				if (hasUpper && hasLower) {
					ctx.SuppressedShapes.Add(bName);
				}

				// if you name it with 'both' for some reason
				if (lowerName.Contains("both")) {
					string leftVersion = lowerName.Replace("both", "left");
					string rightVersion = lowerName.Replace("both", "right");

					if (ctx.BlendshapeNames.Contains(leftVersion) && ctx.BlendshapeNames.Contains(rightVersion)) {
						ctx.SuppressedShapes.Add(bName);
					}
				}
			}

			var expressionToStandard = new Dictionary<AvatarExpression, int>();

			void TryMapStandard(Dictionary<string, AvatarExpression> expressionMap, int priority) {
				foreach (var kvp in expressionMap) {
					string shapeName = kvp.Key;
					AvatarExpression expr = kvp.Value;

					if (ctx.BlendshapeNames.Contains(shapeName) && !ctx.SuppressedShapes.Contains(shapeName)) {

						// only allow mapping to the same expression multiple times if it's from the same standard
						if (!expressionToStandard.TryGetValue(expr, out int currentPriority) || currentPriority == priority) {
							expressionToStandard[expr] = priority;
							ctx.MappedShapes[shapeName] = expr;
						}
					}
				}
			}

			TryMapStandard(BaseUnifiedExpressionMap, 0);
			TryMapStandard(UnifiedExpressionMap, 1);
			TryMapStandard(ARKitExpressionMap, 2);
			TryMapStandard(SRanipalExpressionMap, 3);
			TryMapStandard(FACSExpressionMap, 4);
		}

		static void Postfix() {
			CurrentContext = null;
		}
	}




	[HarmonyPatch(typeof(AvatarCreator), "SetupEyes")]
	class AvatarCreator_SetupEyes_Patch {
		static void Postfix(Slot headReference, Slot leftEye, Slot rightEye, BipedRig rig, Slot avatarRoot) {
			if (!Config!.GetValue(Enabled)) { Msg($"UnifiedExpressionMapper is disabled, so not automatically setting up eye shapes for {avatarRoot.Name}"); return; };
			if (!Config!.GetValue(AssignEyesOnCreation)) { Msg($"Assign Eyes On Creation is disabled, so not automatically setting up eye shapes for {avatarRoot.Name}"); return; };
			Slot eyeManagerRoot = rig[BodyNode.Head].FindChild("Eye Manager");
			if (eyeManagerRoot == null) { Warn($"Could not find eye manager root for {avatarRoot.Name}!"); return; };
			EyeLinearDriver linearDriver = eyeManagerRoot.GetComponent<EyeLinearDriver>();
			if (linearDriver == null) { Warn($"Could not find eye linear driver for {avatarRoot.Name}"); return; };
			AssignEyeShapes(linearDriver);
		}
	}


	// referencing https://github.com/djsime1/ResoniteAvatarClothingHelper/blob/master/AvatarClothingHelper/AvatarClothingHelper.cs#L35 for finding the primary renderer
	public static void AssignEyeShapes(EyeLinearDriver eyeLinearDriver, SkinnedMeshRenderer? primaryRenderer = null) {
		var skinnedRenderers = eyeLinearDriver.Slot.GetObjectRoot().GetComponentsInChildren<SkinnedMeshRenderer>(renderer => renderer.MeshBlendshapeCount > 0).ToArray();
		primaryRenderer = primaryRenderer ?? skinnedRenderers.OrderByDescending(renderer => renderer.MeshBlendshapeCount).First();

		if (primaryRenderer == null) { Warn($"Couldn't find a primary renderer for {eyeLinearDriver.Slot.GetObjectRoot().Name}"); return; };

		var blendshapes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		for (int i = 0; i < primaryRenderer.MeshBlendshapeCount; i++) {
			string shapeName = primaryRenderer.BlendShapeName(i);
			blendshapes[shapeName] = shapeName;
		}

		EyeLinearDriver.Eye GetEye(EyeSide side) {
			var eye = eyeLinearDriver.Eyes.FirstOrDefault(e => e.Side.Value == side);
			if (eye == null) {
				eye = eyeLinearDriver.Eyes.Add();
				eye.Side.Value = side;
			}
			if (eye.ProjectionPlanePoint.Target == null) {
				eye.ProjectionPlanePoint.Target = eyeLinearDriver.Slot;
			}
			return eye;
		}

		void AssignFeature(Func<EyeLinearDriver.Eye, FieldDrive<float>> fieldSelector, string[] leftNames, string[] rightNames, string[] combinedNames) {
			string leftMatch = leftNames.FirstOrDefault(n => blendshapes.ContainsKey(n));
			string rightMatch = rightNames.FirstOrDefault(n => blendshapes.ContainsKey(n));

			// Prioritize split shapes if at least one exists
			if (leftMatch != null || rightMatch != null) {
				if (leftMatch != null) {
					var eye = GetEye(EyeSide.Left);
					fieldSelector(eye).Target = primaryRenderer.TryGetBlendShape(blendshapes[leftMatch]);
				}
				if (rightMatch != null) {
					var eye = GetEye(EyeSide.Right);
					fieldSelector(eye).Target = primaryRenderer.TryGetBlendShape(blendshapes[rightMatch]);
				}
			}
			// Fallback to combined shapes if no split versions exist
			else if (combinedNames != null) {
				string combinedMatch = combinedNames.FirstOrDefault(n => blendshapes.ContainsKey(n));
				if (combinedMatch != null) {
					var eye = GetEye(EyeSide.Combined);
					fieldSelector(eye).Target = primaryRenderer.TryGetBlendShape(blendshapes[combinedMatch]);
				}
			}
		}

		AssignFeature(e => e.LookUp,
			new[] { "EyeLookUpLeft", "eyeLookUpLeft", "Eye_Left_Up", "Eyes_Look_Up_L" },
			new[] { "EyeLookUpRight", "eyeLookUpRight", "Eye_Right_Up", "Eyes_Look_Up_R" },
			new[] { "EyeLookUp", "eyeLookUp", "Eye_Up", "Eyes_Look_Up" });

		AssignFeature(e => e.LookDown,
			new[] { "EyeLookDownLeft", "eyeLookDownLeft", "Eye_Left_Down", "Eyes_Look_Down_L" },
			new[] { "EyeLookDownRight", "eyeLookDownRight", "Eye_Right_Down", "Eyes_Look_Down_R" },
			new[] { "EyeLookDown", "eyeLookDown", "Eye_Down", "Eyes_Look_Down" });

		AssignFeature(e => e.LookLeft,
			new[] { "EyeLookOutLeft", "eyeLookOutLeft", "Eye_Left_Left", "Eyes_Look_Left_L" },
			new[] { "EyeLookInRight", "eyeLookInRight", "Eye_Right_Left", "Eyes_Look_Left_R" },
			new[] { "EyeLookLeft", "eyeLookLeft", "Eye_Left", "Eyes_Look_Left" });

		AssignFeature(e => e.LookRight,
			new[] { "EyeLookInLeft", "eyeLookInLeft", "Eye_Left_Right", "Eyes_Look_Right_L" },
			new[] { "EyeLookOutRight", "eyeLookOutRight", "Eye_Right_Right", "Eyes_Look_Right_R" },
			new[] { "EyeLookRight", "eyeLookRight", "Eye_Right", "Eyes_Look_Right" });

		AssignFeature(e => e.OpenCloseTarget,
			new[] { "EyeClosedLeft", "eyeBlinkLeft", "Eye_Left_Blink", "Eyes_Closed_L", "vrc.blink_left" },
			new[] { "EyeClosedRight", "eyeBlinkRight", "Eye_Right_Blink", "Eyes_Closed_R", "vrc.blink_right" },
			new[] { "EyeClosed", "eyeBlink", "Eye_Blink", "Eyes_Closed", "vrc.blink", "blink" });

		AssignFeature(e => e.WidenTarget,
			new[] { "EyeWideLeft", "eyeWideLeft", "Eye_Left_Wide", "Upper_Lid_Raiser_L" },
			new[] { "EyeWideRight", "eyeWideRight", "Eye_Right_Wide", "Upper_Lid_Raiser_R" },
			new[] { "EyeWide", "eyeWide", "Eye_Wide", "Upper_Lid_Raiser" });

		AssignFeature(e => e.SqueezeTarget,
			new[] { "EyeSquintLeft", "eyeSquintLeft", "Eye_Left_squeeze", "Lid_Tightener_L" },
			new[] { "EyeSquintRight", "eyeSquintRight", "Eye_Right_squeeze", "Lid_Tightener_R" },
			new[] { "EyeSquint", "eyeSquint", "Eye_squeeze", "Lid_Tightener" });

		AssignFeature(e => e.PupilSizeTarget,
			new[] { "EyeDilationLeft", "eyeDilationLeft", "Eye_Left_Dilation" },
			new[] { "EyeDilationRight", "eyeDilationRight", "Eye_Right_Dilation" },
			new[] { "EyeDilation", "eyeDilation", "Eye_Dilation" });

		AssignFeature(e => e.InnerBrowRaiseTarget,
			new[] { "BrowUpLeft", "BrowInnerUpLeft", "browInnerUpLeft", "Inner_Brow_Raiser_L" },
			new[] { "BrowUpRight", "BrowInnerUpRight", "browInnerUpRight", "Inner_Brow_Raiser_R" },
			new[] { "BrowUp", "BrowInnerUp", "browInnerUp", "Inner_Brow_Raiser" });

		AssignFeature(e => e.OuterBrowRaiseTarget,
			new[] { "BrowOuterUpLeft", "browOuterUpLeft", "Outer_Brow_Raiser_L" },
			new[] { "BrowOuterUpRight", "browOuterUpRight", "Outer_Brow_Raiser_R" },
			new[] { "BrowOuterUp", "browOuterUp", "Outer_Brow_Raiser" });

		AssignFeature(e => e.InnerBrowLowerTarget,
			new[] { "BrowLowererLeft", "browDownLeft", "Brow_Lowerer_L" },
			new[] { "BrowLowererRight", "browDownRight", "Brow_Lowerer_R" },
			new[] { "BrowLowerer", "browDown", "Brow_Lowerer" });
	}


	public static void BuildInspectorUI(EyeLinearDriver component, UIBuilder ui) {
		if (!Config!.GetValue(Enabled)) { Msg($"UnifiedExpressionMapper is disabled, so not generating new ui for {component.Slot.Name}"); return; }

		ui.Style.MinHeight = 24f;
		ui.Text("Unified Expression Mapper (Mod)").Color.Value = RadiantUI_Constants.Hero.CYAN;
		ui.Style.MinHeight = 2f;
		ui.Image(RadiantUI_Constants.Hero.CYAN);
		ui.Style.MinHeight = 24f;

		Button autoAssignButton = ui.Button("Auto-assign blendshapes");

		autoAssignButton.LocalPressed += (btn, data) => {
			component.World.RunSynchronously(() => {
				AssignEyeShapes(component);
			});
		};

		// referencing https://github.com/TheJebForge/BoneReferenceHelper/blob/master/BoneReferenceHelper.cs#L97
		Slot infoHolder = ui.Empty("Info Holder");
		ui.NestInto(infoHolder);
		{
			ui.HorizontalLayout(4f);
			{
				ReferenceField<SkinnedMeshRenderer> slotField = infoHolder.AttachComponent<ReferenceField<SkinnedMeshRenderer>>();

				const string key = "Reference";
				SyncMemberEditorBuilder.Build(
					slotField.GetSyncMember(key),
					"Mesh",
					slotField.GetSyncMemberFieldInfo(key),
					ui);

				ui.Button("Assign blendshapes from mesh").LocalPressed += (button, _) => AssignEyeShapes(component, slotField.Reference.Target);
			}
			ui.NestOut();
		}
		ui.NestOut();
	}

}
