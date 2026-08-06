using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Ascend.Prototype.Build;

namespace Ascend.Prototype.Demo.EditorTools
{
    /// <summary>
    /// 「이 빌드를 태우고 플레이하겠다」를 고르는 창. 노션 §11 「승객·부품 선택이 결과를
    /// 바꾸는 체감」을 사람이 직접 확인하는 입구다.
    ///
    /// 헤드리스 표(`tools/headless -- build`)가 이미 완주율을 재고 있다. 그런데 표가
    /// 답하지 못하는 것이 하나 있다 — **그 차이가 플레이에서 느껴지는가.**
    /// `BUILD_DIVERSITY_AUDIT.md` 자신이 그 한계를 적어 두었다("이 감사가 재는 것은
    /// 완주율 축 하나다"). 그건 사람이 태워 보는 수밖에 없고, 태워 보려면 원하는 빌드가
    /// 제시에 뜰 때까지 시드를 뒤지지 않아도 돼야 한다.
    /// </summary>
    public sealed class DemoLoadoutWindow : EditorWindow
    {
        private const string PrefKeyIds        = "Ascend.Demo.LoadoutIds";
        private const string PrefKeyKeepAboard = "Ascend.Demo.KeepAboard";

        private DemoLoadoutSpec _spec;
        private bool _keepAboard;
        private Vector2 _scroll;

        [MenuItem("Ascend/Demo Loadout")]
        public static void Open()
        {
            var window = GetWindow<DemoLoadoutWindow>(false, "데모 적재", true);
            window.minSize = new Vector2(420f, 480f);
            window.Show();
        }

        private void OnEnable()
        {
            _spec = DemoLoadoutSpec.Decode(EditorPrefs.GetString(PrefKeyIds, ""));
            _keepAboard = EditorPrefs.GetBool(PrefKeyKeepAboard, false);
        }

        private void Save()
        {
            EditorPrefs.SetString(PrefKeyIds, _spec.Encode());
            EditorPrefs.SetBool(PrefKeyKeepAboard, _keepAboard);
        }

        private void OnGUI()
        {
            if (_spec == null) _spec = new DemoLoadoutSpec();

            EditorGUILayout.LabelField("고른 적재", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                _spec.Count == 0 ? "(비어 있음 — 자동 정책이 그대로 돈다)" : _spec.Describe(),
                _spec.Count == 0 ? MessageType.None : MessageType.Info);

            List<string> problems = _spec.Problems();
            for (int i = 0; i < problems.Count; i++)
                EditorGUILayout.HelpBox(problems[i], MessageType.Warning);

            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                _keepAboard = EditorGUILayout.ToggleLeft(
                    new GUIContent("하차해도 다시 태운다",
                        "승객은 목적지 층에서 내린다. 축 하나를 10층까지 관측할 때만 켠다."),
                    _keepAboard);
                if (EditorGUI.EndChangeCheck()) Save();

                if (GUILayout.Button("비우기", GUILayout.Width(70f)))
                {
                    _spec.Clear();
                    Save();
                }
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("축 프리셋 — 노션 §6.3 내부 빌드 방향", EditorStyles.boldLabel);

            for (int a = 0; a < DemoLoadoutSpec.Axes.Length; a++)
            {
                BuildAxis axis = DemoLoadoutSpec.Axes[a];
                if (GUILayout.Button(DemoLoadoutSpec.AxisLabel(axis)))
                {
                    _spec = DemoLoadoutSpec.ForAxis(axis);
                    Save();
                }
            }

            if (GUILayout.Button("축마다 하나씩 (대조군)"))
            {
                _spec = DemoLoadoutSpec.OnePerAxis();
                Save();
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("카탈로그", EditorStyles.boldLabel);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawCatalog();
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(6f);
            DrawApplyRow();
        }

        private void DrawCatalog()
        {
            IReadOnlyList<BuildItem> all = BuildCatalog.All;

            for (int a = 0; a < DemoLoadoutSpec.Axes.Length; a++)
            {
                BuildAxis axis = DemoLoadoutSpec.Axes[a];
                EditorGUILayout.LabelField(DemoLoadoutSpec.AxisLabel(axis), EditorStyles.miniBoldLabel);

                EditorGUI.indentLevel++;
                for (int i = 0; i < all.Count; i++)
                {
                    BuildItem item = all[i];
                    if (item.Axis != axis) continue;

                    bool held = _spec.Contains(item.Id);
                    string suffix = item.Kind == BuildItemKind.Passenger
                        ? $"승객 · {item.Weight:0}kg · {item.DestinationFloor}층 하차"
                        : $"부품 · {item.Weight:0}kg";

                    EditorGUI.BeginChangeCheck();
                    bool next = EditorGUILayout.ToggleLeft($"{item.Label}   ({suffix})", held);
                    if (EditorGUI.EndChangeCheck())
                    {
                        if (next) _spec.Add(item.Id);
                        else _spec.Remove(item.Id);
                        Save();
                    }
                }
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(2f);
            }
        }

        private void DrawApplyRow()
        {
            var injector = Object.FindAnyObjectByType<DemoLoadoutInjector>();

            if (injector == null)
            {
                EditorGUILayout.HelpBox(
                    "열린 씬에 DemoLoadoutInjector 가 없다. 아래 버튼으로 만든다 " +
                    "(씬이 더티가 되므로 저장은 사람이 한다).", MessageType.Warning);

                if (GUILayout.Button("씬에 주입기 만들기"))
                {
                    var go = new GameObject("DemoLoadoutInjector");
                    Undo.RegisterCreatedObjectUndo(go, "Create DemoLoadoutInjector");
                    Undo.AddComponent<DemoLoadoutInjector>(go);
                    Selection.activeGameObject = go;
                }
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("씬의 주입기에 적용"))
                {
                    Undo.RecordObject(injector, "Apply Demo Loadout");
                    injector.SetSpec(_spec, _keepAboard);
                    EditorUtility.SetDirty(injector);
                }

                if (GUILayout.Button("주입기 끄기", GUILayout.Width(110f)))
                {
                    Undo.RecordObject(injector, "Disable Demo Loadout");
                    injector.SetSpec(new DemoLoadoutSpec(), _keepAboard);
                    EditorUtility.SetDirty(injector);
                }
            }

            EditorGUILayout.LabelField(
                injector.IsActive ? $"주입기: 켜짐 — {injector.Describe()}" : "주입기: 꺼짐",
                EditorStyles.miniLabel);
        }
    }
}
