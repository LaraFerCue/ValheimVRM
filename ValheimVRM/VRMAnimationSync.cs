using System.Collections.Generic;
using UnityEngine;

namespace ValheimVRM
{
    //[DefaultExecutionOrder(int.MaxValue)]
    public class VRMAnimationSync : MonoBehaviour
    {
        private Animator orgAnim, vrmAnim;
        private HumanPoseHandler orgPose, vrmPose;
        private HumanPose hp = new HumanPose();
        private bool ragdoll;
        private float offset;

        public void Setup(Animator orgAnim, bool isRagdoll = false, float offset = 0.0f)
        {
            this.ragdoll = isRagdoll;
            this.offset = offset;
            this.orgAnim = orgAnim;
            this.vrmAnim = GetComponent<Animator>();
            this.vrmAnim.applyRootMotion = true;
            this.vrmAnim.updateMode = orgAnim.updateMode;
            this.vrmAnim.feetPivotActive = orgAnim.feetPivotActive;
            this.vrmAnim.layersAffectMassCenter = orgAnim.layersAffectMassCenter;
            this.vrmAnim.stabilizeFeet = orgAnim.stabilizeFeet;

            PoseHandlerCreate(orgAnim, vrmAnim);
        }

        void PoseHandlerCreate(Animator org, Animator vrm)
        {
            OnDestroy();
            orgPose = new HumanPoseHandler(org.avatar, org.transform);
            vrmPose = new HumanPoseHandler(vrm.avatar, vrm.transform);
        }

        void OnDestroy()
        {
            if (orgPose != null)
                orgPose.Dispose();
            if (vrmPose != null)
                vrmPose.Dispose();
        }

        private float CalcFootSub()
        {
            Vector3 orgRightFoot = orgAnim.GetBoneTransform(HumanBodyBones.RightFoot).position;
            Vector3 orgLeftFoot = orgAnim.GetBoneTransform(HumanBodyBones.LeftFoot).position;
            Vector3 orgArgFoot = (orgRightFoot + orgLeftFoot) * 0.5f;

            Vector3 vrmRightFoot = vrmAnim.GetBoneTransform(HumanBodyBones.RightFoot).position;
            Vector3 vrmLeftFoot = vrmAnim.GetBoneTransform(HumanBodyBones.LeftFoot).position;
            Vector3 vrmArgFoot = (vrmRightFoot + vrmLeftFoot) * 0.5f;

            return (orgArgFoot - vrmArgFoot).y;
        }

        private static List<int> alreadyHashes = new List<int>();

        // 最初: -161139084
        // 通常: 229373857
        // 最初立ち上がり: -1536343465
        // 立ち上がり: -805461806
        // 座り始め: 890925016
        // 座り: -1544306596
        // 椅子: -1829310159
        // ベッド寝始め: 337039637
        // ベッド: -1603096
        // ベッド起き上がり: -496559199
        // Crouch: -2015693266

        private static List<int> adjustHipHashes = new List<int>()
        {
            -1536343465,
            890925016,
            -1544306596,
            -1829310159,
            337039637,
            -1603096,
            -496559199,
        };

        void Update()
        {
            vrmAnim.transform.localPosition = Vector3.zero;
            if (!ragdoll)
            {
                for (int i = 0; i < 55; i++)
                {
                    Transform orgTrans = orgAnim.GetBoneTransform((HumanBodyBones)i);
                    Transform vrmTrans = vrmAnim.GetBoneTransform((HumanBodyBones)i);

                    if (i > 0 && orgTrans != null && vrmTrans != null)
                    {
                        if ((HumanBodyBones)i == HumanBodyBones.LeftFoot || (HumanBodyBones)i == HumanBodyBones.RightFoot)
                        {
                            orgTrans.position = vrmTrans.position;
                        }
                        else
                        {
                            orgTrans.position = vrmTrans.position + Vector3.up * offset;
                        }
                    }
                }
            }

            vrmAnim.transform.localPosition += Vector3.up * offset;
        }

        void LateUpdate()
        {
            vrmAnim.transform.localPosition = Vector3.zero;

            Vector3 orgHipPos = orgAnim.GetBoneTransform(HumanBodyBones.Hips).position;
            orgPose.GetHumanPose(ref hp);
            vrmPose.SetHumanPose(ref hp);

            int nameHash = orgAnim.GetCurrentAnimatorStateInfo(0).shortNameHash;
            bool adjustFromHip = adjustHipHashes.Contains(nameHash);

            //if (!alreadyHashes.Contains(nameHash))
            //{
            //	alreadyHashes.Add(nameHash);
            //	Debug.Log(orgAnim.GetCurrentAnimatorClipInfo(0)[0].clip.name + ": " + nameHash);
            //}

            Transform vrmHip = vrmAnim.GetBoneTransform(HumanBodyBones.Hips);
            if (adjustFromHip)
            {
                vrmHip.position = orgHipPos;
            }

            float adjustHeight = 0.0f;
            if (nameHash == 890925016 || nameHash == -1544306596 || nameHash == -1829310159) // Sitting
            {
                adjustHeight += 0.1f;
            }

            if (!adjustFromHip)
            {
                adjustHeight = CalcFootSub();
            }

            Vector3 pos = vrmHip.position;
            pos.y += adjustHeight;
            vrmHip.position = pos;

            if (!ragdoll)
            {
                for (var i = 0; i < 55; i++)
                {
                    Transform orgTrans = orgAnim.GetBoneTransform((HumanBodyBones)i);
                    Transform vrmTrans = vrmAnim.GetBoneTransform((HumanBodyBones)i);

                    if (i > 0 && orgTrans != null && vrmTrans != null)
                    {
                        if ((HumanBodyBones)i == HumanBodyBones.LeftFoot || (HumanBodyBones)i == HumanBodyBones.RightFoot)
                        {
                            orgTrans.position = vrmTrans.position;
                        }
                        else
                        {
                            orgTrans.position = vrmTrans.position + Vector3.up * offset;
                        }
                    }
                }
            }

            vrmAnim.transform.localPosition += Vector3.up * offset;
        }
    }
}
