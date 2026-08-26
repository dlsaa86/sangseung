// HHLowfiBoot.cs — 로우파이(저텍스쳐·픽셀 크런치) 룩의 영구 스위치.
//
// 왜 스크립트인가: 텍스처 filterMode 는 런타임 값이라 플레이가 끝나면 임포터 기본값(Bilinear)으로
// 돌아간다. 임포터를 46+장 전부 고치는 대신, 씬이 뜰 때 한 번 훑어서 Point 로 바꾼다.
// 끄고 싶으면 이 컴포넌트를 비활성화하면 된다 — 에셋은 아무것도 안 건드렸으므로 원상복구다.
//
// 스타일 근거: 인스타 레퍼런스(@nineskvlls 로우폴리 렌더 2건, 2026-08-24 수집).
// 픽셀이 도드라지고, 텍스처는 거칠고, 오래된 필름 톤. 나머지 절반은
// HH_FilmLowfi 볼륨 프로필(그레인·비네트·청록 그림자)과 RP renderScale 0.42 + Point 업스케일이 담당한다.
using UnityEngine;

namespace HeavensHunger
{
    [DefaultExecutionOrder(-100)]
    public class HHLowfiBoot : MonoBehaviour
    {
        [Tooltip("쿼터 해상도 텍스처(2) — 텍셀이 커져 저텍스쳐가 읽힌다")]
        [SerializeField] int _mipmapLimit = 2;

        void Awake()
        {
            QualitySettings.globalTextureMipmapLimit = _mipmapLimit;
            ApplyPointFilter();
            ApplyRoomLighting();
        }

        /// <summary>
        /// 걸어다니는 공간 전체의 조도 보장 (설계자 지적 2026-08-25: 구석이 캄캄함).
        /// 기존 전구는 한쪽 구석에만 있어 반대편 벽이 죽는다 — 보조 전구를 코드로 보장해서
        /// 씬 저장 여부와 무관하게 항상 같은 조명 상태를 만든다.
        /// </summary>
        static void ApplyRoomLighting()
        {
            RenderSettings.ambientLight = new Color(0.42f, 0.50f, 0.49f);
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude))
                if (l.name == "LT_CabBulb") l.intensity = Mathf.Max(l.intensity, 5.5f);
            if (GameObject.Find("LT_CabBulb_B") == null)
            {
                var b = new GameObject("LT_CabBulb_B");
                var cab = GameObject.Find("CabinAD47");
                if (cab != null) b.transform.SetParent(cab.transform, true);
                b.transform.position = new Vector3(1.1f, 2.35f, 1.9f);
                var lb = b.AddComponent<Light>();
                lb.type = LightType.Point;
                lb.intensity = 3.0f; lb.range = 8f;
                lb.color = new Color(1f, 0.875f, 0.667f);
                lb.shadows = LightShadows.None;
            }
        }

        // 늦게 로드되는 텍스처(TMP 동적 아틀라스 등)를 위해 한 번 더.
        void Start() { ApplyPointFilter(); }

        /// <summary>
        /// 설계자 지적(2026-08-25): 캐빈의 구운 텍스처에 Point 를 먹이면 비스듬한 각도에서
        /// 지글거려 「깨져 보인다」. 예전에 만든 저작 텍스처(BK_/AO_/TEX_)는 원래 필터로 되돌리고,
        /// 픽셀 느낌은 심볼 스프라이트와 렌더스케일이 담당한다.
        /// </summary>
        static bool IsAuthoredCabinTex(string n)
            => n.StartsWith("BK_") || n.StartsWith("AO_") || n.StartsWith("TEX_") || n.StartsWith("T_");

        static void ApplyPointFilter()
        {
            foreach (var tex in Resources.FindObjectsOfTypeAll<Texture2D>())
            {
                if (tex == null) continue;
                var n = tex.name;
                // SDF 폰트 아틀라스는 거리장이라 Point 를 먹이면 글자가 깨진다 — 제외.
                if (n.Contains("SDF") || n.Contains("Font") || n.Contains("Nanum")
                    || n.Contains("Liberation") || n.Contains("HH_KR")
                    || n.StartsWith("UnityEditor") || n.Contains("icon") || n.Contains("Icon"))
                    continue;
                // 캐빈 저작 텍스처는 부드럽게 유지 — 이미 Point 가 먹었으면 되돌린다.
                if (IsAuthoredCabinTex(n))
                {
                    if (tex.filterMode == FilterMode.Point) tex.filterMode = FilterMode.Bilinear;
                    continue;
                }
                if (tex.filterMode != FilterMode.Point) tex.filterMode = FilterMode.Point;
            }
        }
    }
}
