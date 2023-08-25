using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace ValheimVRM
{
    public class PlayerSettings
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

        public string ToString()
        {
            string str = String.Format(" Player {0} settings:\n" +
                ".modelScale={1}\n" +
                ".modelBrightness={2}\n" +
                ".fixCameraHeight={3}\n" +
                ".modelOffsetY={4}\n" +
                ".useMTShader={5}\n" +
                ".rightHandEquipPos={6}\n" +
                ".leftHandEquipPos={7}\n" +
                ".rightHandBackItemPos={8}\n" +
                ".leftHandBackItemPos={9}\n" +
                ".springBoneStiffness={10}\n" +
                ".springBoneGravityPower={11}\n" +
                ".enablePlayerFade={12}",
                name, modelScale, modelBrightness, fixCameraHeight, modelOffsetY, useMToonShader, rightHandEquipPos, leftHandEquipPos,
                rightHandBackItemPos, leftHandBackItemPos, springBoneStiffness, springBoneGravityPower, enablePlayerFade);
            return str;
        }
    }
    static class Settings
    {
        public static string ValheimVRMDir => Path.Combine(Environment.CurrentDirectory, "ValheimVRM");

        public static string PlayerSettingsPath(string playerName) => Path.Combine(ValheimVRMDir, $"settings_{playerName}.txt");
        private static List<PlayerSettings> playerSettingsList = new List<PlayerSettings>();

        public static PlayerSettings GetSettings(string playerName) => playerSettingsList.Find(x => x.Name() == playerName);

        public static PlayerSettings AddSettingsFromFile(string playerName)
        {
            string path = PlayerSettingsPath(playerName);
            PlayerSettings ps = new PlayerSettings(playerName);
            if (File.Exists(path))
            {
                foreach (string str in File.ReadAllLines(path))
                {
                    try
                    {
                        if (str.Length < 1) continue;
                        if (str.Length > 1 && str.Substring(0, 2) == "//") continue; // Skip contents
                        string[] args = str.Split('=');
                        if (args.Length != 2)
                        {
                            Debug.LogWarningFormat("[ValheimVRM] Error in the setting in the following line: {0}", str);
                            continue;
                        }
                        string value = args[1].Trim().ToLower();
                        if (args[0].Trim() == "ModelScale")
                            ps.modelScale = FloatSettingsParser.Parse(value);
                        else if (args[0].Trim() == "ModelBrightness")
                            ps.modelBrightness = FloatSettingsParser.Parse(value);
                        else if (args[0].Trim() == "FixCameraHeight")
                            ps.fixCameraHeight = BoolSettingsParser.Parse(value);
                        else if (args[0].Trim() == "ModelOffsetY")
                            ps.modelOffsetY = FloatSettingsParser.Parse(value);
                        else if (args[0].Trim() == "UseMToonShader")
                            ps.useMToonShader = BoolSettingsParser.Parse(value);
                        else if (args[0].Trim() == "RightHandEquipPos")
                            ps.rightHandEquipPos = Vector3SettingsParser.Parse(value);
                        else if (args[0].Trim() == "LeftHandEquipPos")
                            ps.leftHandEquipPos = Vector3SettingsParser.Parse(value);
                        else if (args[0].Trim() == "RightHandBackItemPos")
                            ps.rightHandBackItemPos = Vector3SettingsParser.Parse(value);
                        else if (args[0].Trim() == "LeftHandBackItemPos")
                            ps.leftHandBackItemPos = Vector3SettingsParser.Parse(value);
                        else if (args[0].Trim() == "SpringBoneStiffness")
                            ps.springBoneStiffness = FloatSettingsParser.Parse(value);
                        else if (args[0].Trim() == "SpringBoneGravityPower")
                            ps.springBoneGravityPower = FloatSettingsParser.Parse(value);
                        else if (args[0].Trim() == "EnablePlayerFade")
                            ps.enablePlayerFade = BoolSettingsParser.Parse(value);
                        else
                        {
                            Debug.LogWarningFormat("[ValheimVRM] Unrecognized option {0}", args[0]);
                            continue;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogErrorFormat("[ValheimVRM] impossible to parse {0}", str);
                        Debug.LogException(ex);
                    }
                }
                Debug.LogFormat("[ValheimVRM] {0}", ps.ToString());
            }
            else
                Debug.LogWarningFormat("[ValheimVRM] File {0} does not exist, using default settings", path);

            playerSettingsList.Add(ps);
            return ps;
        }
    }
}
