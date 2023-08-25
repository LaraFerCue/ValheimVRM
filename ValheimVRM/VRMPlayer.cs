using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UniGLTF;
using UnityEngine;
using VRM;

namespace ValheimVRM
{
    static public class VRMMaterials
    {
        public static List<Material> GetMaterials(GameObject obj)
        {
            List<Material> ret = new List<Material>();
            foreach (SkinnedMeshRenderer smr in obj.GetComponentsInChildren<SkinnedMeshRenderer>())
                foreach (Material mat in smr.materials)
                    ret.Add(mat);
            foreach (MeshRenderer mr in obj.GetComponentsInChildren<MeshRenderer>())
                foreach (Material mat in mr.materials)
                    ret.Add(mat);
            return ret;
        }

        private static void SetMToonMaterialBrightness(float brightness, GameObject obj)
        {
            foreach (Material mat in GetMaterials(obj))
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

        private static void SetCustomPlayerShaderBrightness(float brightness, GameObject obj)
        {
            Shader shader = Shader.Find("Custom/Player");
            if (shader == null)
                return;

            foreach (Material mat in GetMaterials(obj))
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

        public static void SetMaterialBrightness(bool useMToonShader, float brightness, GameObject obj)
        {
            if (useMToonShader)
                SetMToonMaterialBrightness(brightness, obj);
            else
                SetCustomPlayerShaderBrightness(brightness, obj);
        }
    }


    public class VRMPlayer
    {
        class VRMPlayerException: Exception
        {
            public VRMPlayerException(string message) : base(message) { }
        }

        private GameObject vrm;
        private PlayerSettings settings;
        private byte[] hash;
        public Player player;
        private string name;

        public static GameObject ImportVRM(string path, float scale)
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

        public string GetName() => name;
        public GameObject GetVrm() => vrm;
        public LODGroup GetLODGroup() => vrm.GetComponent<LODGroup>();
        public void SetActive(bool active) => vrm.SetActive(active);
        public PlayerSettings GetSettings() => settings;

        public VRMPlayer(string name)
        {
            this.name = name;
            string vrmPath = Path.Combine(Settings.ValheimVRMDir, $"{name}.vrm");
            using (FileStream stream = File.OpenRead(vrmPath))
            {
                SHA256 sha256 = SHA256Managed.Create();
                hash = sha256.ComputeHash(stream);
            }
            settings = Settings.AddSettingsFromFile(name);
            vrm = ImportVRM(vrmPath, settings.modelScale);
            if (vrm == null)
                return;
            GameObject.DontDestroyOnLoad(vrm);
            VRMMaterials.SetMaterialBrightness(settings.useMToonShader, settings.modelBrightness, vrm);

            LODGroup lodGroup = vrm.AddComponent<LODGroup>();
            LOD lod = new LOD(0.1f, vrm.GetComponentsInChildren<SkinnedMeshRenderer>());
            if (settings.enablePlayerFade) lodGroup.SetLODs(new LOD[] { lod });
            lodGroup.RecalculateBounds();
        }
    }
}
