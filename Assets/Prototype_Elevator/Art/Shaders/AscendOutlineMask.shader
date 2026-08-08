// 외곽선의 **마스크**. 화면에는 아무것도 그리지 않고 스텐실만 찍는다.
//
// ## 왜 필요한가 (2026-08-09 사용자 지적)
//
// 「외곽선만 선택되어야 해. 외곽 안쪽 객체까지 선택되는 느낌이야.」
//
// 조준 대상이 메시 하나면 껍질만으로 충분하다. 그런데 기계는 렌더러가 **24 개**라
// 부품마다 껍질이 생기고, 부품 사이의 오목한 틈(현창 안쪽 같은)에서는 그 껍질을
// 가려 줄 불투명 면이 없다. 그래서 창 안쪽마다 노란 초승달이 박혔다 —
// 「하나의 실루엣」이 아니라 「스물네 개의 테두리」였다.
//
// 실루엣 하나만 남기려면 **대상 전체가 차지한 화면 영역**을 먼저 알아야 한다.
// 이 셰이더가 그것을 스텐실 1 로 찍고, 껍질은 스텐실이 1 이 **아닌** 곳에만 그린다.
// 부품 사이의 틈은 이미 다른 부품이 1 로 찍어 두었으므로 테두리가 생기지 않는다.
//
// 대기열이 1998 인 이유: 모든 마스크가 **모든 껍질보다 먼저** 찍혀야 한다.
// 같은 재질의 두 패스로 만들면 Unity 가 오브젝트 단위로 패스를 돌려서
// A 의 껍질이 B 의 마스크보다 먼저 그려진다 — 그러면 목적이 무너진다.
Shader "Ascend/OutlineMask"
{
    Properties
    {
        // 마스크도 **함께 부풀린다** (2026-08-09 사용자 지적).
        //
        // 「내부 오브젝트는 외곽선이 안 보여야 해. 말 그대로 외곽선이니까 내부에는
        //  선이 없어야 해, 아무것도.」
        //
        // 마스크를 원본 크기로만 찍으면 **부품과 부품 사이 틈**은 어느 마스크에도
        // 덮이지 않는다. 그 틈으로 옆 부품의 껍질이 새어 나와 안쪽에 선이 남았다.
        // 마스크를 조금 부풀려 두면 그 틈이 메워지고, 살아남는 것은 대상 전체의
        // **바깥 경계 하나**뿐이다.
        //
        // 이 값은 껍질 폭보다 **작아야 한다.** 같거나 크면 껍질을 통째로 덮어
        // 외곽선이 아예 사라진다. 차이가 곧 선의 두께다.
        _OutlineWidth ("마스크 확장 폭 (거리 비례)", Range(0, 0.03)) = 0.004
    }

    SubShader
    {
        // 껍질이 불투명 뒤로 옮겨졌으므로 마스크도 같이 옮긴다 — 마스크가 껍질보다
        // **먼저** 찍히기만 하면 된다(2998 < 2999).
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-2" }

        Pass
        {
            Name "OutlineMask"
            Tags { "LightMode" = "UniversalForward" }

            // 색도 깊이도 건드리지 않는다. 스텐실만 남긴다.
            ColorMask 0
            ZWrite Off
            // 껍질이 `ZTest Always` 라 마스크도 같아야 한다. `LEqual` 로 두면 대상이
            // 가려졌을 때 스텐실이 안 찍혀서 껍질이 안쪽까지 통째로 칠해진다.
            ZTest Always
            Cull Back

            Stencil
            {
                Ref 1
                Comp Always
                Pass Replace
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings   { float4 positionCS : SV_POSITION; };

            CBUFFER_START(UnityPerMaterial)
                float _OutlineWidth;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 nrmWS = normalize(TransformObjectToWorldNormal(IN.normalOS));
                // 껍질과 **같은 방식으로** 부풀린다. 두 확장이 같은 규칙을 따라야
                // 차이가 일정한 두께의 선이 된다.
                posWS += nrmWS * _OutlineWidth * distance(GetCameraPositionWS(), posWS);
                OUT.positionCS = TransformWorldToHClip(posWS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target { return 0; }
            ENDHLSL
        }
    }
    Fallback Off
}
