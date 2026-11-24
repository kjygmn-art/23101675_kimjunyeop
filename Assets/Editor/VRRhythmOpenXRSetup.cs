using UnityEditor;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
using UnityEngine.XR.OpenXR.Features.Interactions;

namespace VRRhythmBeat.EditorTools
{
    /// <summary>
    /// PICO 4 Ultra PC VR (PICO Connect + SteamVR) 실행에 필요한
    /// Windows Standalone OpenXR Loader 및 컨트롤러 Interaction Profile을 자동 구성합니다.
    /// </summary>
    [InitializeOnLoad]
    public static class VRRhythmOpenXRSetup
    {
        private const string SetupVersionKey = "VRRhythmBeat_PCVROpenXRSetup_v2";
        private static bool _scheduled;

        static VRRhythmOpenXRSetup()
        {
            if (_scheduled) return;
            _scheduled = true;
            EditorApplication.delayCall += RunAutoSetupIfNeeded;
        }

        private static void RunAutoSetupIfNeeded()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Application.isBatchMode)
            {
                return;
            }

            if (EditorPrefs.GetBool(SetupVersionKey, false))
            {
                return;
            }

            ConfigurePCVROpenXR();
            EditorPrefs.SetBool(SetupVersionKey, true);
        }

        [MenuItem("VR Rhythm Beat/Configure PC VR OpenXR (PICO 4 Ultra + SteamVR)", false, 30)]
        public static void ConfigurePCVROpenXR()
        {
            // 1. Windows Standalone용 XR Plug-in Management에 OpenXR Loader 활성화
            if (!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.settingsKey, out XRGeneralSettingsPerBuildTarget perBuildTarget) || perBuildTarget == null)
            {
                Debug.LogWarning("[VR Rhythm Beat] XRGeneralSettingsPerBuildTarget 에셋을 찾지 못했습니다.");
                return;
            }

            if (!perBuildTarget.HasSettingsForBuildTarget(BuildTargetGroup.Standalone))
            {
                perBuildTarget.CreateDefaultSettingsForBuildTarget(BuildTargetGroup.Standalone);
            }

            XRGeneralSettings standaloneSettings = perBuildTarget.SettingsForBuildTarget(BuildTargetGroup.Standalone);
            if (standaloneSettings != null)
            {
                standaloneSettings.InitManagerOnStart = true;

                if (!perBuildTarget.HasManagerSettingsForBuildTarget(BuildTargetGroup.Standalone))
                {
                    perBuildTarget.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Standalone);
                }

                XRManagerSettings manager = perBuildTarget.ManagerSettingsForBuildTarget(BuildTargetGroup.Standalone);
                if (manager != null)
                {
                    bool hasOpenXR = false;
                    foreach (XRLoader loader in manager.activeLoaders)
                    {
                        if (loader is OpenXRLoader)
                        {
                            hasOpenXR = true;
                            break;
                        }
                    }

                    if (!hasOpenXR)
                    {
                        XRPackageMetadataStore.AssignLoader(manager, typeof(OpenXRLoader).FullName, BuildTargetGroup.Standalone);
                    }

                    EditorUtility.SetDirty(manager);
                }

                EditorUtility.SetDirty(standaloneSettings);
            }

            EditorUtility.SetDirty(perBuildTarget);

            // 2. Windows Standalone OpenXR Interaction Profiles 활성화
            //    PICO Connect + SteamVR 환경에서는 Oculus Touch 프로필만 사용해야
            //    Valve Index 프로필과 충돌해 컨트롤러 각도가 뒤틀리는 현상을 방지할 수 있습니다.
            OpenXRSettings pcOpenXR = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Standalone);
            if (pcOpenXR != null)
            {
                EnableFeature<OculusTouchControllerProfile>(pcOpenXR);
                EnableFeature<KHRSimpleControllerProfile>(pcOpenXR);
                DisableFeature<ValveIndexControllerProfile>(pcOpenXR);
                EditorUtility.SetDirty(pcOpenXR);
            }

            // 3. Android 탭에도 OculusTouchControllerProfile을 함께 켜두어 추후 APK 빌드 호환성 확보
            OpenXRSettings androidOpenXR = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            if (androidOpenXR != null)
            {
                EnableFeature<OculusTouchControllerProfile>(androidOpenXR);
                EditorUtility.SetDirty(androidOpenXR);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[VR Rhythm Beat] PC VR(Windows Standalone) OpenXR Loader 및 컨트롤러 프로필(Oculus Touch, KHR Simple, Valve Index) 설정이 완료되었습니다!");
        }

        private static void EnableFeature<T>(OpenXRSettings settings) where T : OpenXRFeature
        {
            T feature = settings.GetFeature<T>();
            if (feature != null)
            {
                feature.enabled = true;
                EditorUtility.SetDirty(feature);
            }
        }

        private static void DisableFeature<T>(OpenXRSettings settings) where T : OpenXRFeature
        {
            T feature = settings.GetFeature<T>();
            if (feature != null)
            {
                feature.enabled = false;
                EditorUtility.SetDirty(feature);
            }
        }
    }
}
