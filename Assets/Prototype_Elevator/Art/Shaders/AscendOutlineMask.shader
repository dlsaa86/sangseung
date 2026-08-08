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
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry-2" }

        Pass
        {
            Name "OutlineMask"
            Tags { "LightMode" = "UniversalForward" }

            // 색도 깊이도 건드리지 않는다. 스텐실만 남긴다.
            ColorMask 0
            ZWrite Off
            ZTest LEqual
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

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings   { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target { return 0; }
            ENDHLSL
        }
    }
    Fallback Off
}
