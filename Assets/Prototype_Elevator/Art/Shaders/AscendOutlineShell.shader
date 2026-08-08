// 조준한 물체의 **외곽선**. 껍질(shell) 방식이다.
//
// ## 왜 림이 아니라 껍질인가 (2026-08-08)
//
// 사용자 지시: 「조작 가능한 오브젝트에 마우스를 올리면 외곽선으로 선택할 수 있음을
// 알리면 좋겠어. 이건 **모든 오브젝트**에 해당하는 거임.」
//
// 첫 판본은 `Ascend/Stylized` 의 `_RimStrength` 를 올리는 방식이었다. 그런데 실측하니
// 상호작용물 6개 중 **5개가 림을 가진 렌더러 0개**였다 —
//
//     ExecutionLever   렌더러 1 → 림 0
//     PowerTank        렌더러 1 → 림 0
//     OverharvestLever 렌더러 9 → 림 0
//     DoorControl      렌더러 2 → 림 0
//     ContractPanel    렌더러 7 → 림 3
//     MachineFocus     렌더러 25 → 림 16
//
// 그것들은 `URP/Lit` 이라 `_RimStrength` 자체가 없다. 발광 폴백으로 물러났지만
// 「물체가 조금 밝아진다」는 외곽선이 아니다. 재질을 전부 `Ascend/Stylized` 로
// 옮기는 것은 **그림이 바뀌는 아트 결정**이라 할 수 없다.
//
// 껍질은 **원본 재질을 건드리지 않는다.** 같은 메시를 법선 방향으로 살짝 부풀려
// 앞면을 잘라내고(`Cull Front`) 그리면, 화면에는 물체 실루엣 바깥에만 테두리가 남는다.
// 어떤 셰이더를 쓰든 상관없고, 끄면 흔적이 0 이다.
Shader "Ascend/OutlineShell"
{
    Properties
    {
        _OutlineColor ("외곽선 색", Color) = (1, 0.86, 0.45, 1)
        // 화면 기준 폭. 거리에 비례해 키우므로 멀어져도 가늘어지지 않는다 —
        // 월드 고정 폭으로 하면 가까이서는 두껍고 멀리서는 사라진다.
        // 카메라 거리에 곱해지는 비율이다. 0.006 이면 2 m 거리에서 12 mm.
        _OutlineWidth ("외곽선 폭 (거리 비례)", Range(0, 0.03)) = 0.006

        // ⚠ `_DepthPush` 를 없앴다 (2026-08-09). 그 항의 목적은 「껍질이 물체보다 항상
        // 뒤에 있게 해서 몸통을 물체가 덮게 한다」였는데, 지금은 `ZTest Always` 로
        // 깊이 검사 자체를 쓰지 않고 **스텐실**이 몸통을 막는다. 목적이 사라진 손잡이를
        // 남겨 두면 다음 사람이 그것으로 문제를 풀려다 헛돈다.
    }

    SubShader
    {
        // **불투명보다 먼저** 그린다 (Geometry-1 = 1999 < 2000).
        //
        // ⚠ 처음엔 반투명 대기열(2900)에 두고 `ZWrite Off` + `ZTest LEqual` 로 몸통을
        // 걸러 내려 했다. 이론상 부풀린 뒷면은 물체 앞면보다 멀어서 깊이 검사에 걸려야
        // 하는데, **실제로는 걸러지지 않고 물체를 통째로 노랗게 덮었다**(실측 두 번).
        //
        // 그래서 깊이 검사에 기대지 않고 **그리는 순서**로 해결한다. 껍질이 먼저 그려지고
        // 물체가 그 위를 덮으면, 화면에는 껍질이 물체보다 삐져나온 가장자리만 남는다.
        // 이 방식은 물체가 어떤 셰이더를 쓰든, 깊이 텍스처가 있든 없든 성립한다.
        // **불투명 뒤에, 깊이 검사 없이** 그린다 (2026-08-09 사용자 지시).
        //
        // 「외곽선은 맨 위로 보이게, 모든 오브젝트를 뚫고 보이게 하면 해결될 것 같아.」
        //
        // 앞서는 불투명보다 **먼저**(1999) 그려 물체가 덮게 했다. 그 방식은 돌출된
        // 물체에는 맞지만, 기계처럼 **오목한 액자 안에 들어간** 대상은 바깥 실루엣이
        // 프레임에 가려 테두리가 거의 안 보였다. 안쪽 오염은 없앴는데 신호도 같이 죽었다.
        //
        // 그래서 순서를 뒤집는다 — 전부 그린 뒤 맨 위에 얹는다. 가려짐을 포기하는
        // 대신 **어떤 상황에서도 보이는 것**을 얻는다. 사용자가 그 교환을 택했다.
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-1" }

        Pass
        {
            Name "OutlineShell"
            // ⚠ `SRPDefaultUnlit` 이 아니라 `UniversalForward` 다.
            // 전자는 URP 에서 **불투명 단계가 끝난 뒤** 그려지므로, 대기열을 1999 로
            // 낮춰도 물체보다 먼저 그려지지 않는다 — 그래서 순서 방식이 안 먹고
            // 껍질이 물체를 덮은 채로 남았다(실측 3146 화소, 테두리가 아니라 덩어리).
            Tags { "LightMode" = "UniversalForward" }

            // 앞면을 잘라 **뒷면만** 남긴다. 부풀린 뒷면이 실루엣 바깥으로 삐져나온다.
            Cull Front
            // 깊이를 쓴다 — 그래야 껍질보다 앞에 있는 다른 물체가 테두리를 가려서
            // 벽 너머로 비쳐 보이지 않는다.
            // 깊이를 쓰지도 검사하지도 않는다 — 무엇에도 가리지 않고 맨 위에 얹힌다.
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha

            // **대상이 차지한 영역 안에는 그리지 않는다.** `Ascend/OutlineMask` 가
            // 대기열 1998 에서 대상 전체를 1 로 찍어 두므로, 여기서 1 이 아닌 곳만
            // 남기면 결과는 **바깥 실루엣 하나**다.
            //
            // 이것이 없으면 부품이 여럿인 대상에서 부품마다 테두리가 생긴다 —
            // 기계(렌더러 24 개)에서 현창 안쪽마다 초승달이 박혔던 것이 그것이다.
            Stencil
            {
                Ref 1
                Comp NotEqual
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings   { float4 positionCS : SV_POSITION; };

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float  _OutlineWidth;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 nrmWS = normalize(TransformObjectToWorldNormal(IN.normalOS));

                // ⚠ **클립 공간 XY 로 밀지 않는다** (2026-08-08 실측으로 폐기).
                //
                // 첫 판본은 `positionCS.xy += normalize(nrmCS.xy) * w` 였다. 카메라 축과
                // 나란한 면은 `nrmCS.xy` 가 0 에 가까운데, 그것을 `normalize` 하면 방향이
                // 정의되지 않는다. 그 면들이 아무 데로나 밀려나 **물체를 통째로 덮었다** —
                // 테두리가 아니라 노란 덩어리가 됐다.
                //
                // 월드 공간에서 법선을 따라 밀면 그런 특이점이 없다. 그리고 **뒷면이
                // 실제로 더 멀어지므로** 깊이 검사가 몸통을 걸러 내고 실루엣만 남는다 —
                // 이것이 껍질 방식이 성립하는 이유 자체다.
                //
                // 카메라 거리를 곱해 화면상 폭을 일정하게 유지한다.
                // 카메라 거리를 곱해 화면상 폭을 일정하게 유지한다.
                posWS += nrmWS * _OutlineWidth * distance(GetCameraPositionWS(), posWS);
                OUT.positionCS = TransformWorldToHClip(posWS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return half4(_OutlineColor.rgb, _OutlineColor.a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
