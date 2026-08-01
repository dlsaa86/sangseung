using UnityEngine;

namespace Ascend.CaptureHarness.EditorTools
{
    /// <summary>
    /// 씬을 세우는 에디터 스크립트들이 머티리얼을 만드는 **한 군데**.
    ///
    /// ## 왜 만들었나
    ///
    /// 2026-08-02 전수 조사에서 나온 것: 씬의 렌더러 30종이 **전부**
    /// `Universal Render Pipeline/Lit` 을 쓰고, `Ascend/Stylized` 를 쓰는 렌더러가
    /// **0개**였다. `MAT_Ascend_Iron`·`_Wood`·`_Brass` 와 심볼 셋은 고아였다.
    ///
    /// `UP-VIS-01` 스타일 락의 네 축 중 셋(플랫 셰이딩·회녹색 그림자·폴리곤 면)이
    /// 그 셰이더 안에 구현돼 있었으므로, 독립 평가자는 아홉 번 다 **그레이박스**를
    /// 채점한 셈이다. 스타일 2.30/5 가 세 라운드 안 움직인 것은 개선이 부족해서가
    /// 아니라 개선한 것이 화면에 없어서였다.
    ///
    /// ## 왜 한꺼번에 갈지 않나
    ///
    /// 이 저장소는 셰이더 일괄 교체를 **두 번 되돌렸다**(6차 판정 「순손실」).
    /// 두 번 다 전부 갈고 나서 비교했고, 그러면 무엇이 좋아지고 무엇이 나빠졌는지
    /// 분리되지 않는다. 세 번째로 같은 방식을 쓰지 않는다.
    ///
    /// 그래서 호출부마다 `stylized` 를 따로 켠다. 한 무리를 켜고 → 캡처 →
    /// 독립 판정 → 통과하면 다음 무리. 나빠지면 그 호출부의 인자 하나만 되돌린다.
    /// 되돌리기가 한 줄이라는 것이 이 구조의 전부다.
    /// </summary>
    internal static class AscendMaterialFactory
    {
        private const string StylizedName = "Ascend/Stylized";
        private const string LitName = "Universal Render Pipeline/Lit";

        /// <summary>
        /// 머티리얼 하나. <paramref name="stylized"/> 가 참이고 셰이더를 찾을 수 있을
        /// 때만 스타일 셰이더를 쓴다 — 못 찾으면 **조용히 `Lit` 으로 내려간다.**
        /// 여기서 예외를 던지면 씬 생성 전체가 멈추고, 그건 스타일 하나 때문에
        /// 치르기엔 비싼 값이다. 대신 무엇이 쓰였는지 <paramref name="usedShader"/> 로
        /// 돌려주므로 호출부가 보고서에 적을 수 있다 — 「켰다」와 「걸렸다」를 가른다.
        /// </summary>
        public static Material Create(string name, Color color, bool stylized, out string usedShader)
        {
            Shader shader = stylized ? Shader.Find(StylizedName) : null;
            if (shader == null) shader = Shader.Find(LitName) ?? Shader.Find("Standard");
            usedShader = shader != null ? shader.name : "(없음)";

            var material = new Material(shader) { name = name };
            Tint(material, color);

            // 광택을 죽인다. 반사가 있으면 로우폴리 면이 매끈해 보여 PS1 방향에서 멀어진다.
            // 스타일 셰이더에는 이 프로퍼티가 없고, 없으면 건너뛴다.
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.03f);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
            return material;
        }

        /// <summary>
        /// 색을 넣는다. `Material.color` 하나로 끝내지 않는 이유 — 그건 `_Color` 를
        /// 찾고 없으면 `_BaseColor` 를 찾는 편의 함수인데, 커스텀 셰이더에서는
        /// **아무 데도 안 걸리고 조용히 무시될 수 있다.** 이름을 직접 짚는다.
        /// </summary>
        private static void Tint(Material material, Color color)
        {
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        }
    }
}
