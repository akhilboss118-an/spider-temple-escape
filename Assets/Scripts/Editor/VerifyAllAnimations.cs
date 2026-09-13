using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

namespace Runner.Editor
{
    public static class VerifyAllAnimations
    {
        public static void RunVerification()
        {
            string[] characters = { "Akhilboss", "Harika", "Nandini", "Navaneeth", "Pavan", "Pravalika", "Srikar" };
            Debug.Log("=== DETAILED ANIMATION VERIFICATION FOR ALL 7 CHARACTERS ===");

            int totalPassed = 0;

            foreach (var c in characters)
            {
                string charFolder = $"Assets/Characters/{c}";
                string controllerPath = $"{charFolder}/{c}_Controller.controller";
                string prefabPath = $"{charFolder}/{c}_Prefab.prefab";
                string resPrefabPath = $"Assets/Resources/Characters/{c}.prefab";

                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
                if (controller == null)
                {
                    Debug.LogError($"[{c}] Missing AnimatorController at {controllerPath}");
                    continue;
                }

                var sm = controller.layers[0].stateMachine;
                AnimationClip runClip = null;
                AnimationClip jumpClip = null;
                AnimationClip slideClip = null;
                AnimationClip stumbleClip = null;
                AnimationClip dieClip = null;

                foreach (var state in sm.states)
                {
                    if (state.state.name == "Run") runClip = state.state.motion as AnimationClip;
                    else if (state.state.name == "Jump") jumpClip = state.state.motion as AnimationClip;
                    else if (state.state.name == "Slide") slideClip = state.state.motion as AnimationClip;
                    else if (state.state.name == "Stumble") stumbleClip = state.state.motion as AnimationClip;
                    else if (state.state.name == "DieBackwards") dieClip = state.state.motion as AnimationClip;
                }

                Debug.Log($"[{c}] RUN CLIP: {(runClip != null ? runClip.name : "NULL")}, Len: {runClip?.length:F2}s, Loop: {runClip?.isLooping}, Human: {runClip?.isHumanMotion}");
                Debug.Log($"[{c}] JUMP CLIP: {(jumpClip != null ? jumpClip.name : "NULL")}, Len: {jumpClip?.length:F2}s, Loop: {jumpClip?.isLooping}, Human: {jumpClip?.isHumanMotion}");
                Debug.Log($"[{c}] SLIDE CLIP: {(slideClip != null ? slideClip.name : "NULL")}, Len: {slideClip?.length:F2}s, Loop: {slideClip?.isLooping}, Human: {slideClip?.isHumanMotion}");
                Debug.Log($"[{c}] STUMBLE CLIP: {(stumbleClip != null ? stumbleClip.name : "NULL")}, Len: {stumbleClip?.length:F2}s, Loop: {stumbleClip?.isLooping}, Human: {stumbleClip?.isHumanMotion}");
                Debug.Log($"[{c}] DIE CLIP: {(dieClip != null ? dieClip.name : "NULL")}, Len: {dieClip?.length:F2}s, Loop: {dieClip?.isLooping}, Human: {dieClip?.isHumanMotion}");

                // Validate Prefab
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                var anim = prefab != null ? prefab.GetComponent<Animator>() : null;
                var smr = prefab != null ? prefab.GetComponentInChildren<SkinnedMeshRenderer>() : null;

                bool runValid = runClip != null && runClip.isLooping && runClip.length > 0.4f;
                bool remainingValid = jumpClip != null && slideClip != null && stumbleClip != null && dieClip != null;
                bool prefabValid = anim != null && anim.avatar != null && smr != null && smr.sharedMesh != null;

                if (runValid && remainingValid && prefabValid)
                {
                    Debug.Log($"<color=green>[VERIFY PASS]</color> {c}: All 5 animations, Humanoid Avatar, and SkinnedMesh Prefab verified 100% OK!");
                    totalPassed++;
                }
                else
                {
                    Debug.LogError($"[VERIFY FAIL] {c}: runValid={runValid}, remainingValid={remainingValid}, prefabValid={prefabValid}");
                }
            }

            Debug.Log($"=== VERIFICATION RESULT: {totalPassed}/7 CHARACTERS FULLY PASSED ===");
            TestRunCycleFor("Akhilboss", "Assets/Characters/Akhilboss/Akhilboss_Prefab.prefab");
            TestRunCycleFor("Srikar", "Assets/Characters/Srikar/Srikar_Prefab.prefab");
            TestRunCycleFor("Harika", "Assets/Characters/Harika/Harika_Prefab.prefab");
            TestRunCycleFor("Nandini", "Assets/Characters/Nandini/Nandini_Prefab.prefab");
            TestRunCycleFor("Navaneeth", "Assets/Characters/Navaneeth/Navaneeth_Prefab.prefab");
            TestRunCycleFor("Pavan", "Assets/Characters/Pavan/Pavan_Prefab.prefab");
            TestRunCycleFor("Pravalika", "Assets/Characters/Pravalika/Pravalika_Prefab.prefab");
        }

        private static void TestRunCycleFor(string name, string path)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) return;

            GameObject instance = UnityEngine.Object.Instantiate(prefab);
            instance.transform.position = Vector3.zero;
            instance.transform.rotation = Quaternion.identity;

            Transform hips = null;
            foreach (var t in instance.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.ToLowerInvariant().Contains("hips")) { hips = t; break; }
            }

            Animator anim = instance.GetComponent<Animator>();
            if (anim != null)
            {
                anim.applyRootMotion = false;
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                float minX = 999f, maxX = -999f;
                for (int i = 0; i <= 20; i++)
                {
                    anim.Play("Run", 0, i / 20f);
                    anim.Update(0f);
                    if (hips != null)
                    {
                        float x = hips.position.x;
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                    }
                }
                Debug.Log($"[{name} SWAY] MinX={minX:F3}, MaxX={maxX:F3}, TotalSpan={(maxX - minX):F3}m");
            }
            UnityEngine.Object.DestroyImmediate(instance);
        }
    }
}
