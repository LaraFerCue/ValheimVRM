using HarmonyLib;

namespace ValheimVRM
{
    public static class AccessUtil
    {
        public static Tout GetField<Tin, Tout>(this Tin self, string fieldName)
        {
            return AccessTools.FieldRefAccess<Tin, Tout>(fieldName).Invoke(self);
        }
    }
}