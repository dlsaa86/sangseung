using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace Ascend.Prototype.Diagnostics
{
    /// <summary>비어 있는 필수 참조 하나. 원인과 경로를 함께 들고 다닌다.</summary>
    public readonly struct WiringDefect
    {
        /// <summary>씬 루트부터의 전체 경로. 「원인과 경로」의 경로 쪽이다.</summary>
        public readonly string HierarchyPath;

        public readonly string ComponentType;
        public readonly string FieldName;

        /// <summary>비어 있어서 무엇이 죽는가. 없으면 빈 문자열.</summary>
        public readonly string Consequence;

        public WiringDefect(string hierarchyPath, string componentType, string fieldName, string consequence)
        {
            HierarchyPath = hierarchyPath;
            ComponentType = componentType;
            FieldName = fieldName;
            Consequence = consequence ?? string.Empty;
        }

        public string Describe()
        {
            string tail = Consequence.Length > 0 ? $" — {Consequence}" : string.Empty;
            return $"{HierarchyPath} · {ComponentType}.{FieldName} 이(가) 비어 있다{tail}";
        }
    }

    /// <summary>훑은 결과. 테스트가 단정할 수 있도록 숫자를 남긴다.</summary>
    public readonly struct WiringValidationResult
    {
        public readonly IReadOnlyList<WiringDefect> Defects;

        /// <summary>필수로 표시된 필드가 몇 개 검사됐는가. 0이면 검사기가 아무것도 안 본 것이다.</summary>
        public readonly int RequiredFieldsChecked;

        /// <summary>훑은 MonoBehaviour 수.</summary>
        public readonly int BehavioursScanned;

        public WiringValidationResult(IReadOnlyList<WiringDefect> defects, int requiredFieldsChecked, int behavioursScanned)
        {
            Defects = defects ?? Array.Empty<WiringDefect>();
            RequiredFieldsChecked = requiredFieldsChecked;
            BehavioursScanned = behavioursScanned;
        }

        public bool IsClean => Defects.Count == 0;
    }

    /// <summary>
    /// 씬 로드 직후 <see cref="RequiredReferenceAttribute"/> 가 붙은 필드를 훑어
    /// 빈 것을 원인·경로와 함께 오류로 낸다.
    ///
    /// **왜 씬에 오브젝트를 두지 않는가:** 검사기를 씬 오브젝트로 만들면 그 오브젝트
    /// 자체가 빠질 수 있고, 그러면 「결선 검사기가 결선되지 않아 결선 실패를 놓친다」가
    /// 된다. <see cref="RuntimeInitializeOnLoadMethodAttribute"/> 는 씬 내용과 무관하게
    /// 플레이어가 실행하므로 그 실패 모드가 없다.
    ///
    /// **왜 에디터 전용이 아닌가:** `TECH_SPEC.md` §2 가 요구하는 것은 「개발 빌드와
    /// 에디터」다. 이전 판본(`PlayerSetupValidator`)은 `#if UNITY_EDITOR` 안의
    /// `MenuItem` 이라 빌드에 들어가지 않았고, 사람이 메뉴를 누를 때만 돌았다 —
    /// 즉 요구의 절반(빌드)과 성격(자동)을 둘 다 놓치고 있었다.
    /// </summary>
    public static class SceneWiringValidator
    {
        public const string LogPrefix = "[배선]";

        private static readonly Dictionary<Type, FieldInfo[]> FieldCache = new Dictionary<Type, FieldInfo[]>();
        private static readonly StringBuilder PathBuilder = new StringBuilder(128);

        /// <summary>마지막 훑기 결과. 자동 실행분을 테스트가 되짚어 볼 수 있게 남긴다.</summary>
        public static WiringValidationResult LastResult { get; private set; }

        /// <summary>자동 훑기가 한 번이라도 돌았는가.</summary>
        public static bool HasRun { get; private set; }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ValidateOnSceneLoad()
        {
            Validate(true);
        }
#endif

        /// <summary>
        /// 지금 로드된 씬의 필수 참조를 훑는다. <paramref name="log"/> 가 참이면
        /// 빈 것마다 <see cref="Debug.LogError(object, UnityEngine.Object)"/> 를 낸다 —
        /// 컨텍스트 오브젝트를 함께 넘기므로 콘솔에서 클릭하면 그 오브젝트가 잡힌다.
        /// </summary>
        public static WiringValidationResult Validate(bool log)
        {
            MonoBehaviour[] behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            List<WiringDefect> defects = new List<WiringDefect>();
            int checkedFields = 0;

            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null)
                {
                    continue;
                }

                Type type = behaviour.GetType();
                FieldInfo[] fields = RequiredFieldsOf(type);
                if (fields.Length == 0)
                {
                    continue;
                }

                string path = null;
                for (int f = 0; f < fields.Length; f++)
                {
                    checkedFields++;
                    FieldInfo field = fields[f];
                    if (!IsEmpty(field.GetValue(behaviour)))
                    {
                        continue;
                    }

                    path = path ?? HierarchyPathOf(behaviour.transform);
                    RequiredReferenceAttribute attribute =
                        (RequiredReferenceAttribute)Attribute.GetCustomAttribute(field, typeof(RequiredReferenceAttribute));

                    WiringDefect defect = new WiringDefect(path, type.Name, field.Name, attribute?.Consequence);
                    defects.Add(defect);

                    if (log)
                    {
                        Debug.LogError($"{LogPrefix} {defect.Describe()}", behaviour);
                    }
                }
            }

            WiringValidationResult result = new WiringValidationResult(defects, checkedFields, behaviours.Length);
            LastResult = result;
            HasRun = true;

            if (log)
            {
                if (defects.Count > 0)
                {
                    Debug.LogError(
                        $"{LogPrefix} 필수 참조 {defects.Count}건이 비어 있다 — " +
                        $"검사 {checkedFields}필드 / 컴포넌트 {behaviours.Length}개.");
                }
                else
                {
                    Debug.Log(
                        $"{LogPrefix} 필수 참조 이상 없음 — 검사 {checkedFields}필드 / " +
                        $"컴포넌트 {behaviours.Length}개.");
                }
            }

            return result;
        }

        /// <summary>
        /// 비어 있는가. `UnityEngine.Object` 는 `==` 를 오버로드해 파괴된 객체도 null 로
        /// 보고하지만, `object` 로 박싱된 상태에서 `== null` 을 쓰면 그 오버로드가 아니라
        /// 참조 비교가 걸려 **파괴된 객체가 살아 있는 것처럼 보인다.** 그래서 캐스트한다.
        /// </summary>
        private static bool IsEmpty(object value)
        {
            if (value == null)
            {
                return true;
            }

            if (value is UnityEngine.Object unityObject)
            {
                return unityObject == null;
            }

            if (value is string text)
            {
                return text.Length == 0;
            }

            // 배열은 **길이 0도 비어 있는 것으로 친다.** null 만 검사하면
            // `Transform[0]` 이 통과하는데, 그건 「연결했지만 아무것도 안 들어 있다」라
            // 결과는 null 과 똑같다 — 그 연출이 조용히 일어나지 않는다.
            if (value is System.Array array)
            {
                return array.Length == 0;
            }

            return false;
        }

        private static FieldInfo[] RequiredFieldsOf(Type type)
        {
            if (FieldCache.TryGetValue(type, out FieldInfo[] cached))
            {
                return cached;
            }

            List<FieldInfo> found = new List<FieldInfo>();
            for (Type cursor = type; cursor != null && cursor != typeof(MonoBehaviour); cursor = cursor.BaseType)
            {
                FieldInfo[] declared = cursor.GetFields(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                for (int i = 0; i < declared.Length; i++)
                {
                    if (Attribute.IsDefined(declared[i], typeof(RequiredReferenceAttribute)))
                    {
                        found.Add(declared[i]);
                    }
                }
            }

            FieldInfo[] result = found.ToArray();
            FieldCache[type] = result;
            return result;
        }

        /// <summary>씬 루트부터의 경로. 같은 이름이 여럿일 때 어느 것인지 가리는 유일한 수단이다.</summary>
        public static string HierarchyPathOf(Transform transform)
        {
            if (transform == null)
            {
                return "(경로 없음)";
            }

            PathBuilder.Clear();
            BuildPath(transform);
            return PathBuilder.ToString();
        }

        private static void BuildPath(Transform transform)
        {
            if (transform.parent != null)
            {
                BuildPath(transform.parent);
                PathBuilder.Append('/');
            }

            PathBuilder.Append(transform.name);
        }
    }
}
