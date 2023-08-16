using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace ValheimVRM
{
    class PlayerSettings
    {
        private string name;
        public float modelScale, modelBrightness, modelOffsetY, springBoneStiffness, springBoneGravityPower;
        public bool fixCameraHeight, useMToonShader, enablePlayerFade;
        public Vector3 rightHandEquipPos, leftHandEquipPos, rightHandBackItemPos, leftHandBackItemPos;

        public PlayerSettings(string name)
        {
            this.name = name;
            modelScale = 1.1f;
            modelBrightness = .8f;
            fixCameraHeight = true;
            modelOffsetY = 0;
            useMToonShader = false;
            rightHandEquipPos = Vector3.zero;
            leftHandEquipPos = Vector3.zero;
            rightHandBackItemPos = Vector3.zero;
            leftHandBackItemPos = Vector3.zero;
            springBoneStiffness = 1f;
            springBoneGravityPower = 1f;
            enablePlayerFade = true;
        }

        public string Name() => this.name;
    }
	static class Settings
	{
		public static string ValheimVRMDir => Path.Combine(Environment.CurrentDirectory, "ValheimVRM");

		public static string PlayerSettingsPath(string playerName) => Path.Combine(ValheimVRMDir, $"settings_{playerName}.txt");
        private static List<PlayerSettings> playerSettingsList = new List<PlayerSettings>();

        public static PlayerSettings GetSettings(string playerName) => playerSettingsList.Find(x => x.Name() == playerName);

        public static bool AddSettingsFromFile(string playerName)
        {
            string path = PlayerSettingsPath(playerName);
            PlayerSettings ps = new PlayerSettings(playerName);
            if (File.Exists(path))
            {
                foreach (string str in File.ReadAllLines(path))
                {
                    bool parseSuccessful = false;
                    try
                    {
                        if (str.Length > 1 && str.Substring(0, 1) == "//") continue; // Skip contents
                        string[] args = str.Split('=');
                        if (args.Length != 2)
                        {
                            Debug.LogWarningFormat("[ValheimVRM] Error in the setting in the following line: {0}", str);
                            continue;
                        }
                        if (args[0].Trim() == "ModelScale")
                            parseSuccessful = float.TryParse(args[1].Trim(), out ps.modelScale);
                        else if (args[0].Trim() == "ModelBrightness")
                            parseSuccessful = float.TryParse(args[1].Trim(), out ps.modelBrightness);
                        else if (args[0].Trim() == "FixCameraHeight")
                            parseSuccessful = bool.TryParse(args[1].Trim(), out ps.fixCameraHeight);
                        else if (args[0].Trim() == "ModelOffsetY")
                            parseSuccessful = float.TryParse(args[1].Trim(), out ps.modelOffsetY);
                        else if (args[0].Trim() == "UseMToonShader")
                            parseSuccessful = bool.TryParse(args[1].Trim(), out ps.useMToonShader);
                        else if (args[0].Trim() == "RightHandEquipPos")
                            parseSuccessful = ReadVector3(args[1].Trim(), out ps.rightHandEquipPos);
                        else if (args[0].Trim() == "LeftHandEquipPos")
                            parseSuccessful = ReadVector3(args[1].Trim(), out ps.leftHandEquipPos);
                        else if (args[0].Trim() == "RightHandBackItemPos")
                            parseSuccessful = ReadVector3(args[1].Trim(), out ps.rightHandBackItemPos);
                        else if (args[0].Trim() == "LeftHandBackItemPos")
                            parseSuccessful = ReadVector3(args[1].Trim(), out ps.leftHandBackItemPos);
                        else if (args[0].Trim() == "SpringBoneStiffness")
                            parseSuccessful = float.TryParse(args[1].Trim(), out ps.springBoneStiffness);
                        else if (args[0].Trim() == "SpringBoneGravityPower")
                            parseSuccessful = float.TryParse(args[1].Trim(), out ps.springBoneGravityPower);
                        else if (args[0].Trim() == "EnablePlayerFade")
                            parseSuccessful = bool.TryParse(args[1].Trim(), out ps.enablePlayerFade);
                        else
                            Debug.LogWarningFormat("[ValheimVRM] Unrecognized option {0}", args[0]);

                        if (parseSuccessful)
                            Debug.LogFormat("[ValheimVRM] {0} = {1}", args[0], args[1]);
                        else
                            Debug.LogErrorFormat("[ValheimVRM] impossible to parse {0} at {1}", args[0], str);
                            
                    } catch (Exception ex)
                    {
                        Debug.LogException(ex);
                    }
                }
                playerSettingsList.Add(ps);
                return true;
            }
            return false;
        }

        public static bool ReadVector3(string str, out Vector3 vector)
        {
            Match match = new Regex("\\((?<x>[^,]*?),(?<y>[^,]*?),(?<z>[^,]*?)\\)").Match(str);
            vector = new Vector3();
            if (match.Success == false) return false;
            Vector3 res = new Vector3()
            {
                x = float.Parse(match.Groups["x"].Value),
                y = float.Parse(match.Groups["y"].Value),
                z = float.Parse(match.Groups["z"].Value)
            };
            vector = res;
            return true;
        }
    }
}
