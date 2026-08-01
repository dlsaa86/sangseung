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

                #ifdef _ADDITIONAL_LIGHTS
                uint count = GetAdditionalLightsCount();
                for (uint i = 0u; i < count; ++i)
                {
                    Light extra = GetAdditionalLight(i, input.positionWS);
                    float extraNdotl = saturate(dot(normalWS, extra.direction));
                    float extraAtten = pow(saturate(extra.distanceAttenuation * extra.shadowAttenuation),
                                           _FalloffPower);
                    lit += albedo * extra.color * Quantize(extraNdotl * extraAtten, _Steps);
                }
                #endif

                // 그림자 쪽을 회녹색으로 민다 — 검정으로 떨어뜨리면 「어두울 뿐」이 되고
                // 락이 요구한 「차가운 회녹색 그림자」가 안 나온다.
                //
                // **곱하지 않는다.** 첫 판본은 `_ShadowTint * _BaseColor` 였고,
                // 그래서 어두운 기본색(무쇠 0.16)이 그늘에서 거의 0 이 됐다.
                // 심볼 3종에 채택했다가 「거의 검은 덩어리」가 되어 되돌렸다.
                // 기본색을 일정 비율 남기고 색조를 **더한다** — 어두운 재질도 형태가 남는다.
                float3 shadowed = albedo * _ShadowLift + _ShadowTint.rgb * 0.35;
                float3 color = lerp(shadowed, lit, saturate(lambert + _AmbientFloor));

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
