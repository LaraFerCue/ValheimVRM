using HarmonyLib;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UniGLTF;
using UnityEngine;
using VRM;

namespace ValheimVRM
{
	[HarmonyPatch(typeof(Shader))]
	[HarmonyPatch(nameof(Shader.Find))]
	static class ShaderPatch
	{
		static bool Prefix(ref Shader __result, string name)
		{
			if (VRMShaders.Shaders.TryGetValue(name, out var shader))
			{
				__result = shader;
				return false;
			}

			return true;
		}
	}

	public static class VRMShaders
	{
		public static Dictionary<string, Shader> Shaders { get; } = new Dictionary<string, Shader>();

		public static void Initialize()
		{
			var bundlePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), @"ValheimVRM.shaders");
			if (File.Exists(bundlePath))
			{
                AssetBundle assetBundle = AssetBundle.LoadFromFile(bundlePath);
                Shader[] assets = assetBundle.LoadAllAssets<Shader>();
				foreach (Shader asset in assets)
				{
					UnityEngine.Debug.Log("[ValheimVRM] Add Shader: " + asset.name);
					Shaders.Add(asset.name, asset);
				}
			}
		}
	}

	public static class VRMModels
	{
		public static Dictionary<string, byte[]> VrmBufDic = new Dictionary<string, byte[]>();
		public static Dictionary<Player, GameObject> PlayerToVrmDic = new Dictionary<Player, GameObject>();
		public static Dictionary<Player, string> PlayerToNameDic = new Dictionary<Player, string>();
	}

	[HarmonyPatch(typeof(VisEquipment), "UpdateLodgroup")]
	static class Patch_VisEquipment_UpdateLodgroup
	{
		[HarmonyPostfix]
		static void Postfix(VisEquipment __instance)
		{
			if (!__instance.m_isPlayer) return;
			Player player = __instance.GetComponent<Player>();
			if (player == null || !VRMModels.PlayerToVrmDic.ContainsKey(player)) return;

			GameObject hair = __instance.GetField<VisEquipment, GameObject>("m_hairItemInstance");
			if (hair != null) SetVisible(hair, false);

			GameObject beard = __instance.GetField<VisEquipment, GameObject>("m_beardItemInstance");
			if (beard != null) SetVisible(beard, false);

			List<GameObject> chestList = __instance.GetField<VisEquipment, List<GameObject>>("m_chestItemInstances");
			if (chestList != null) foreach (var chest in chestList) SetVisible(chest, false);

            List<GameObject> legList = __instance.GetField<VisEquipment, List<GameObject>>("m_legItemInstances");
			if (legList != null) foreach (var leg in legList) SetVisible(leg, false);

            List<GameObject> shoulderList = __instance.GetField<VisEquipment, List<GameObject>>("m_shoulderItemInstances");
			if (shoulderList != null) foreach (var shoulder in shoulderList) SetVisible(shoulder, false);

            List<GameObject> utilityList = __instance.GetField<VisEquipment, List<GameObject>>("m_utilityItemInstances");
			if (utilityList != null) foreach (var utility in utilityList) SetVisible(utility, false);

            GameObject helmet = __instance.GetField<VisEquipment, GameObject>("m_helmetItemInstance");
			if (helmet != null) SetVisible(helmet, false);

			// 武器位置合わせ
			string name = VRMModels.PlayerToNameDic[player];

			GameObject leftItem = __instance.GetField<VisEquipment, GameObject>("m_leftItemInstance");
			if (leftItem != null) leftItem.transform.localPosition = Settings.ReadVector3(name, "LeftHandEuqipPos", Vector3.zero);

            GameObject rightItem = __instance.GetField<VisEquipment, GameObject>("m_rightItemInstance");
			if (rightItem != null) rightItem.transform.localPosition = Settings.ReadVector3(name, "RightHandEuqipPos", Vector3.zero);

            // divided  by 100 to keep the settings file positions in the same number range. (position offset appears to be on the world, not local)
            GameObject rightBackItem = __instance.GetField<VisEquipment, GameObject>("m_rightBackItemInstance");
			if (rightBackItem != null) rightBackItem.transform.localPosition = Settings.ReadVector3(name, "RightHandBackItemPos", Vector3.zero) / 100.0f;

            GameObject leftBackItem = __instance.GetField<VisEquipment, GameObject>("m_leftBackItemInstance");
			if (leftBackItem != null) leftBackItem.transform.localPosition = Settings.ReadVector3(name, "LeftHandBackItemPos", Vector3.zero) / 100.0f;
		}

		private static void SetVisible(GameObject obj, bool flag)
		{
			foreach (MeshRenderer mr in obj.GetComponentsInChildren<MeshRenderer>()) mr.enabled = flag;
			foreach (SkinnedMeshRenderer smr in obj.GetComponentsInChildren<SkinnedMeshRenderer>()) smr.enabled = flag;
		}
	}

	[HarmonyPatch(typeof(Humanoid), "OnRagdollCreated")]
	static class Patch_Humanoid_OnRagdollCreated
	{
		[HarmonyPostfix]
		static void Postfix(Humanoid __instance, Ragdoll ragdoll)
		{
			if (!__instance.IsPlayer()) return;

			foreach (SkinnedMeshRenderer smr in ragdoll.GetComponentsInChildren<SkinnedMeshRenderer>())
			{
				smr.forceRenderingOff = true;
				smr.updateWhenOffscreen = true;
			}
			

			Animator ragAnim = ragdoll.gameObject.AddComponent<Animator>();
			ragAnim.keepAnimatorStateOnDisable = true;
			ragAnim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

			Animator orgAnim = ((Player)__instance).GetField<Player, Animator>("m_animator");
			ragAnim.avatar = orgAnim.avatar;

			if (VRMModels.PlayerToVrmDic.TryGetValue((Player)__instance, out GameObject vrm))
			{
				vrm.transform.SetParent(ragdoll.transform);
				vrm.GetComponent<VRMAnimationSync>().Setup(ragAnim, true);
			}
		}
	}

	[HarmonyPatch(typeof(Character), "SetVisible")]
	static class Patch_Character_SetVisible
	{
		[HarmonyPostfix]
		static void Postfix(Character __instance, bool visible)
		{
			if (!__instance.IsPlayer()) return;

			if (VRMModels.PlayerToVrmDic.TryGetValue((Player)__instance, out GameObject vrm))
			{
				LODGroup lodGroup = vrm.GetComponent<LODGroup>();
				if (visible)
				{
					lodGroup.localReferencePoint = __instance.GetField<Character, Vector3>("m_originalLocalRef");
				}
				else
				{
					lodGroup.localReferencePoint = new Vector3(999999f, 999999f, 999999f);
				}
			}
		}
	}

	[HarmonyPatch(typeof(Player), "OnDeath")]
	static class Patch_Player_OnDeath
	{
		[HarmonyPostfix]
		static void Postfix(Player __instance)
		{
			string name = null;
			if (VRMModels.PlayerToNameDic.ContainsKey(__instance)) name = VRMModels.PlayerToNameDic[__instance];
			if (name != null && Settings.ReadBool(name, "FixCameraHeight", true))
			{
				GameObject.Destroy(__instance.GetComponent<VRMEyePositionSync>());
			}
		}
	}

	[HarmonyPatch(typeof(Character), "GetHeadPoint")]
	static class Patch_Character_GetHeadPoint
	{
		[HarmonyPostfix]
		static bool Prefix(Character __instance, ref Vector3 __result)
		{
			Player player = __instance as Player;
			if (player == null) return true;

			if (VRMModels.PlayerToVrmDic.TryGetValue(player, out GameObject vrm))
			{
				Animator animator = vrm.GetComponentInChildren<Animator>();
				if (animator == null) return true;

				Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
				if (head == null) return true;

				__result = head.position;
				return false;
			}
			
			return true;
		}
	}

	[HarmonyPatch(typeof(Player), "Awake")]
	static class Patch_Player_Awake
	{
		private static Dictionary<string, GameObject> vrmDic = new Dictionary<string, GameObject>();

		[HarmonyPostfix]
		static void Postfix(Player __instance)
		{
			string playerName = null;
			if (Game.instance != null)
			{
				playerName = __instance.GetPlayerName();
				if (playerName == "" || playerName == "...") playerName = Game.instance.GetPlayerProfile().GetName();
			}
			else
			{
				int index = FejdStartup.instance.GetField<FejdStartup, int>("m_profileIndex");
				List<PlayerProfile> profiles = FejdStartup.instance.GetField<FejdStartup, List<PlayerProfile>>("m_profiles");
				if (index >= 0 && index < profiles.Count) playerName = profiles[index].GetName();
			}

			if (!string.IsNullOrEmpty(playerName) && !vrmDic.ContainsKey(playerName))
			{
				string path = Path.Combine(Environment.CurrentDirectory, "ValheimVRM", $"{playerName}.vrm");

				ref ZNetView m_nview = ref AccessTools.FieldRefAccess<Player, ZNetView>("m_nview").Invoke(__instance);
				if (!File.Exists(path))
				{
					Debug.LogErrorFormat("[ValheimVRM] VRM model for player {0} not found.", playerName);
					Debug.LogErrorFormat("[ValheimVRM] VRM file path: {0}", path);
				}
				else
				{
					if (!Settings.ContainsSettings(playerName))
					{
						if (!Settings.AddSettingsFromFile(playerName))
						{
							Debug.LogWarningFormat("[ValheimVRM] Settings file for {0} not found. Please check that the following file exists {1}",
								playerName, Settings.PlayerSettingsPath(playerName));
						}
					}

					float scale = Settings.ReadFloat(playerName, "ModelScale", 1.1f);
					GameObject orgVrm =  ImportVRM(path, scale);
					if (orgVrm != null)
					{
						GameObject.DontDestroyOnLoad(orgVrm);
						vrmDic[playerName] = orgVrm;
						VRMModels.VrmBufDic[playerName] = File.ReadAllBytes(path);

						//[Error: Unity Log] _Cutoff: Range
						//[Error: Unity Log] _MainTex: Texture
						//[Error: Unity Log] _SkinBumpMap: Texture
						//[Error: Unity Log] _SkinColor: Color
						//[Error: Unity Log] _ChestTex: Texture
						//[Error: Unity Log] _ChestBumpMap: Texture
						//[Error: Unity Log] _ChestMetal: Texture
						//[Error: Unity Log] _LegsTex: Texture
						//[Error: Unity Log] _LegsBumpMap: Texture
						//[Error: Unity Log] _LegsMetal: Texture
						//[Error: Unity Log] _BumpScale: Float
						//[Error: Unity Log] _Glossiness: Range
						//[Error: Unity Log] _MetalGlossiness: Range

						// シェーダ差し替え
						float brightness = Settings.ReadFloat(playerName, "ModelBrightness", 0.8f);
						List<Material> materials = new List<Material>();
						foreach (SkinnedMeshRenderer smr in orgVrm.GetComponentsInChildren<SkinnedMeshRenderer>())
						{
							foreach (Material mat in smr.materials)
							{
								if (!materials.Contains(mat)) materials.Add(mat);
							}
						}
						foreach (MeshRenderer mr in orgVrm.GetComponentsInChildren<MeshRenderer>())
						{
							foreach (Material mat in mr.materials)
							{
								if (!materials.Contains(mat)) materials.Add(mat);
							}
						}

						if (Settings.ReadBool(playerName, "UseMToonShader", false))
						{
							foreach (Material mat in materials)
							{
								if (mat.HasProperty("_Color"))
								{
									Color color = mat.GetColor("_Color");
									color.r *= brightness;
									color.g *= brightness;
									color.b *= brightness;
									mat.SetColor("_Color", color);
								}
							}
						}
						else
						{
							Shader shader = Shader.Find("Custom/Player");
							foreach (Material mat in materials)
							{
								if (mat.shader == shader) continue;

								Color color = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;

								Texture2D mainTex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") as Texture2D : null;
								Texture2D tex = mainTex;
								if (mainTex != null)
								{
									tex = new Texture2D(mainTex.width, mainTex.height);
									Color[] colors = mainTex.GetPixels();
									for (int i = 0; i < colors.Length; i++)
									{
										Color col = colors[i] * color;
										float h, s, v;
										Color.RGBToHSV(col, out h, out s, out v);
										v *= brightness;
										colors[i] = Color.HSVToRGB(h, s, v);
										colors[i].a = col.a;
									}
									tex.SetPixels(colors);
									tex.Apply();
								}

								Texture bumpMap = mat.HasProperty("_BumpMap") ? mat.GetTexture("_BumpMap") : null;
								mat.shader = shader;

								mat.SetTexture("_MainTex", tex);
								mat.SetTexture("_SkinBumpMap", bumpMap);
								mat.SetColor("_SkinColor", color);
								mat.SetTexture("_ChestTex", tex);
								mat.SetTexture("_ChestBumpMap", bumpMap);
								mat.SetTexture("_LegsTex", tex);
								mat.SetTexture("_LegsBumpMap", bumpMap);
								mat.SetFloat("_Glossiness", 0.2f);
								mat.SetFloat("_MetalGlossiness", 0.0f);
								
							}
						}

                        LODGroup lodGroup = orgVrm.AddComponent<LODGroup>();
						LOD lod = new LOD(0.1f, orgVrm.GetComponentsInChildren<SkinnedMeshRenderer>());
						if (Settings.ReadBool(playerName, "EnablePlayerFade", true)) lodGroup.SetLODs(new LOD[] { lod });
						lodGroup.RecalculateBounds();

						LODGroup orgLodGroup = __instance.GetComponentInChildren<LODGroup>();
						lodGroup.fadeMode = orgLodGroup.fadeMode;
						lodGroup.animateCrossFading = orgLodGroup.animateCrossFading;

						orgVrm.SetActive(false);
					}
				}
			}

			if (!string.IsNullOrEmpty(playerName) && vrmDic.ContainsKey(playerName))
			{
				GameObject vrmModel = GameObject.Instantiate(vrmDic[playerName]);
				VRMModels.PlayerToVrmDic[__instance] = vrmModel;
				VRMModels.PlayerToNameDic[__instance] = playerName;
				vrmModel.SetActive(true);
				vrmModel.transform.SetParent(__instance.GetComponentInChildren<Animator>().transform.parent, false);

				foreach (var smr in __instance.GetVisual().GetComponentsInChildren<SkinnedMeshRenderer>())
				{
					smr.forceRenderingOff = true;
					smr.updateWhenOffscreen = true;
				}

				Animator orgAnim = AccessTools.FieldRefAccess<Player, Animator>(__instance, "m_animator");
				orgAnim.keepAnimatorStateOnDisable = true;
				orgAnim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

				vrmModel.transform.localPosition = orgAnim.transform.localPosition;

				// アニメーション同期
				float offsetY = Settings.ReadFloat(playerName, "ModelOffsetY");
				if (vrmModel.GetComponent<VRMAnimationSync>() == null) vrmModel.AddComponent<VRMAnimationSync>().Setup(orgAnim, false, offsetY);
				else vrmModel.GetComponent<VRMAnimationSync>().Setup(orgAnim, false, offsetY);

				// カメラ位置調整
				if (Settings.ReadBool(playerName, "FixCameraHeight", true))
				{
					Transform vrmEye = vrmModel.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.LeftEye);
					if (vrmEye == null) vrmEye = vrmModel.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Head);
					if (vrmEye == null) vrmEye = vrmModel.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Neck);
					if (vrmEye != null)
					{
						if (__instance.gameObject.GetComponent<VRMEyePositionSync>() == null) __instance.gameObject.AddComponent<VRMEyePositionSync>().Setup(vrmEye);
						else __instance.gameObject.GetComponent<VRMEyePositionSync>().Setup(vrmEye);
					}
				}

				// MToonの場合環境光の影響をカラーに反映する
				if (Settings.ReadBool(playerName, "UseMToonShader", false))
				{
					if (vrmModel.GetComponent<MToonColorSync>() == null) vrmModel.AddComponent<MToonColorSync>().Setup(vrmModel);
					else vrmModel.GetComponent<MToonColorSync>().Setup(vrmModel);
				}

				// SpringBone設定
				float stiffness = Settings.ReadFloat(playerName, "SpringBoneStiffness", 1.0f);
				float gravity = Settings.ReadFloat(playerName, "SpringBoneGravityPower", 1.0f);
				foreach (var springBone in vrmModel.GetComponentsInChildren<VRM.VRMSpringBone>())
				{
					springBone.m_stiffnessForce *= stiffness;
					springBone.m_gravityPower *= gravity;
					springBone.m_updateType = VRMSpringBone.SpringBoneUpdateType.FixedUpdate;
					springBone.m_center = null;
				}
			}
		}

		private static GameObject ImportVRM(string path, float scale)
		{
			try
			{
                GltfData data = new GlbFileParser(path).Parse();
				VRMData vrm = new VRMData(data);
                VRMImporterContext context = new VRMImporterContext(vrm);
                RuntimeGltfInstance loaded = default(RuntimeGltfInstance);
				loaded = context.Load();
				loaded.ShowMeshes();
				loaded.Root.transform.localScale *= scale;

				Debug.Log("[ValheimVRM] Module successfully loaded");
				Debug.LogFormat("[ValheimVRM] VRM file path: {0}", path);

				return loaded.Root;
			}
			catch (Exception ex)
			{
				Debug.LogError(ex);
			}

			return null;
		}
	}
}