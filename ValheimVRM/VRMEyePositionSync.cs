using UnityEngine;

namespace ValheimVRM
{
    public class VRMEyePositionSync : MonoBehaviour
    {
        private Transform vrmEye;
        private Transform orgEye;

        public void Setup(Transform vrmEye)
        {
            this.vrmEye = vrmEye;
            this.orgEye = GetComponent<Player>().m_eye;
        }

        void Update()
        {
            Vector3 pos = this.orgEye.position;
            pos.y = this.vrmEye.position.y;
            this.orgEye.position = pos;
        }
    }
}
