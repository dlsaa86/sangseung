using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Ascend.CaptureHarness.EditorTools
{
    /// <summary>
    /// 게임 뷰 해상도를 고정한다. **화면 캡처의 해상도는 이것이 정한다.**
    ///
    /// 왜 필요한가: `ScreenCapture.CaptureScreenshotAsTexture()` 는 현재 게임 뷰 크기로
    /// 찍는다. 그래서 고정 캡처 21장 중 화면 경로로 찍는 3장(`17`·`19`·`20`)만
    /// **816×714** 로 나왔고 나머지 18장(RenderTexture 경로)은 1920×1080 이었다.
    /// 같은 세트 안에서 해상도가 갈리면 판독성 판정이 그 차이를 본다 —
    /// 실제로 독립 평가가 그 3장에 2·2·3점을 주면서 「816px 게임 뷰 종속일 수 있으니
    /// 1920 에서 재확인 필요」라고 단서를 달았다.
    ///
    /// **리플렉션을 쓴다.** `UnityEditor.GameViewSizes` 가 internal 이라 공개 API 가 없다.
    /// 그래서 **실패를 조용히 넘기지 않는다** — 실패하면 `false` 를 돌려주고, 부르는 쪽이
    /// 매니페스트에 「요청한 해상도가 아니다」를 적게 한다. 버전이 올라가 리플렉션이
    /// 깨졌을 때 캡처가 조용히 틀린 해상도로 나가는 것이 가장 나쁘다.
    /// </summary>
    public static class GameViewResolution
    {
        public const int SpecWidth = 1920;
        public const int SpecHeight = 1080;

        /// <summary>마지막 시도가 실패한 이유. 성공했으면 빈 문자열.</summary>
        public static string LastError { get; private set; } = string.Empty;

        [MenuItem("Ascend/Set Game View 1920x1080")]
        private static void SetSpecFromMenu()
        {
            bool ok = TrySetFixed(SpecWidth, SpecHeight);
            if (ok)
            {
                Debug.Log($"[캡처] 게임 뷰를 {SpecWidth}×{SpecHeight} 로 고정했다. " +
                          $"현재 Screen {Screen.width}×{Screen.height} " +
                          "(플레이 모드 진입 후 반영되는 경우가 있다).");
            }
            else
            {
                Debug.LogError($"[캡처] 게임 뷰 고정 실패 — {LastError}");
            }
        }

        /// <summary>
        /// 게임 뷰를 지정 해상도의 고정 크기로 맞춘다. 이미 같은 크기가 목록에 있으면
        /// 그것을 고르고, 없으면 추가한 뒤 고른다.
        /// </summary>
        public static bool TrySetFixed(int width, int height)
        {
            LastError = string.Empty;
            try
            {
                Assembly editorAssembly = typeof(Editor).Assembly;

                Type sizesType = editorAssembly.GetType("UnityEditor.GameViewSizes");
                Type groupType = editorAssembly.GetType("UnityEditor.GameViewSizeGroup");
                Type sizeType = editorAssembly.GetType("UnityEditor.GameViewSize");
                Type sizeKindType = editorAssembly.GetType("UnityEditor.GameViewSizeType");
                Type gameViewType = editorAssembly.GetType("UnityEditor.GameView");
                if (sizesType == null || groupType == null || sizeType == null ||
                    sizeKindType == null || gameViewType == null)
                {
                    LastError = "GameViewSizes 계열 내부 타입을 찾지 못했다 (Unity 버전 변경?)";
                    return false;
                }

                Type singletonType = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
                PropertyInfo instanceProperty = singletonType.GetProperty(
                    "instance", BindingFlags.Public | BindingFlags.Static);
                object sizes = instanceProperty?.GetValue(null);
                if (sizes == null)
                {
                    LastError = "GameViewSizes.instance 를 얻지 못했다";
                    return false;
                }

                object group = sizesType.GetProperty("currentGroup",
                    BindingFlags.Public | BindingFlags.Instance)?.GetValue(sizes);
                if (group == null)
                {
                    LastError = "currentGroup 을 얻지 못했다";
                    return false;
                }

                int index = FindIndexOf(groupType, sizeType, group, width, height);
                if (index < 0)
                {
                    // FixedResolution = 열거의 첫 값. 이름으로 찾아 값에 의존하지 않는다.
                    object fixedKind = Enum.Parse(sizeKindType, "FixedResolution");
                    object created = Activator.CreateInstance(
                        sizeType, fixedKind, width, height, $"Ascend {width}x{height}");
                    groupType.GetMethod("AddCustomSize",
                        BindingFlags.Public | BindingFlags.Instance)?.Invoke(group, new[] { created });
                    index = FindIndexOf(groupType, sizeType, group, width, height);
                }

                if (index < 0)
                {
                    LastError = "크기를 추가했으나 목록에서 다시 찾지 못했다";
                    return false;
                }

                EditorWindow window = EditorWindow.GetWindow(gameViewType, false, null, false);
                if (window == null)
                {
                    LastError = "게임 뷰 창을 열지 못했다";
                    return false;
                }

                MethodInfo select = gameViewType.GetMethod("SizeSelectionCallback",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (select == null)
                {
                    LastError = "GameView.SizeSelectionCallback 을 찾지 못했다";
                    return false;
                }

                select.Invoke(window, new object[] { index, null });
                window.Repaint();
                return true;
            }
            catch (Exception e)
            {
                LastError = e.GetType().Name + ": " + e.Message;
                return false;
            }
        }

        private static int FindIndexOf(Type groupType, Type sizeType, object group, int width, int height)
        {
            MethodInfo getTotalCount = groupType.GetMethod("GetTotalCount",
                BindingFlags.Public | BindingFlags.Instance);
            MethodInfo getGameViewSize = groupType.GetMethod("GetGameViewSize",
                BindingFlags.Public | BindingFlags.Instance);
            if (getTotalCount == null || getGameViewSize == null) return -1;

            PropertyInfo widthProperty = sizeType.GetProperty("width",
                BindingFlags.Public | BindingFlags.Instance);
            PropertyInfo heightProperty = sizeType.GetProperty("height",
                BindingFlags.Public | BindingFlags.Instance);
            if (widthProperty == null || heightProperty == null) return -1;

            int count = (int)getTotalCount.Invoke(group, null);
            for (int i = 0; i < count; i++)
            {
                object size = getGameViewSize.Invoke(group, new object[] { i });
                if (size == null) continue;
                if ((int)widthProperty.GetValue(size) == width &&
                    (int)heightProperty.GetValue(size) == height)
                {
                    return i;
                }
            }
            return -1;
        }
    }
}
