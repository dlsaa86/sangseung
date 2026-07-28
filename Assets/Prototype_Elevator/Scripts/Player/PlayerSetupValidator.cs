using UnityEngine;

namespace Ascend.Prototype.Player
{
    /// <summary>Editor-only self-check for the scene-owned first-person setup.</summary>
    public static class PlayerSetupValidator
    {
#if UNITY_EDITOR
        [UnityEditor.MenuItem("Ascend/Validate Player Setup")]
        private static void ValidatePlayerSetup()
        {
            FirstPersonController[] controllers = Object.FindObjectsByType<FirstPersonController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            CrosshairInteractor[] interactors = Object.FindObjectsByType<CrosshairInteractor>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            CrosshairView[] views = Object.FindObjectsByType<CrosshairView>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            int errors = 0;
            int warnings = 0;

            if (controllers.Length == 0)
            {
                Debug.LogError("[Ascend] Player setup: FirstPersonController를 찾지 못했습니다.");
                errors++;
            }
            else
            {
                for (int i = 0; i < controllers.Length; i++)
                {
                    FirstPersonController controller = controllers[i];
                    if (!controller.HasCharacterController)
                    {
                        Debug.LogError($"[Ascend] Player setup: {controller.name}에 CharacterController가 없습니다.", controller);
                        errors++;
                    }

                    if (controller.ViewCamera == null)
                    {
                        Debug.LogWarning($"[Ascend] Player setup: {controller.name}의 카메라 참조가 없습니다. 자식 Camera를 자동 검색합니다.", controller);
                        warnings++;
                    }
                }
            }

            if (interactors.Length == 0)
            {
                Debug.LogError("[Ascend] Player setup: CrosshairInteractor를 찾지 못했습니다.");
                errors++;
            }
            else
            {
                for (int i = 0; i < interactors.Length; i++)
                {
                    CrosshairInteractor interactor = interactors[i];
                    if (!interactor.HasCameraReference)
                    {
                        Debug.LogWarning($"[Ascend] Player setup: {interactor.name}의 카메라 참조가 없습니다. Main Camera를 자동 검색합니다.", interactor);
                        warnings++;
                    }

                    if (!interactor.HasViewReference)
                    {
                        Debug.LogWarning($"[Ascend] Player setup: {interactor.name}에 CrosshairView가 연결되지 않았습니다.", interactor);
                        warnings++;
                    }
                }
            }

            if (views.Length == 0)
            {
                Debug.LogWarning("[Ascend] Player setup: CrosshairView를 찾지 못했습니다. 씬에 uGUI 조준점을 배치하고 연결하세요.");
                warnings++;
            }
            else
            {
                for (int i = 0; i < views.Length; i++)
                {
                    CrosshairView view = views[i];
                    if (!view.HasCrosshairGraphic)
                    {
                        Debug.LogWarning($"[Ascend] Player setup: {view.name}에 중앙 조준점 Graphic이 없습니다.", view);
                        warnings++;
                    }

                    if (!view.HasPromptText)
                    {
                        Debug.LogWarning($"[Ascend] Player setup: {view.name}에 프롬프트 TMP_Text가 없습니다.", view);
                        warnings++;
                    }
                }
            }

            Debug.Log($"[Ascend] Player setup report: controllers={controllers.Length}, interactors={interactors.Length}, views={views.Length}, errors={errors}, warnings={warnings}.");
        }
#endif
    }
}
