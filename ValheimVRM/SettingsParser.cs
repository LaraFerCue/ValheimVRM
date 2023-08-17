using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using UnityEngine;

namespace ValheimVRM
{
    class SettingsParserException: FormatException
    {
        public SettingsParserException(string message) : base(message) { }
    }


    static class FloatSettingsParser
    {
        public static float Parse(string str) {
            try
            {
                return float.Parse(str, System.Globalization.CultureInfo.InvariantCulture);
            } catch (Exception e)
            {
                UnityEngine.Debug.LogException(e);
                throw new SettingsParserException(e.Message);
            }
        }
    }

    static class BoolSettingsParser
    {
        public static bool Parse(string str)
        {
            try
            {
                return bool.Parse(str);
            } catch(Exception e)
            {
                UnityEngine.Debug.LogException(e);
                throw new SettingsParserException(e.Message);
            }
        }
    }

    static class Vector3SettingsParser {
        public static Vector3 Parse(string str) 
        {
            try
            {
                Match match = new Regex("\\((?<x>[^,]*?),(?<y>[^,]*?),(?<z>[^,]*?)\\)").Match(str);
                if (match.Success == false) throw new SettingsParserException("Setting is not a Vector3");
                return new Vector3()
                {
                    x = float.Parse(match.Groups["x"].Value),
                    y = float.Parse(match.Groups["y"].Value),
                    z = float.Parse(match.Groups["z"].Value)
                };
            } catch (Exception e)
            {
                UnityEngine.Debug.LogException(e);
                throw new SettingsParserException(e.Message);
            }
        } 
    }
}
