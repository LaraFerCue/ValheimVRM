using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using UniGLTF;
using UniGLTF.MeshUtility;
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
        public static List<VRMPlayer> players = new List<VRMPlayer>();
    }

    [HarmonyPatch(typeof(VisEquipment), "UpdateLodgroup")]
    static class Patch_VisEquipment_UpdateLodgroup
    {
        static void DeactivateVisibilityEquipment(VisEquipment __instance)
        {
            if (!__instance.m_isPlayer) return;

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
        }

        static Material GetBodyMaterial(GameObject __instance)
        {
            foreach (SkinnedMeshRenderer smr in __instance.gameObject.GetComponentsInChildren<SkinnedMeshRenderer>())
                if (smr.name == "body")
                    return smr.material;
            return null;
        }

        [HarmonyPostfix]
        static void Postfix(VisEquipment __instance)
        {
            Player player = __instance.GetComponent<Player>();
            VRMPlayer vrmPlayer = VRMModels.players.Find(x => x.player == player);
            if (player == null || vrmPlayer == null)
            {
                Debug.LogWarningFormat("[ValheimVRM] Player or vrmPlayer not found! player = {0} vrmPlayer = {1}", player, vrmPlayer);
                return;
            }

            /*
             * Valheim uses skinned meshes for the clothes and changes the materials depending on the kind of
             * armor it is being used. In order to have something similar, we would have to do something like
             * that.
             */
            DeactivateVisibilityEquipment(__instance);
            Material bodyMat = GetBodyMaterial(__instance.gameObject);
            if (bodyMat != null)
            {
                Debug.LogFormat("BodyMat = {0} Texture: .skin = {1} .chest = {2} .legs = {3}", bodyMat.name, 
                    bodyMat.GetTexture(Shader.PropertyToID("_MainTex")), bodyMat.GetTexture(Shader.PropertyToID("_ChestTex")),
                    bodyMat.GetTexture(Shader.PropertyToID("_LegsTex")));
            }
            /**
             * Rags pants -> bodyMat._LegsTex = StartingRagsLegs_d
             * Rags Tunic -> bodyMat._ChestTex = StartingRagsChest_d
             * Leather Pants -> bodyMat._LegsTex = LeatherArmourPants_d
             * Leather Tunic -> shorts.material = LeatherChest
             * Leather Helmet -> bronzehelmet.Material = helmet_leather_mat
             * Deer Hide Cape -> cape2.Material = CapeDeerHide
             * Troll Leather Pants -> bodyMat._LegsTex = TrollLeatherArmorLegs_d
             * Troll Leather Tunic -> shorts.material = TrollLeatherChest
             * Troll Leather Hood -> hood.material = helmet_trollleather
             * Troll Leather Cape -> cape2.material = CapeTrollHide
             * Bronze Plate Leggings -> bodyMat._LegsTex = BronzeArmorLegs_d
             *                       -> BronzeArmor.001.material = BronzeArmorMesh_mat
             * Bronze Plate Cuirass -> bodyMat._ChestTex = BronzeArmorChest_d
             *                      -> BronzeArmor.material = BronzeArmorMesh_mat
             * Bronze Helmet -> bronzehelmet.material = helmet_bronze_mat
             * Iron Greaves -> bodyMat._LegsTex = IronArmorLegs_d
             *              -> SilverWolfArmorLegs.001.material = IronArmorLegs_mat
             * Iron Scale Mail -> bodyMat._ChestTex = IronArmorChestPlayer_d
             *                 -> IronArmor.material = IronArmorChest_mat
             * Iron Helmet -> bronzehelmet.material = helmet_iron_mat
             * Caparace Breastplate -> bodyMat._ChestTex = carapacearmorChest_d
             *                      -> CarapaceArmor.material = carapacearmor_mat
             * Caparace Greaves -> bodyMat._LegsTex = carapaceArmorLegs_d
             *                  -> CarapaceLegs.material = carapacearmor_mat
             * Fenris Leggings -> bodyMat._LegsTex = FenringArmorLegs_d
             *                 -> FenringBoots.material = FenringArmor_mat
             * Fenris Coat -> bodyMat._ChestTex = FenringArmorChest_d
             *             -> FenringPants.material = FenringArmor_mat
             * Eitr-weave Trousers -> bodyMat._LegsTex = MageArmorLegs_d
             *                     -> MageArmorShoes.material = MageArmor_mat
             * Eitr-weave Robe -> bodyMat._ChestTex = MageArmorChestRed_d
             *                 -> MageArmorBody.001.material = MageArmor_mat
             * Root Harnesk -> bodyMat._ChestTex = AbominationArmorChest_d
             *              -> RootArmorSkirt.material = AbominationArmor
             * Root leggings -> bodyMat._LegsTex = AbominationArmorLegs_d
             *               -> RootArmorPants.005 = AbominationArmor
             * Drake Helmet -> default.material = DragonVisor_mat
             * Wolf Armor Chest -> bodyMat._ChestTex = SilverArmour_Skin_d
             *                  -> SilverWolfArmor = SilverArmourChest_mat
             * Wolf Armor Legs -> bodyMat._LegsTex = SilverArmour_Skin_Legs_d
             *                 -> Cube.008.material = SilverArmourChest_mat
             * Wolf Fur Cape -> WolfCape_cloth.material = WolfCape
             *               -> WolfCape.material = WolfCapeChain
             * Padded Helmet -> ChainLinkVisor.material = Padded_mat
             * Padded Greaves -> Padded_Grieves.material = Padded_mat
             *                -> bodyMat._LegsTex = Padded_Grieves_d
             * Padded Cuirass -> Padded_Cuirrass.material = Padded_mat
             *                -> bodyMat._ChestTex = PaddedArmorChest_d
             * Feather Cape -> MageCape.material = feathercape_mat
             * Cape TEST -> cape1.material = CapeLinen
             * Linen Cape -> cape1.material = CapeLinen
             * Lox Cape -> LoxCape.material = LoxCape_Mat
             * Cape Of Odin -> ?
             */

            GameObject leftItem = __instance.GetField<VisEquipment, GameObject>("m_leftItemInstance");
            if (leftItem != null) leftItem.transform.localPosition = vrmPlayer.GetSettings().leftHandEquipPos;

            GameObject rightItem = __instance.GetField<VisEquipment, GameObject>("m_rightItemInstance");
            if (rightItem != null) rightItem.transform.localPosition = vrmPlayer.GetSettings().rightHandEquipPos;

            // divided  by 100 to keep the settings file positions in the same number range. (position offset appears to be on the world, not local)
            GameObject rightBackItem = __instance.GetField<VisEquipment, GameObject>("m_rightBackItemInstance");
            if (rightBackItem != null) rightBackItem.transform.localPosition = vrmPlayer.GetSettings().rightHandBackItemPos / 100.0f;

            GameObject leftBackItem = __instance.GetField<VisEquipment, GameObject>("m_leftBackItemInstance");
            if (leftBackItem != null) leftBackItem.transform.localPosition = vrmPlayer.GetSettings().leftHandBackItemPos / 100.0f;
        }

        private static void SetVisible(GameObject obj, bool flag)
        {
            foreach (MeshRenderer mr in obj.GetComponentsInChildren<MeshRenderer>())
            {
                mr.enabled = flag;
                foreach (Material m in mr.materials)
                    Debug.LogFormat("{0} material: {1}", mr, m);
            }
            foreach (SkinnedMeshRenderer smr in obj.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                smr.enabled = flag;
                foreach(Material m in smr.materials)
                    Debug.LogFormat("{0} material: {1}", smr, m);
            }
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

            VRMPlayer vrmPlayer = VRMModels.players.Find(x => x.player ==  __instance);
            if (vrmPlayer != null)
            {
                vrmPlayer.GetVrm().transform.SetParent(ragdoll.transform);
                vrmPlayer.GetVrm().GetComponent<VRMAnimationSync>().Setup(ragAnim, true);
            }
            else
                Debug.LogErrorFormat("[ValheimVRM] No VRM Player found {0}", vrmPlayer);
        }
    }

    [HarmonyPatch(typeof(Character), "SetVisible")]
    static class Patch_Character_SetVisible
    {
        [HarmonyPostfix]
        static void Postfix(Character __instance, bool visible)
        {
            if (!__instance.IsPlayer()) return;

            VRMPlayer vrmPlayer = VRMModels.players.Find(x=> x.player == __instance);
            if (vrmPlayer != null)
            {
                LODGroup lodGroup = vrmPlayer.GetVrm().GetComponent<LODGroup>();
                if (visible)
                {
                    lodGroup.localReferencePoint = __instance.GetField<Character, Vector3>("m_originalLocalRef");
                }
                else
                {
                    lodGroup.localReferencePoint = new Vector3(999999f, 999999f, 999999f);
                }
            }
            else
                Debug.LogErrorFormat("[ValheimVRM] No VRM Player found {0}", vrmPlayer);
        }
    }

    [HarmonyPatch(typeof(Player), "OnDeath")]
    static class Patch_Player_OnDeath
    {
        [HarmonyPostfix]
        static void Postfix(Player __instance)
        {
            string name = null;
            VRMPlayer vrmModel = VRMModels.players.Find(x => x.player == __instance);
            if (vrmModel != null)
                name = vrmModel.GetName();
            else
                Debug.LogErrorFormat("[ValheimVRM] No VRM Player found {0}", vrmModel);
            if (name != null && Settings.GetSettings(name).fixCameraHeight)
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

            VRMPlayer vrmPlayer = VRMModels.players.Find(x => x.player == player);
            if (vrmPlayer != null)
            {
                Animator animator = vrmPlayer.GetVrm().GetComponentInChildren<Animator>();
                if (animator == null) return true;

                Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
                if (head == null) return true;

                __result = head.position;
                return false;
            }
            else
                Debug.LogErrorFormat("[ValheimVRM] No VRM Player found {0}", vrmPlayer);

            return true;
        }
    }

    [HarmonyPatch(typeof(Player), "Awake")]
    static class Patch_Player_Awake
    {

        static string GetPlayerName(Player __instance)
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
            return playerName;
        }

        static VRMPlayer LoadCustomPlayer(Player __instance, string playerName)
        {
            string path = Path.Combine(Environment.CurrentDirectory, "ValheimVRM", $"{playerName}.vrm");
            VRMPlayer vrmPlayer = new VRMPlayer(playerName);

            vrmPlayer.player = __instance;
            VRMModels.players.Add(vrmPlayer);

            ref ZNetView m_nview = ref AccessTools.FieldRefAccess<Player, ZNetView>("m_nview").Invoke(__instance);
            
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

            LODGroup lodGroup = vrmPlayer.GetLODGroup();
            LODGroup orgLodGroup = __instance.GetComponentInChildren<LODGroup>();
            lodGroup.fadeMode = orgLodGroup.fadeMode;
            lodGroup.animateCrossFading = orgLodGroup.animateCrossFading;

            vrmPlayer.SetActive(false);
            return vrmPlayer;
        }


        [HarmonyPostfix]
        static void Postfix(Player __instance)
        {
            string playerName = GetPlayerName(__instance);

            if (string.IsNullOrEmpty(playerName))
            {
                Debug.LogErrorFormat("[ValheimVRM] Could not fetch the player's name for {0}", __instance);
            }

            VRMPlayer player = VRMModels.players.Find(x =>  x.GetName() == playerName);
            if (player ==null)
                player = LoadCustomPlayer(__instance, playerName);

            InstanciatePlayer(player);
        }

        private static void InstanciatePlayer(VRMPlayer player)
        {
            GameObject vrmModel = GameObject.Instantiate(player.GetVrm());
            vrmModel.SetActive(true);
            vrmModel.transform.SetParent(player.player.GetComponentInChildren<Animator>().transform.parent, false);

            foreach (var smr in player.player.GetVisual().GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                smr.forceRenderingOff = true;
                smr.updateWhenOffscreen = true;
            }

            Animator orgAnim = AccessTools.FieldRefAccess<Player, Animator>(player.player, "m_animator");
            orgAnim.keepAnimatorStateOnDisable = true;
            orgAnim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            vrmModel.transform.localPosition = orgAnim.transform.localPosition;

            // アニメーション同期
            PlayerSettings settings = player.GetSettings();
            float offsetY = settings.modelOffsetY;
            if (vrmModel.GetComponent<VRMAnimationSync>() == null) vrmModel.AddComponent<VRMAnimationSync>().Setup(orgAnim, false, offsetY);
            else vrmModel.GetComponent<VRMAnimationSync>().Setup(orgAnim, false, offsetY);

            // カメラ位置調整
            if (settings.fixCameraHeight)
            {
                Transform vrmEye = vrmModel.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.LeftEye);
                if (vrmEye == null) vrmEye = vrmModel.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Head);
                if (vrmEye == null) vrmEye = vrmModel.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Neck);
                if (vrmEye != null)
                {
                    if (player.player.gameObject.GetComponent<VRMEyePositionSync>() == null) player.player.gameObject.AddComponent<VRMEyePositionSync>().Setup(vrmEye);
                    else player.player.gameObject.GetComponent<VRMEyePositionSync>().Setup(vrmEye);
                }
            }

            // MToonの場合環境光の影響をカラーに反映する
            if (settings.useMToonShader)
            {
                if (vrmModel.GetComponent<MToonColorSync>() == null) vrmModel.AddComponent<MToonColorSync>().Setup(vrmModel);
                else vrmModel.GetComponent<MToonColorSync>().Setup(vrmModel);
            }

            // SpringBone設定
            float stiffness = settings.springBoneStiffness;
            float gravity = settings.springBoneGravityPower;
            foreach (var springBone in vrmModel.GetComponentsInChildren<VRM.VRMSpringBone>())
            {
                springBone.m_stiffnessForce *= stiffness;
                springBone.m_gravityPower *= gravity;
                springBone.m_updateType = VRMSpringBone.SpringBoneUpdateType.FixedUpdate;
                springBone.m_center = null;
            }
        }

    }
}