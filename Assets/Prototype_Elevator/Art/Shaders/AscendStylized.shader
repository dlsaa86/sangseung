// 공통 스타일 셰이더 (`UP-VIS-04`).
//
// `VISUAL_BIBLE.md` §2.1 이 요구하는 것 중 셰이더가 책임지는 셋을 구현한다 —
//   · 「단순한 Gouraud 또는 플랫 셰이딩」  → 램버트를 계단으로 양자화한다
//   · 「제한적인 국소 조명과 빠른 감쇠」    → 감쇠를 거듭제곱으로 가파르게 만든다
//   · 「차가운 회녹색 그림자」              → 그림자 쪽을 회녹색으로 물들인다
//
// 왜 URP Lit 을 안 쓰는가: Lit 은 물리 기반이라 부드러운 하이라이트와
// 완만한 감쇠를 만든다. 그건 PS1~초기 PS2 감각의 반대다. 5차 시각 평가가
// 스타일 2.23/5 를 준 이유의 하나가 「전부 같은 무지 머티리얼」이었다.
//
// 왜 ShaderGraph 가 아닌가: `.shadergraph` 는 바이너리에 가까운 대형 YAML 이라
// 리뷰가 안 되고, 이 저장소는 직렬화 에셋이 조용히 깨진 이력이 있다.
// 텍스트 셰이더는 diff 가 읽힌다.
Shader "Ascend/Stylized"
{
    Properties
    {
        _BaseColor      ("기본 색", Color) = (0.55, 0.52, 0.46, 1)
        _ShadowTint     ("그림자 색조 (회녹색)", Color) = (0.20, 0.26, 0.24, 1)
        _Steps          ("명암 계단 수", Range(2, 8)) = 3
        _FalloffPower   ("감쇠 가파름", Range(1, 6)) = 2.5
        _AmbientFloor   ("주변광 바닥", Range(0, 0.6)) = 0.35
        _ShadowLift     ("그림자에 남는 기본색 비율", Range(0, 1)) = 0.55
        _RimStrength    ("실루엣 림", Range(0, 1)) = 0.25

        // **이 프로퍼티가 없으면 이 셰이더는 어디에도 채택할 수 없다.**
        // `SpinBoardView`(정화 점등) · `InstrumentPanelView`(계기 발광) ·
        // `OverharvestUnlockEffect`(덮개 레일) 셋이 `MaterialPropertyBlock` 으로
        // `_EmissionColor` 를 쓴다. 이름이 없으면 블록이 조용히 무시되고
        // **점등이 사라진 채 아무 오류도 안 난다** — `UP-CORE-12` 가 GIF 로 확인된
        // 바로 그 연출이 그렇게 죽는다.
        [HDR] _EmissionColor ("발광 (MaterialPropertyBlock 이 쓴다)", Color) = (0, 0, 0, 1)

        // **기본값이 흰색인 것이 이 프로퍼티의 안전 장치다.**
        //
        // 이 셰이더에는 텍스처 슬롯이 하나도 없었다. 그래서 `UP-VIS-01` 스타일 락의
        // 첫 항목(「저해상도 손그림 픽셀 텍스처」)이 에셋을 만들어도 화면에 올라갈 데가
        // 없었고, 머티리얼에 배정해도 **조용히 무시된 채 아무 오류도 안 났다.**
        //
        // 흰색을 곱하면 아무것도 달라지지 않는다. 그래서 기존 머티리얼 23장은
        // 이 변경 뒤에도 픽셀 단위로 같은 그림을 낸다 — 되돌릴 일이 생기지 않는다.
        // 이 저장소는 셰이더·머티리얼 일괄 교체를 두 번 되돌렸고(6차 판정 「순손실」),
        // 그 두 번 다 「바꾼 뒤에 비교」했다. 이번엔 **바꿔도 같은 상태**에서 시작해
        // 머티리얼 하나씩 텍스처를 물리며 매번 판정한다.
        _BaseMap ("표면 텍스처 (흰색이면 무지 — 기존과 동일)", 2D) = "white" {}

        // 진단 전용. 0 = 정상. 1 = 직접광만. 2 = 앰비언트 항만.
        // 기본이 0 이라 켜지 않는 한 그림에 영향이 없다.
        _DebugOutput ("진단 출력 (0 정상 · 1 직접광 · 2 앰비언트)", Range(0, 2)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            // **Forward+ 를 선언하지 않으면 추가 광원이 통째로 안 보인다.**
            // 이 프로젝트의 `PC_Renderer` 는 `m_RenderingMode = 2`(ForwardPlus)다.
            // 그 경로에서는 추가 광원이 화면 클러스터에 들어가고,
            // 고전 `GetAdditionalLightsCount()` 루프는 **0 을 돌려준다.**
            // URP 17 은 `_FORWARD_PLUS` 를 폐기하고 `_CLUSTER_LIGHT_LOOP` 로 바꿨다.
            // 옛 이름을 쓰면 컴파일은 되지만 경고와 함께 변형 수가 불어난다.
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _ShadowTint;
                float  _Steps;
                float  _FalloffPower;
                float  _AmbientFloor;
                float  _ShadowLift;
                float  _RimStrength;
                float4 _EmissionColor;
                float4 _BaseMap_ST;
                float  _DebugOutput;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float  fogCoord   : TEXCOORD2;
                float2 uv         : TEXCOORD3;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs   nrm = GetVertexNormalInputs(input.normalOS);
                output.positionCS = pos.positionCS;
                output.positionWS = pos.positionWS;
                output.normalWS   = nrm.normalWS;
                output.fogCoord   = ComputeFogFactor(pos.positionCS.z);
                output.uv         = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            /// 지각 휘도. 계단을 걸 축이고, 그림자가 환경을 따라가는지 재는 축이기도 하다.
            float Lum(float3 c) { return dot(c, float3(0.2126, 0.7152, 0.0722)); }

            // 램버트를 계단으로 끊는다. 이것이 「플랫/Gouraud」의 핵심이고,
            // 부드러운 그라디언트를 없애 폴리곤 면이 드러나게 한다.
            float Quantize(float value, float steps)
            {
                steps = max(2.0, steps);
                return floor(saturate(value) * steps) / (steps - 1.0);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                float ndotl = saturate(dot(normalWS, mainLight.direction));
                // 감쇠를 가파르게 — 「빠른 감쇠」. 거듭제곱이 가장 싸다.
                float atten = pow(saturate(mainLight.distanceAttenuation * mainLight.shadowAttenuation),
                                  _FalloffPower);
                float lambert = Quantize(ndotl * atten, _Steps);

                // 텍스처를 **기본색에 곱한다.** 흰 텍스처면 `albedo == _BaseColor.rgb` 라
                // 기존 머티리얼의 결과가 비트 단위로 같다. 아래 세 군데가 전부 이 값을 쓴다 —
                // 하나라도 `_BaseColor` 를 직접 쓰면 텍스처가 그늘에서만 사라지는
                // 「반쯤 적용」이 되고, 그게 가장 찾기 어려운 종류의 결함이다.
                float3 albedo = _BaseColor.rgb * SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb;

                float3 lit = albedo * mainLight.color * lambert;

                // **추가 광원이 위험 단계를 나른다.** 씬의 점광 1 · 스팟 1 이 그것이고,
                // 방향광은 벽에 균일하게 닿으므로 위험 연출을 실어 나르지 못한다.
                //
                // 그래서 이 루프가 도는지 아닌지가 「위험 단계가 벽에 보이는가」를 통째로
                // 결정한다. 세 번의 실패가 전부 여기서 나왔다 — 그림자 항을 상수로 둔 것도,
                // 환경 밝기를 곱한 것도, 계단 수를 올린 것도 전부 **빛이 0 인 상태**에
                // 대고 한 조정이었다. 좌벽 200px 이 정확히 한 가지 값이었던 것이 증거다.
                //
                // Forward+ 에서는 광원 순회를 `LIGHT_LOOP_BEGIN` 이 맡는다. 그 매크로가
                // `inputData.normalizedScreenSpaceUV` 로 화면 클러스터를 찾으므로
                // 아래 두 필드를 반드시 채워야 한다 — 안 채우면 조용히 0 개가 나온다.
                InputData lightInput = (InputData)0;
                lightInput.positionWS = input.positionWS;
                lightInput.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

                uint pixelLightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light extra = GetAdditionalLight(lightIndex, input.positionWS);
                    float extraNdotl = saturate(dot(normalWS, extra.direction));
                    float extraAtten = pow(saturate(extra.distanceAttenuation * extra.shadowAttenuation),
                                           _FalloffPower);
                    lit += albedo * extra.color * Quantize(extraNdotl * extraAtten, _Steps);
                LIGHT_LOOP_END

                // **빛은 더한다. 스타일은 더한 뒤에 건다.**
                //
                // 이 셰이더는 위험 단계를 두 번 먹었고, 두 번 다 원인이 여기였다.
                //
                // 첫 판본은 `lerp(shadowed, lit, saturate(lambert + _AmbientFloor))` 였다.
                // `shadowed` 는 재질 색과 색조만으로 만든 **상수**라 조명과 무관했고,
                // 섞임 계수 `lambert` 는 **주광만** 보고 만든 값이었다. 위험 연출은
                // **추가 광원**을 움직이는데, 그것들이 아무리 흔들려도 벽에 실리는 양이
                // 주광이 정한 만큼으로 눌렸다. `_AmbientFloor 0.35` 가 그 눌림의 바닥을
                // 고정했다.
                //
                // 11차 독립 판정 실측 — 벽에 켠 순간 좌벽 휘도가 89.2 → 79.4 → 72.5 → 66.7
                // (위험 4단계 단조 하강)에서 **네 단계 전부 82.3** 으로 평탄해졌고,
                // `Stable` 과 `Collapse` 프레임의 **60.5% 가 바이트 동일**이 됐다.
                // 「최악 상태의 계기가 가장 태연하다」(`UP-FIX-10`)가 환경 전체로 번진 것이다.
                //
                // 두 번째 시도는 그림자 항에 「환경 밝기」를 곱하는 것이었다. 더 나빠졌다 —
                // 좌벽이 위험 3단계에서 **55.24 로 완전히 동일**했다. 곱한 값 자체가 상수였다.
                // 증상을 한 겹 아래에서 다시 만든 것이고, 축을 안 바꾸고 세게 민 셈이다.
                //
                // URP/Lit 이 위험 단계를 그대로 보여 준 이유는 단순하다 — **그건 빛을 더한다.**
                // 계단 셰이딩과 회녹색 그림자는 빛을 어떻게 **누적하느냐**와 무관한 축이다.
                // 누적 방식까지 바꿀 이유가 없었다. 누적은 표준대로 두고, 스타일은
                // 누적이 끝난 뒤에 건다.
                //
                // 회녹색은 그래서 **주변광에 얹는다** — 빛이 없는데 색조가 남아 있으면
                // 그건 그림자가 아니라 자체발광이다. 검정으로 떨어뜨리지 않는 것이 락의
                // 요구이고(어두운 무쇠가 그늘에서 형태를 잃으면 안 된다), 그건 색조를
                // **더해서** 지킨다. 곱하면 어두운 기본색이 0 이 된다 — 심볼 3종에서
                // 그렇게 「거의 검은 덩어리」가 나와 되돌린 적이 있다.
                float3 sh = SampleSH(normalWS);
                float3 ambientTerm = albedo * sh * _ShadowLift
                                   + _ShadowTint.rgb * 0.35 * saturate(Lum(sh) * 2.0);
                float3 color = ambientTerm + lit;

                // **진단용 스위치. 기본 0 이면 아무것도 안 바뀐다.**
                //
                // 좌벽이 위험 3단계에서 109.45 로 똑같이 나오는데, 원인이 둘 중
                // 어느 쪽인지 추론으로는 안 갈렸다 — ①추가 광원이 프래그먼트에
                // 실제로 안 들어온다 ②들어오는데 앰비언트 항이 덮는다.
                // 실측한 것: `CabinLight` 는 1.6 → 0.54 로 변하고 좌벽 표면까지 1.20m,
                // range 7.00 이라 **닿는다.** 그러니 ①의 「거리 때문」은 이미 기각됐다.
                //
                // 두 항을 따로 화면에 내보내면 한 번의 캡처로 갈린다.
                // 1 = 직접광만 · 2 = 앰비언트 항만.
                if (_DebugOutput > 0.5 && _DebugOutput < 1.5) color = lit;
                else if (_DebugOutput >= 1.5) color = ambientTerm;

                // **계단은 이미 걸려 있다 — 빛마다, 감쇠에.** 여기서 최종 휘도에 또 걸지 않는다.
                //
                // 한 번 그렇게 해 봤고 위험 단계를 통째로 먹었다. `_Steps` 6 이면 한 칸이
                // 0.167 인데 위험 단계 사이의 차이는 0.07 남짓이다 — 네 단계가 **같은 칸**에
                // 떨어져 109.45 로 완전히 동일해진다. 계단 수를 8 로 올려도 마찬가지다.
                //
                // 계단이 끊어야 하는 것은 **형태 음영**이지 **전체 밝기**가 아니다.
                // 「빛이 얼마나 비스듬히 닿는가」는 칸으로 끊어야 폴리곤 면이 드러나고,
                // 「빛이 얼마나 센가」는 이어져야 위험이 읽힌다. 위쪽 `Quantize(ndotl * atten)`
                // 이 앞의 것을 하고, 빛 색·세기는 그 뒤에 연속으로 곱해진다.
                // 두 축이 한 곱셈 안에서 각자 제 일을 한다.
                // 실루엣을 살짝 세운다. 「큰 실루엣」이 락의 첫 항목이고,
                // 무지 머티리얼끼리 겹치면 경계가 사라진다.
                float3 viewDir = normalize(GetWorldSpaceViewDir(input.positionWS));
                float rim = pow(1.0 - saturate(dot(normalWS, viewDir)), 3.0);
                color += rim * _RimStrength * _ShadowTint.rgb;

                // 발광은 조명과 무관하게 **더한다**. 정화 점등은 어두운 칸에서도
                // 보여야 하고, 곱하면 그림자 쪽에서 사라진다.
                color += _EmissionColor.rgb;

                color = MixFog(color, input.fogCoord);
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        // 그림자 드리우기. 이게 없으면 이 셰이더를 쓴 물체가 그림자를 못 만든다.
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
            };

            ShadowVaryings ShadowVert(ShadowAttributes input)
            {
                ShadowVaryings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS   = TransformObjectToWorldNormal(input.normalOS);
                float4 clip = TransformWorldToHClip(
                    ApplyShadowBias(positionWS, normalWS, _LightDirection));
                #if UNITY_REVERSED_Z
                    clip.z = min(clip.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    clip.z = max(clip.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                output.positionCS = clip;
                return output;
            }

            half4 ShadowFrag(ShadowVaryings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
