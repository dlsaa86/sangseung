using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Ascend.Prototype.Art;
using Ascend.Prototype.Build;
using Ascend.Prototype.Player;
using Ascend.Prototype.Risk;
using Ascend.Prototype.Spin;
using Ascend.Prototype.View;

namespace Ascend.Prototype.Run.Tests
{
    /// <summary>
    /// `AUTONOMOUS_PROTOTYPE_GOAL.md` §12의 필수 캡처 세트를 만든다.
    ///
    /// `HeroSliceCaptureRig`와 따로 두는 이유는 두 가지다. 첫째, 그 리그의 네 시점 좌표는
    /// 구 치수(내부 폭 3.20)에 맞춰 하드코딩돼 있어 지금은 벽 안쪽을 찍는다. 둘째,
    /// 요구 세트가 커졌다 — 위험 4단계, 적재 유무, 과수확 3단계가 새로 들어왔다.
    ///
    /// **위험 상태는 연출된 것이 아니라 실제 게임 상태다.** `RiskStateView`에는 단계를
    /// 강제하는 진입점이 없고 전부 `RiskEvaluator`가 게임 상태에서 계산한다. 그래서 이
    /// 리그는 무게를 싣고 과수확을 당겨 **점수를 실제로 올린다.** 무엇을 해서 그 단계에
    /// 도달했는지는 매니페스트에 남긴다 — 캡처가 실제 플레이에서 볼 수 없는 상태를
    /// 보여주면 그건 증거가 아니라 광고다.
    ///
    /// 비교 쌍은 같은 좌표로 찍는다: 위험 4단계, 화물칸 빈/최대.
    /// </summary>
    public sealed class TenFloorCaptureRig : MonoBehaviour
    {
        public const string DefaultOutputDirectory = "Captures/TenFloor";

        /// <summary>
        /// 진단 세트의 출력 위치. 기본은 <see cref="DefaultOutputDirectory"/> 다.
        ///
        /// 왜 바꿀 수 있어야 하나: 포스트 체인의 **디더링·필름 그레인이 축 둘을 서로
        /// 부정한다.** ±1 LSB 노이즈만으로 G-4(인접 화소 차 ≤ 1 인 평탄 구간)는
        /// 부서지고, 반대로 G-1(국소 분산)은 텍스처가 없어도 올라가 **거짓 그린**이 된다.
        /// 그래서 포스트를 끈 세트를 따로 찍어 G-1·G-4 를 거기서 재고, G-6 은 켠
        /// 세트에서 잰다. 두 세트는 **같은 카메라·같은 게임 상태**여야 비교가 성립하므로
        /// 리그를 복제하지 않고 출력 위치와 포스트 스위치만 바꾼다.
        /// </summary>
        public static string OutputDirectory
        {
            get => UnityEditor.EditorPrefs.GetString(OutDirPrefKey, DefaultOutputDirectory);
            private set => UnityEditor.EditorPrefs.SetString(OutDirPrefKey, value);
        }

        public static string ManifestPath => OutputDirectory + "/manifest.txt";

        /// <summary>
        /// 결과판 관심영역(ROI) 목록. `capture-metrics.ps1 -BoardRoiCsv` 가 읽어
        /// `G-SLOT-A`(결과판 띠·색)를 잰다.
        ///
        /// **왜 파일로 내보내는가**: 24장의 카메라 자세가 전부 달라 사각형 하나로는
        /// 대부분의 장에서 엉뚱한 곳을 잰다. 장마다 사각형이 필요하고, 그 사각형을
        /// 아는 것은 카메라 행렬을 가진 이 리그뿐이다.
        /// </summary>
        public static string BoardRoiPath => OutputDirectory + "/board-roi.csv";

        /// <summary>이 런에서 포스트 처리를 강제로 끌 것인가. 진단 세트 전용.</summary>
        public static bool PostDisabledForRun
        {
            get => UnityEditor.EditorPrefs.GetBool(NoPostPrefKey, false);
            private set => UnityEditor.EditorPrefs.SetBool(NoPostPrefKey, value);
        }

        private const string OutDirPrefKey = "Ascend.TenFloorCaptureRig.OutDir";
        private const string NoPostPrefKey = "Ascend.TenFloorCaptureRig.NoPost";

        /// <summary>
        /// 화면 캡처가 나와야 할 해상도. `VISUAL_SPEC.md:107` 의 기준 해상도이고,
        /// RenderTexture 경로는 <see cref="Width"/>×<see cref="Height"/> 로 이미 이 크기다.
        /// 게임 뷰가 이보다 작으면 화면 경로의 장들만 작아지므로 <see cref="ScreenShot"/> 이
        /// 매니페스트에 경고를 적는다. (여기 「18장」·「3장」이라고 적혀 있었는데 둘 다 틀렸다 —
        /// 세트가 커져도 안 따라오는 숫자는 주석에도 쓰지 않는다.)
        /// 고정은 에디터 쪽(`Ascend/Capture Ten Floor Set`)이 캡처 시작 전에 한다.
        /// </summary>
        public const int SpecCaptureWidth = 1920;
        public const int SpecCaptureHeight = 1080;
        private const string PrefKey = "Ascend.TenFloorCaptureRig.Armed";

        private const int Width = 1920;
        private const int Height = 1080;
        private const float Fov = 60f;

        private readonly StringBuilder _manifest = new StringBuilder();
        private readonly StringBuilder _boardRoi = new StringBuilder();
        private int _roiRows;
        private int _roiUnmeasurable;
        private Camera _camera;
        private RenderTexture _target;
        private Texture2D _readback;
        private int _shots;

        // **장수를 세어서 적기 위한 것들이다.** 예전에는 매니페스트가 「나머지 18장」·
        // 「이 한 장만 방식이 다르다」를 **하드코딩**으로 주장했다. 실제로는 23장이고
        // 화면 캡처 경로만 여럿이다(`UP-REC-06`). 하드코딩된 숫자는 세트가 커질 때마다
        // 조용히 틀려지고, 틀린 숫자를 매번 새로 찍어 낸다.
        private int _renderShots;
        private int _screenShots;

        /// <summary>이번 런이 실제로 쓴 PNG 파일명. 끝에서 폴더와 대조해 잔존물을 잡는다.</summary>
        private readonly HashSet<string> _written = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private readonly struct Pose
        {
            public readonly string Name;
            public readonly Vector3 Position;
            public readonly Vector3 LookAt;
            public Pose(string name, Vector3 position, Vector3 lookAt)
            {
                Name = name; Position = position; LookAt = lookAt;
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  🔴 좌표를 **명세에서 끌어온다** (2026-08-03 · `UP-FIX-69`)
        // ══════════════════════════════════════════════════════════════════
        //
        // 아래 열 시점은 전부 **존재하지 않는 방**의 좌표였다 — 구 캐빈
        // x[−1.20..1.20] · z[−1.50..1.50] · 높이 3.20, 장치가 **좌벽**에 붙어 있고
        // 출입구가 +Z 이던 시절이다. 지금 방은 4.0 × 4.6 × 2.9 이고 장치는
        // **후면 벽**(+Z), 가위문은 **좌벽**(−X), 선반은 우벽이다.
        //
        // 그 결과가 매니페스트에 그대로 찍혔다 —
        //   `02_device_front` : **결과판 9칸 중 프레임 안 0칸** · 계기 글자줄 프레임밖 6줄
        // 「필수 고정 캡처 세트」가 필수 대상을 한 칸도 담지 못했다.
        // 매니페스트는 그것을 정직하게 적었고, 그래서 **세트가 증거가 아니라 목록**이었다.
        //
        // `AscendHeroCapture` 는 처음부터 명세에서 끌어왔고 방이 바뀌어도 따라왔다.
        // 이 리그만 손으로 적혀 있었다. 같은 규약으로 맞춘다 —
        // **좌표를 손으로 적지 않는다. 장치가 움직이면 캡처도 따라 움직여야 비교가 성립한다.**
        //
        // 아래 주석들에 남은 옛 좌표 서술(x=-0.9 결과판, z=1.38 계기 등)은 **구 방의
        // 것이다.** 지우지 않고 남기는 이유는 그때의 실패와 그 해소 논리(예: 「대상을
        // 겨눈다」가 아니라 「두 대상의 **중점**을 겨눈다」)가 지금도 유효하기 때문이다.
        // 숫자만 낡았고 규칙은 살아 있다.

        // ── 방에서 유도한 기준값 ──
        private static float EyeY => ReferenceRoomSpec.EyeHeight;
        private static float DeviceFaceZ => ReferenceRoomSpec.MachineFrontZ;
        private static float LeverRightX
            => ReferenceRoomSpec.LeverColumnCenterX + ReferenceRoomSpec.LeverColumnWidth * 0.5f;
        /// <summary>캐비닛 좌단과 레버 우단의 중점. 둘을 한 화면에 담는 가로 중심이다.</summary>
        private static float DevicePairX => (ReferenceRoomSpec.MachineLeftX + LeverRightX) * 0.5f;
        /// <summary>캐비닛 하단과 경고등 상단의 중점.</summary>
        private static float DevicePairY
            => (ReferenceRoomSpec.MachineBottomY
              + ReferenceRoomSpec.WarningLampCenterY + ReferenceRoomSpec.WarningLampDiameter * 0.5f) * 0.5f;

        /// <summary>
        /// 반폭·반높이를 세로 60° 화각에 담는 최소 거리(m). 두 축을 다 계산하고 먼 쪽을 쓴다.
        /// 손으로 적은 거리는 장치 크기가 바뀌면 조용히 틀려진다.
        /// </summary>
        private static float FitDistance(float halfWidth, float halfHeight, float margin)
        {
            const float vFovDeg = 60f;
            float halfV = vFovDeg * 0.5f * Mathf.Deg2Rad;
            float halfH = Mathf.Atan(Mathf.Tan(halfV) * ReferenceRoomSpec.ReferenceAspect);
            return Mathf.Max((halfHeight + margin) / Mathf.Tan(halfV),
                             (halfWidth + margin) / Mathf.Tan(halfH));
        }

        // 좌표는 2026-07-31 비례 재조정 기준이었다(내부 x[-1.20..1.20] · z[-1.50..1.50] · 높이 3.20).
        // 눈높이 1.62를 유지한다 — 캡처가 플레이어가 실제로 보는 높이여야 판정이 성립한다.
        // 입구 시점을 두 번 틀렸다.
        //
        // 처음엔 y=1.35 를 내려다봐서 화면 절반이 무특징 검은 바닥이었다. 눈높이로
        // 들었더니 이번엔 **카메라가 과수확 레버 하우징 안에 들어갔다** —
        // `Housing` 은 x[0.25..0.85] · y[0.90..1.74] · z[0.95..1.47] 를 차지하는데
        // (0.72, 1.62, 1.38) 이 그 안이다. 독립 평가자가 "흰 다각형이 장치를 관통한다"고
        // 지적한 것은 관통 기하가 아니라 그 상자의 **안쪽 면**이었다.
        //
        // 통관 볼륨과 겹치는 렌더러를 전수 조사해서야 알았다 — 외부 기하는 하나도
        // 없었고, 그래서 원인이 씬이 아니라 카메라라는 것이 드러났다.
        // 하우징 왼쪽으로 비켜서 실내를 길이 방향으로 훑는다.
        //
        // 하우징을 피한 뒤에도 구도가 틀렸다 — 앞벽이 화면 중앙을 채우고 장치가
        // 오른쪽 끝으로 밀렸다. 시선을 장치 쪽으로 더 돌려 왼쪽 벽(결과판)과
        // 바닥·천장이 함께 들어오게 한다.
        // 출입구는 **좌벽 가위문**이다. 그 안쪽에 서서 방을 가로질러 본다 —
        // 시선에 바닥·장치·우측 선반이 함께 들어온다.
        private static Pose Entry => new Pose("Entry",
            new Vector3(ReferenceRoomSpec.WallLeftX + 0.60f, EyeY, -0.40f),
            new Vector3(0.10f, 1.15f, 1.80f));

        // **`UP-FIX-01` — 이 세트에는 높이를 보여주는 프레임이 0장이었다.**
        // 1차 독립 판정의 최우선 지적이고 지금까지 **한 번도 시도된 적이 없다**.
        // PRD §12.2 가 「좁고 높고 박스형」을 공간 미학 목표로 두는데 그 높이가
        // 어느 장에서도 읽히지 않았다.
        //
        // **왜 `01_entry` 로는 안 되는가 — 산수다.** 그 시점은 (0.12, 1.62, 1.30) 에서
        // (-0.85, 1.35, -0.35) 를 본다. 시선이 **아래로 8.03°** 다. 천장(안쪽면 y=3.20)을
        // 가로 화각 안에서 전수로 투영해 보면 **가장 낮게 찍히는 점조차 정규화 세로 +1.220**
        // 이다(프레임 위끝이 +1.000). 즉 어느 지점을 골라도 천장은 프레임 위로 벗어난다 —
        // 「높이 프레임 0장」은 취향 문제가 아니라 각도가 모자란 것이다.
        //
        // **그래서 반대로 선다.** 앞벽 앞 0.10 · 방 한가운데 · 눈높이를 0.95 로 낮추고
        // 시선을 **위로 10.4°** 준다. 낮은 눈높이는 두 가지를 동시에 준다 —
        // 천장이 위로 멀어지면서 **면으로 보이고**(선이 아니라), 뒷벽의 세로 화각 폭이
        // 줄어(h=0.95 에서 55.9° · h=1.62 에서 57.8°) 바닥선과 천장선이 **둘 다** 들어온다.
        //
        // 기하로 미리 계산한 값 (1920×1080 px · 수직 60° · `ndc` = −1 아래끝 … +1 위끝):
        //   뒷벽 안쪽면 z=1.50 m 의 바닥선 **−0.94 ndc** · 천장선 **+0.90 ndc**
        //     → 두 선 사이 1.84 ndc = 프레임세로의 **92%**
        //   출입구 상단(y=2.05 m) **+0.32 ndc** (사람 치수 기준자)
        //     → 문 위 여백 0.68 ndc = 프레임세로의 **34%**
        //     → 문 상단~천장선 0.58 ndc = 프레임세로의 **29%**
        //   천장면이 z 1.25…1.50 m 구간에서 프레임 위쪽 5.0% 를 띠로 채운다
        //   뒷벽 폭 2.40 m 이 가로 ±0.44 ndc → 44% 폭 × 92% 높이. **가로보다 세로가 길게 찍힌다**
        //
        // **여기 「문 위로 프레임의 58%」라고 적혀 있었다. 단위 혼동이다** — 0.58 은
        // 문 상단에서 천장선까지의 **ndc 길이**이고, 화면 전체가 2.0 ndc 이므로 프레임
        // 비율로는 **29%** 다. 8차 독립 판정이 이 수를 잡았고 그림에서 잰 값(≈34%)은
        // 문 위 *여백*(0.68 ndc)에 해당한다. **ndc 와 % 를 한 문장에 이름 없이 섞지 않는다.**
        // 변환은 `NdcSpanToFramePercent`(×50) 하나만 쓴다.
        // 이대로 나오지 않으면 이 안이 틀린 것이다. 실측은 매니페스트의 「높이 실측」줄에 남는다.
        //
        // 자리 확인 (씬 YAML 실측): FrontWall z −1.70…−1.50 (카메라 뒤 0.10) ·
        // ConsoleSlab x −1.19…−0.85 · ExecutionPlate x −1.18…−1.15 · Handrail_R x=1.14 ·
        // 통관 z −0.69…0.69 (x≈−0.95) — (0, 0.95, −1.40) 은 어느 것과도 겹치지 않는다.
        // 코너 챔퍼(x −1.20…−0.44 · z 0.74…1.50 · y 1.15…2.10)보다 아래이고 뒤다.
        //
        // **기존 아홉 시점은 하나도 건드리지 않았다.** 번호가 곧 정체성이라 이 장은 24 번으로
        // 새로 세운다 — 백로그 §5.1 에 번호를 겹쳐 사고 난 기록이 있다.
        // 낮게 서서 위를 본다 — 천장이 선이 아니라 면으로 보이고 바닥선도 함께 들어온다.
        // 규칙은 그대로고 좌표만 새 방에서 다시 뽑았다(앞벽 앞 0.30m · 눈높이 0.95).
        private static Pose EntryHeight => new Pose("EntryHeight",
            new Vector3(0f, 0.95f, ReferenceRoomSpec.WallFrontZ + 0.30f),
            new Vector3(0f, ReferenceRoomSpec.InteriorHeight * 0.66f, ReferenceRoomSpec.WallRearZ));

        // 캐비닛 **과 레버**를 한 화면에. 거리는 둘의 경계 상자에서 유도한다.
        private static Pose DeviceFront => new Pose("DeviceFront",
            new Vector3(DevicePairX, DevicePairY,
                        DeviceFaceZ - FitDistance((LeverRightX - ReferenceRoomSpec.MachineLeftX) * 0.5f,
                                                  DevicePairY - ReferenceRoomSpec.MachineBottomY, 0.14f)),
            new Vector3(DevicePairX, DevicePairY, DeviceFaceZ));

        // 사선 — 깊이 단계(벽 / 후면 프레임 / 캐비닛 / 도어 / 링 / 들어간 유리)가 갈리는 각도.
        private static Pose DeviceSide => new Pose("DeviceSide",
            new Vector3(ReferenceRoomSpec.MachineCenterX + 1.30f, EyeY, DeviceFaceZ - 1.15f),
            new Vector3(ReferenceRoomSpec.MachineCenterX, ReferenceRoomSpec.WindowGridCenterY + 0.06f, DeviceFaceZ));
        // 결과판은 x[-1.10..-0.76] · y[0.95..2.25] · z[-0.69..0.69]를 차지한다.
        // 처음엔 x=-0.30(판에서 0.54m)에 뒀더니 **카메라가 판 안에 들어가** 조각만
        // 잡혔다. 1.35m 물러나 가운데 줄 세 칸이 나란히 들어오게 한다 —
        // 한 화면에 구·정육면체·캡슐이 같은 크기로 놓여야 "3종 비교"가 된다.
        //
        // 그런데 1.35m 는 `DeviceFront`(x=0.35)와 0.05m 차이라 **두 장이 사실상 같은
        // 그림**이 됐다. 독립 평가자가 "04는 02와 중복이다. 세 심볼 비교는 별도
        // 근접 샷이어야 한다"고 지적했다. 판 앞면이 x=-0.76 이므로 x=-0.05 면
        // 0.71m — 판 안에 들어가지 않으면서 심볼이 화면을 채운다.
        // 가운데 줄 세 칸이 나란히 들어오는 거리. 세 심볼이 같은 크기로 놓여야 "3종 비교"다.
        // 거리는 세 칸의 가로 폭(간격 2칸 + 링)에서 유도한다.
        private static Pose SymbolClose => new Pose("SymbolClose",
            new Vector3(ReferenceRoomSpec.MachineCenterX, ReferenceRoomSpec.WindowGridCenterY + 0.05f,
                        DeviceFaceZ - FitDistance(ReferenceRoomSpec.WindowPitchX
                                                  + ReferenceRoomSpec.WindowRingDiameter * 0.5f,
                                                  ReferenceRoomSpec.WindowRingDiameter * 0.5f, 0.06f)),
            new Vector3(ReferenceRoomSpec.MachineCenterX, ReferenceRoomSpec.WindowGridCenterY, DeviceFaceZ));
        // 화물칸 시점은 **문지방 위**에서 내려다본다. 처음에는 (0.60, 1.62, 1.25)에 뒀는데
        // 최대 적재 상태에서 오른쪽 열 승객(x=0.85, z=0.35)이 카메라 코앞에 서서 화면의
        // 대부분을 검게 가렸다 — "동선이 살아 있는가"를 판정할 수 없는 그림이 나왔다.
        // 문 개구부 중심(x=0.65) 위 2.35m에서 안쪽을 내려다보면 여섯 자리가 모두 들어온다.
        // 화물칸은 **가위문 위**에서 중앙 바닥을 내려다본다. 문이 좌벽으로 옮겨졌으므로
        // 시점도 그쪽이다. 천장 2.9 아래에서 최대한 높게.
        private static Pose CargoBay => new Pose("CargoBay",
            new Vector3(ReferenceRoomSpec.WallLeftX + 0.45f, ReferenceRoomSpec.InteriorHeight - 0.45f, 0.20f),
            new Vector3(0.55f, 0.30f, -0.10f));

        // 위험 단계 — 조명·진동이 방 전체에서 어떻게 변하는지 보는 시점.
        private static Pose Risk => new Pose("Risk",
            new Vector3(0.85f, EyeY, -0.95f),
            new Vector3(-0.45f, 1.25f, 1.70f));
        // 과수확 레버는 x[0.25..0.85] · y[0.90..1.90] · z[0.91..1.47]을 차지한다.
        // 처음엔 0.9m 앞에 세웠더니 하우징이 화면을 통째로 덮어 "잠겼는가 열렸는가"를
        // 판정할 수 없었다. 1.5m 물러나 레버와 주변 맥락이 함께 들어오게 한다.
        // 과수확 시점을 뒤로 물리고 시선을 왼쪽으로 돌린다.
        //
        // 독립 시각 평가(2026-08-01 2회차)가 이 세 장(11·12·13)에서 **계기판 모든 줄의
        // 첫 글자가 좌측에서 잘린다**고 지적했다 — 「/ 10 위험도 안정」「력 0 / 요구 350」
        // 「핀 5/5」「류 없음」. 게임에서 가장 비싼 결정을 내리는 화면에서 전력 수치를
        // 읽을 수 없었다.
        //
        // 원인은 구도다. 레버는 x[0.25..0.85] z[0.91..1.47] 이고 계기 라벨은
        // x=-1.04 z=1.38 이라 둘이 1.6m 떨어져 있는데, 옛 시점은 레버만 겨눠
        // 계기가 프레임 왼쪽으로 밀려났다. 두 목표의 중간을 보고 0.6m 물러나면
        // 수평 화각(1920×1080 · 수직 60° → 수평 약 91°) 안에 38° 벌어짐으로 둘 다 들어온다.
        // **두 목표의 중점을 겨눈다** — 그 규칙은 그대로다. 다만 이제 레버와 계기가
        // 서로 다른 벽이 아니라 **같은 후면 벽**에 있어 훨씬 쉬워졌다.
        // 레버 컬럼(x 0.744)과 전력 계기(x 1.274)의 중점을 본다.
        private static Pose Overharvest => new Pose("Overharvest",
            new Vector3(0.45f, EyeY, DeviceFaceZ - 1.45f),
            new Vector3((ReferenceRoomSpec.LeverColumnCenterX + ReferenceRoomSpec.PowerMeterCenterX) * 0.5f,
                        (ReferenceRoomSpec.LeverPivotY + ReferenceRoomSpec.PowerMeterCenterY) * 0.5f,
                        DeviceFaceZ + 0.06f));

        // **금지 항목 `B-5 #15` 를 정면으로 겨냥한 시점.** 「핵심 결과가 특정 위치에서만
        // 보인다」가 이 세트의 유일한 금지 항목 위반이었다 — 3×3 결과판과 전력 계기가
        // 어느 캡처에서도 **동시에** 읽히지 않았다. 판을 보면 계기가 잘리고 계기를 보면
        // 판이 좌측 2열만 들어왔다.
        //
        // 결과판은 좌측 벽 x≈-0.9 의 z[-0.69..0.69], 계기 라벨은 같은 좌측의 z=1.38 이다.
        //
        // **첫 시도는 실패했고 그 실패가 규칙을 알려줬다.** 시선을 z=0.45 에 두었더니
        // 판은 온전히 들어왔는데 계기가 우측 끝에서 「력」 한 글자만 남고 잘렸다 —
        // 시선이 -z 쪽으로 치우쳐 계기가 화각 가장자리로 밀린 것이다.
        // 두 목표의 **중점**(z ≈ 0.69)을 보면 판이 -20°, 계기가 +19° 로 축을 사이에 두고
        // 대칭이 되어 둘 다 수평 화각(약 91°) 한가운데 들어온다.
        //
        // 「대상을 겨눈다」가 아니라 **「두 대상의 중점을 겨눈다」**가 이 문제의 규칙이다.
        // 🔴 **`B-5 #15` 를 겨냥한 시점이고, 이번 리블록아웃이 그 문제를 구조적으로
        // 풀어 줬다.** 예전에는 결과판이 좌벽, 계기가 같은 벽의 다른 끝이라 둘을 한
        // 화면에 넣기가 기하적으로 빠듯했다. 지금은 **캐비닛과 전력 계기가 같은
        // 후면 벽에 나란히** 있다 — 캐비닛 좌단(−1.274)부터 계기 우단(+1.634)까지.
        //
        // 거리는 그 폭에서 유도한다. 손으로 적으면 계기가 옮겨질 때 조용히 잘린다.
        private static float BoardGaugeLeftX => ReferenceRoomSpec.MachineLeftX;
        private static float BoardGaugeRightX
            => ReferenceRoomSpec.PowerMeterCenterX + ReferenceRoomSpec.PowerMeterWidth * 0.5f;
        private static Pose BoardAndGauge => new Pose("BoardAndGauge",
            new Vector3((BoardGaugeLeftX + BoardGaugeRightX) * 0.5f, 1.20f,
                        DeviceFaceZ - FitDistance((BoardGaugeRightX - BoardGaugeLeftX) * 0.5f, 0.62f, 0.16f)),
            new Vector3((BoardGaugeLeftX + BoardGaugeRightX) * 0.5f, 1.20f, DeviceFaceZ + 0.06f));
        // 계약 선택자는 오른쪽 벽의 명판 세 장(월드 x≈0.99, y 1.21~1.80)이다.
        // 처음에는 뒷벽 계기판을 함께 담으려 했지만 둘은 서로 90° 떨어진 벽에 있어
        // 한 프레임에 둘 다 읽히게 넣을 수 없었다. 조건 문구를 명판 옆으로 옮긴 뒤로는
        // 선택자 하나만 봐도 "무엇을·얼마에" 고르는지가 전부 들어온다.
        // 계약 패널은 이제 **우벽 앞쪽**에 있다(`AscendReferenceRoomRewire.RelocateInteractables`
        // 가 후면 벽에서 뺐다 — 밝은 판이 통관 장치를 정면에서 가렸기 때문이다).
        // 그 자리를 명세에서 다시 유도한다.
        private static float ContractX => ReferenceRoomSpec.WallRightX - 0.06f;
        private static float ContractZ
            => ReferenceRoomSpec.ShelfCenterZ - ReferenceRoomSpec.ShelfLength * 0.5f - 0.45f;
        private static Pose Contract => new Pose("Contract",
            new Vector3(ContractX - 1.25f, 1.55f, ContractZ + 0.05f),
            new Vector3(ContractX - 0.05f, 1.45f, ContractZ));

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (!UnityEditor.EditorPrefs.GetBool(PrefKey, false)) return;
            UnityEditor.EditorPrefs.SetBool(PrefKey, false);
            var go = new GameObject("TenFloorCaptureRig");
            go.AddComponent<TenFloorCaptureRig>();
        }

        /// <summary>기본 세트. 씬에 직렬화된 포스트 설정을 그대로 통과시킨다.</summary>
        public static void Arm()
        {
            OutputDirectory = DefaultOutputDirectory;
            PostDisabledForRun = false;
            UnityEditor.EditorPrefs.SetBool(PrefKey, true);
        }

        /// <summary>
        /// 포스트를 끈 진단 세트. G-1(국소 분산)·G-4(계단 평탄 구간)는 여기서 잰다 —
        /// 디더·그레인이 그 둘을 각각 거짓 그린과 거짓 레드로 만들기 때문이다.
        /// </summary>
        public static void ArmNoPost(string outputDirectory)
        {
            OutputDirectory = outputDirectory;
            PostDisabledForRun = true;
            UnityEditor.EditorPrefs.SetBool(PrefKey, true);
        }
#endif

        /// <summary>고정 프레임 시간. manifest 머리말에 적어 재현 조건을 남긴다.</summary>
        public const float CaptureDeltaTime = 1f / 60f;

        /// <summary>`UnityEngine.Random` 시드. Film Grain 오프셋이 여기에 걸려 있다.</summary>
        public const int RandomSeed = 20260802;
        private int _restoreVSync = -1;

        /// <summary>세운 시계를 되돌린다. 안 되돌리면 다음 Play 세션이 캡처 시계로 돈다.</summary>
        private void RestoreClock()
        {
            if (_restoreVSync < 0) return;
            Time.captureDeltaTime = 0f;
            QualitySettings.vSyncCount = _restoreVSync;
            _restoreVSync = -1;
        }

        private IEnumerator Start()
        {
            yield return null;

            // 프레임 시간을 못박는다. 이걸 세우지 않으면 `Time.deltaTime` 이 기계 부하를
            // 따라가고, 시간으로 재는 모든 대기가 **다른 프레임 수**로 끝난다.
            // 위험 상태 블렌딩·덮개 각도·승객 기울기가 전부 프레임 단위로 진행하므로
            // 같은 시드의 두 캡처가 바이트 단위로 달라진다. 고정 캡처 세트의 전제가
            // "같은 입력이면 같은 그림"이므로 여기서 시계를 고정한다.
            _restoreVSync = QualitySettings.vSyncCount;
            QualitySettings.vSyncCount = 0;
            Time.captureDeltaTime = CaptureDeltaTime;

            // **`UnityEngine.Random` 도 못박는다.** 판정 코어는 시드된 `System.Random` 을
            // 쓰므로 여기 영향을 받지 않지만(`TECH_SPEC` 결정론), **URP 의 Film Grain 은
            // 프레임마다 `Random.value` 로 노이즈 텍스처를 오프셋한다.** 그레인을 켠 채
            // 시드를 안 박으면 같은 시드의 두 캡처 런이 화소 단위로 갈리고, 이 세트의
            // 전제인 "같은 입력이면 같은 그림"이 깨진다. 프레임 수가 고정이므로
            // 시드를 박으면 소비 순서까지 같아진다.
            UnityEngine.Random.InitState(RandomSeed);

            var run = FindAnyObjectByType<RunSessionBehaviour>();
            var bridge = FindAnyObjectByType<RouletteInteractionBridge>();
            var risk = FindAnyObjectByType<RiskStateView>();
            var recorder = FindAnyObjectByType<AccidentRecorder>();
            if (run == null || bridge == null)
            {
                Debug.LogError("[상승] 캡처 리그: 씬 배선 없음");
                Finish();
                yield break;
            }

            SetupCamera();
            Header(run);

            // ── 1) 공간과 장치 (Stable, 적재 없음) ──
            run.ResetRun(RunMode.TenFloor, 1337);
            yield return WaitSeconds(2.5f);   // 09·10·16 과 같은 정착 시간을 준다

            // **판을 채운 뒤에 찍는다.** 처음에는 이 네 장이 전부 빈 판때기였다 —
            // 엔진은 스핀이 끝나면 수확·정화된 칸을 비우고 `ResetRun`도 판을 지우므로,
            // 아무 때나 찍으면 9칸이 전부 공란이다. 독립 평가자가
            // "세 심볼 비교 샷에 비교 대상이 0개"라고 지적했다.
            //
            // 게임의 핵심이 결과판인데 그것이 안 찍히면 이 세트는 무엇을 하는 게임인지
            // 보여주지 못한다.
            var board = FindAnyObjectByType<SpinBoardView>();

            // 열마다 한 종류씩. "세 통관이 각각 한 열"이라는 대응과 "세 심볼 구분"을
            // 한 장에서 동시에 보여준다.
            ShowBoard(board, ColumnPerKind());
            yield return WaitFrames(3);
            yield return Shot("02_device_front", DeviceFront, risk,
                "수확 장치 정면 — 세 통관과 3×3 대응 / 열마다 한 종류(영혼·흡수체·증식체)");
            yield return Shot("03_device_side", DeviceSide, risk, "1인칭 조작 거리에서 본 장치");
            yield return AimPromptScreenShot();
            yield return Shot("04_symbols", SymbolClose, risk,
                "세 심볼 비교 — 왼쪽 열 정상 영혼 / 가운데 흡수체 / 오른쪽 증식체");

            // 실제 추첨 결과도 한 장. 인위적 배열만으로는 "실제로 이렇게 나온다"를 못 보인다.
            ShowBoard(board, DrawSample(1337, 8, 0));
            yield return WaitFrames(3);
            yield return Shot("01_entry", Entry, risk, "입구에서 본 전체 내부 — 요구 캡처 §12");

            // **`UP-FIX-01` 전용 장.** `01_entry` 와 **같은 판·같은 상태·같은 시드**에서
            // 시점만 바꾼다 — 다른 것이 함께 달라지면 「높이가 보이게 됐다」의 원인 귀속이 무너진다.
            yield return Shot("24_entry_height", EntryHeight, risk,
                "**`UP-FIX-01` — 공간의 높이.** 1차 독립 판정의 최우선 지적이 " +
                "「높이를 보여주는 프레임이 0장」이었고 이 장이 그 첫 시도다. " +
                "`01_entry`(눈높이 1.62 m · 시선 **아래로 8.03°**)로는 원리적으로 안 된다 — " +
                "그 프레임에서 천장이 가장 낮게 찍히는 점조차 **+1.220 ndc** 다(위끝 +1.000 ndc). " +
                "이 장은 눈높이 **0.95 m** 에서 **위로 10.36°** 겨눈다. " +
                "**예측**(기하 계산 · `ndc` = −1 아래끝 … +1 위끝, 화면 전체 2.0 ndc): 뒷벽 안쪽면이 " +
                "바닥선 **−0.94 ndc** 부터 천장선 **+0.90 ndc** 까지, 즉 1.84 ndc = **프레임세로의 92%** 를 채우고, " +
                "출입구 상단(2.05 m)이 **+0.32 ndc** 에 와서 문 위 여백이 0.68 ndc = **프레임세로의 34%**, " +
                "문 상단~천장선이 0.58 ndc = **프레임세로의 29%** 가 된다. " +
                "**여기 「문 위로 프레임의 58%」라고 적혀 있었다 — 8차 판정이 잡은 단위 혼동이다.** " +
                "0.58 은 ndc 길이이고 프레임 비율로는 그 절반인 29% 다(ndc 길이 ×50 = 프레임 %). " +
                "**이대로 나오지 않으면 이 안이 틀린 것이다** — 다음 줄의 「높이 실측」이 실제 값이고, " +
                "높이가 실제로 읽히는가는 매니페스트가 아니라 평가자가 그림에서 판정한다. " +
                "`01_entry` 를 포함해 **기존 시점은 하나도 바꾸지 않았다**",
                RoomHeightFacts);

            yield return Shot("05_cargo_empty", CargoBay, risk, "빈 화물 공간 — 07 과 같은 좌표");

            // **금지 항목 `B-5 #15` 를 푸는 장.** 결과판과 전력 계기가 한 화면에 있어야
            // 「판을 보려면 계기를 포기한다」가 성립하지 않는다. 실제로 둘 다 들어왔는지는
            // 매니페스트가 주장할 것이 아니라 **평가자가 그림에서 확인할 것**이다 —
            // 이 세트는 「주장과 그림이 다른 장 9건」으로 반려된 적이 있다(`UP-FIX-13`).
            //
            // **이 장의 옛 문구가 그 9건 중 하나였다.** 「문틀 기하에 가려 파편만 남는다」라고
            // 네 라운드 동안 적혀 있었는데, 독립 설계자가 카메라→라벨 광선을 씬의 모든 후보와
            // 교차시킨 결과 **가리는 물체가 하나도 없었다**(`PASS3_STRUCTURAL_PLAN.md` §0).
            // 재지 않은 것을 원인이라고 적었고, 그 문장 때문에 네 번의 수정이 엉뚱한 곳을 팠다.
            // `UP-FIX-20` A안 — 순차 공개가 **정지 화면에서** 읽히는지를 이 장이 함께 판정한다.
            // 리그의 다른 장들은 `run.Spin()` 을 직접 불러 판을 밀어 넣으므로 `SpinPresenter`
            // 가 돌지 않고, 실제로 연출이 도는 장은 `22` 하나인데 그 화각에 결과판이 없다
            // (`UP-FIX-19`). 그래서 여기서 셔터 표식을 직접 세운다 — 2열까지 열린 상태면
            // **Open · Opening · Sealed 세 단계가 한 프레임에 동시에** 남는다.
            // 표식이 없으면 「공개 중」과 「빈 판」이 정지 화면에서 같아 보이고,
            // 그 상태에서는 `UP-CORE-11`(순차 공개)이 영원히 증명되지 않는다.
            FindAnyObjectByType<PurifyMarkerView>()?.ShowReveal(2);

            yield return Shot("21_board_and_gauge", BoardAndGauge, risk,
                "**금지 항목 `B-5 #15` 의 판정 장 — 3×3 결과판과 전력 계기가 한 화면에서 동시에 읽히는가.** " +
                "**성공했다고 주장하지 않는다.** 아래 「프레임 실측」의 숫자가 이 장이 말할 수 있는 전부이고, " +
                "읽히는가는 평가자가 그림에서 판정한다. " +
                "**직전 네 라운드의 진단은 틀렸다** — 여기에는 「문틀 기하에 가려 파편만 남는다」라고 적혀 있었으나, " +
                "카메라→라벨 광선을 씬의 모든 후보와 교차시킨 결과 **가리는 물체가 없다**" +
                "(`PASS3_STRUCTURAL_PLAN.md` §0). 실제 원인은 둘이었다 — " +
                "① 계기면을 스침각으로 본다(겉보기 폭 ×0.327 · 법선각 70.9°) " +
                "② 라벨의 글자축이 카메라 깊이축과 같아 한 줄의 일부가 구조적으로 프레임 밖으로 자란다. " +
                "이번 세트는 ①에 **코너 챔퍼**(계기판을 45° 회전해 결과판과의 법선 90° 분리를 화해시킨다)로 답했다. " +
                "**챔퍼 이전 실측값이 ×0.327 이었다** — 아래 계수가 그보다 크지 않으면 챔퍼는 듣지 않은 것이다. " +
                "②는 이 장이 고치지 않았다");

            // **세운 표식을 여기서 반드시 내린다** (`UP-FIX-41`).
            //
            // 바로 위에서 `ShowReveal(2)` 로 셔터 막대를 직접 세웠는데 끝까지 내리지 않아
            // **그 뒤의 모든 장에 막대가 서 있었다** — 07·08·06·09·10·11·12·13·14·15…
            // `PurifyBar_07` 이 `13_overharvest_pulled` 의 계기판 「스핀」·「흡」 글자를 먹었고,
            // 네 라운드 동안 그 증상이 계기판 배치 문제로 오진됐다.
            //
            // 이 리그의 다른 장들은 `run.Spin()` 을 직접 불러 `SpinPresenter` 를 우회하므로
            // 아무도 이 표식을 대신 지워 주지 않는다. **세운 쪽이 내린다.**
            FindAnyObjectByType<PurifyMarkerView>()?.Clear();
            yield return WaitFrames(1);

            yield return CapturePresentingScreen(run, bridge, risk);

            // ── 2) 적재 ──
            // 2층까지 몰고 가서 실을 수 있는 만큼 싣는다. 최대 적재 캡처는 "동선이
            // 살아 있는가"를 판정하는 자료라 실제로 꽉 찬 상태여야 한다.
            yield return DriveToFloor(run, bridge, 2);
            FloorSession floor = run.Session.Current;
            if (floor != null && floor.Phase == FloorPhase.Boarding)
            {
                while (floor.BuildOffers.Count > 0 && run.TakeBuildOffer(0)) yield return null;
                run.FinishBoarding();
            }
            // 슬롯을 마저 채운다 — 한 층의 후보만으로는 6칸이 안 찬다.
            foreach (BuildItem item in BuildCatalog.All)
            {
                if (run.Session.Loadout.IsFull) break;
                run.Session.Loadout.Add(item);
            }
            yield return WaitFrames(4);

            yield return Shot("07_cargo_full", CargoBay, risk,
                $"최대 적재 {run.Session.Loadout.Count}개 / {run.Session.CarriedWeight:F0}kg — 05 와 같은 좌표");
            yield return Shot("08_passenger_and_device", DeviceSide, risk,
                "승객과 장치가 한 화면에 — 적재가 장치 접근을 막지 않는가");

            // ── 3) 위험 4단계 (같은 좌표) ──
            //
            // **같은 좌표만으로는 대조가 안 된다.** 처음에는 Stable 을 1층·화물 없음에서
            // 찍고 Strain·Critical 을 2층·화물 있음에서 찍었다. 독립 평가자가 바로
            // 짚었다 — "좌측에 화물 상자가 있고 없고가 상태 차이보다 눈에 먼저 띈다.
            // 지금 세트로는 '위험 단계가 공간을 바꾸는가'를 판정할 수 없다."
            //
            // 대조군은 **나머지를 고정해야** 대조군이다. Stable 도 여기서 찍는다 —
            // 같은 층, 같은 적재, 같은 요구 전력. 달라지는 것은 위험 단계뿐이다.
            //   Strain   ← 과적 (OverloadScore 3.0 ≥ StrainEnter 3.0)
            //   Critical ← 과적 + 과수확 (3.0 + 3.2 + 잔류 ≥ CriticalEnter 7.0)
            //   Collapse ← 층 실패 (점수와 무관하게 Collapse) — 이것만 별도 런이다
            FloorSession riskFloor = run.Session.Current;
            yield return WaitSeconds(2.5f);   // 09·10 과 같은 정착 시간을 준다
            yield return Shot("06_risk_stable", Risk, risk,
                $"Stable — {(riskFloor != null ? riskFloor.Plan.Floor : 0)}층 / " +
                $"적재 {run.Session.Loadout.Count}개 {run.Session.CarriedWeight:F0}kg / " +
                $"요구 {(riskFloor != null ? riskFloor.RequiredPower : 0f):F0} / " +
                $"실제 단계 {LevelName(risk)} — 대조군. 09·10·16 과 **고정된 것**: 층·카메라 좌표·적재 개수. " +
                "**달라지는 것**: 무게(09 에서 +140kg)·요구 전력·소비 스핀. " +
                "과적이 요구를 끌어올리는 것이 Strain 의 정의라 그것까지 고정하면 상태 자체를 만들 수 없다. " +
                "즉 이 넷은 '같은 판을 같은 각도에서 본 것'이지 '같은 숫자'가 아니다");

            run.Session.AddWeight(140f);   // 허용 중량을 확실히 넘긴다
            yield return WaitSeconds(2.5f);   // 조명·험 블렌딩이 수렴할 시간(2.2/초)
            yield return Shot("09_risk_strain", Risk, risk,
                $"Strain — {(riskFloor != null ? riskFloor.Plan.Floor : 0)}층 / " +
                $"과적 {run.Session.CarriedWeight:F0}/{run.Session.WeightCapacity:F0} / " +
                $"실제 단계 {LevelName(risk)} — 06 에서 무게만 +140kg");

            yield return ForceCritical(run, bridge, risk);
            yield return Shot("10_risk_critical", Risk, risk,
                $"Critical — 실제 단계 {LevelName(risk)} / 점수 {(risk != null ? risk.Score : 0f):F1}");

            // ── 4) 과수확 3단계 ──
            //
            // 해제 조건은 `Decision && CanBank && SpinsRemaining > 0`이다. 시드 하나로는
            // 요구 전력을 마지막 스핀에서야 넘길 수 있고, 그러면 남은 스핀이 0이라
            // 영영 해제되지 않는다. 실제로 첫 촬영이 `unlocked=False`로 나왔다.
            // 조건을 만족하는 시드를 찾을 때까지 돌린다.
            var overharvest = FindAnyObjectByType<InteractableOverharvestLever>();
            int chosenSeed = 12;
            foreach (int seed in new[] { 12, 1337, 7, 4242, 90210, 1, 31415, 271828 })
            {
                run.ResetRun(RunMode.TenFloor, seed);
                yield return WaitFrames(3);
                chosenSeed = seed;
                yield return SpinUntilBankable(run, bridge);
                yield return WaitFrames(2);
                FloorSession probe = run.Session.Current;
                if (probe != null && probe.Phase == FloorPhase.Decision &&
                    probe.CanBank && probe.SpinsRemaining > 0) break;
            }

            // 잠금 상태는 조건을 만족하기 **전** 상태여야 하므로 새 런에서 찍는다.
            //
            // `WaitFrames(4)` 였다. 에디터 Play 는 100fps 넘게 돌아 약 0.04초다.
            // `RiskStateView._blendSpeed` 는 2.2/초라 직전 런의 위험 상태에서
            // 안정으로 수렴하는 데 1.4초가 걸린다 — 그래서 이 장이 "위험도 안정" 문구와
            // **적색 램프**를 함께 찍었다. 다른 위험 샷들은 이미 `WaitSeconds(2.5f)` 를 쓴다.
            // 같은 함정을 두 번 밟았다.
            run.ResetRun(RunMode.TenFloor, chosenSeed);
            yield return WaitSeconds(2.5f);
            yield return Shot("11_overharvest_locked", Overharvest, risk,
                $"잠금 상태 — 시드 {chosenSeed} / unlocked={(overharvest != null && overharvest.IsUnlocked)}");

            yield return SpinUntilBankable(run, bridge);
            // 브리지가 해제를 반영하고 보호 덮개가 다 열릴 때까지 기다린다. 해제와
            // 조작 가능은 같은 순간이 아니다 — 첫 촬영에서 `unlocked=False`가 찍힌 이유다.
            yield return WaitFrames(3);
            if (overharvest != null)
            {
                float deadline = Time.realtimeSinceStartup + 8f;
                while (!overharvest.IsCoverOpen && Time.realtimeSinceStartup < deadline)
                    yield return null;
                yield return WaitFrames(2);
            }
            yield return Shot("12_overharvest_unlocked", Overharvest, risk,
                $"해제 순간 — unlocked={(overharvest != null && overharvest.IsUnlocked)} " +
                $"덮개열림={(overharvest != null && overharvest.IsCoverOpen)} " +
                $"조작={(overharvest != null && overharvest.CanInteract)}");

            // **당긴 직후**를 찍는다. 연출이 끝나기를 기다리면 덮개가 다시 닫혀
            // 11(잠금)과 기하학적으로 같은 그림이 된다 — 첫 촬영이 그랬다.
            // 판돈이 빠지고 스핀이 도는 순간이 이 선택의 결과다.
            float anteBefore = 0f;
            int extraBefore = 0;
            if (overharvest != null && overharvest.CanInteract)
            {
                FloorSession before = run.Session.Current;
                anteBefore = before != null ? before.PendingAnte : 0f;
                extraBefore = before != null ? before.ExtraSpinsTaken : 0;
                overharvest.Interact(gameObject);
                yield return WaitFrames(2);
            }
            FloorSession afterPull = run.Session.Current;
            yield return Shot("13_overharvest_pulled", Overharvest, risk,
                afterPull != null
                    ? $"당긴 직후 — 판돈 {anteBefore:F0} 지불 / 추가 스핀 {extraBefore}→{afterPull.ExtraSpinsTaken} / " +
                      $"전력 {afterPull.Power:F0}/{afterPull.RequiredPower:F0} / 위험 {LevelName(risk)}"
                    : "당긴 직후");
            yield return WaitWhileLocked(bridge);

            // ── 5) 계약 선택 ──
            run.ResetRun(RunMode.TenFloor, 1337);
            yield return WaitFrames(2);
            yield return DriveToFloor(run, bridge, 6);   // 계약이 처음 나오는 층
            yield return WaitFrames(3);
            FloorSession contractFloor = run.Session.Current;

            // **탑승 단계에서 찍으면 계약 단계가 아니다.** 첫 시도가 그랬고, 계기판은
            // 당연히 "확정 — 변화 없음"을 띄웠다. 캡처 이름이 `contract_select`인데
            // 화면은 계약을 고르는 중이 아니었던 것이다. 문을 닫아 단계를 넘긴다.
            if (contractFloor != null && contractFloor.Phase == FloorPhase.Boarding)
            {
                run.FinishBoarding();
                yield return WaitFrames(3);
            }
            yield return Shot("14_contract_select", Contract, risk,
                contractFloor != null
                    ? $"{contractFloor.Plan.Floor}층 {contractFloor.Phase} — 선택지 " +
                      $"{contractFloor.Plan.ContractChoices.Length}종 / 미리보기 " +
                      $"{(bridge != null ? bridge.PreviewIndex + 1 : 0)}"
                    : "계약 층 도달 실패");

            // ── 6) 깊은 연쇄 ──
            yield return CaptureDeepCascade(run, bridge, risk);

            // ── 7) 사고와 결과 ──
            yield return CaptureCollapse(run, bridge, risk, recorder);

            Finish();
        }

        // ── 상태 만들기 ─────────────────────────────────────────────────────

        private static string LevelName(RiskStateView risk)
            => risk != null ? risk.Level.ToString() : "—";

        // ── 결과판 채우기 ────────────────────────────────────────────────────

        /// <summary>
        /// 결과판에 보드를 밀어 넣고 연출자가 덮어쓰지 못하게 잠근다.
        ///
        /// `DrivenExternally`를 세우지 않으면 `SpinBoardView`가 다음 프레임에
        /// 런의 최종 보드(대개 빈 판)를 따라가 방금 넣은 것을 지운다.
        /// </summary>
        private static void ShowBoard(SpinBoardView view, SpinBoard board)
        {
            if (view == null) return;
            view.DrivenExternally = true;
            view.ShowBoard(board);
        }

        /// <summary>
        /// 한 캐스케이드 단계의 정화를 표식·강조로 세운다. 재생을 거치지 않고 판을
        /// 직접 밀어 넣었을 때 `SpinPresenter`가 하던 일을 대신한다.
        /// </summary>
        private static int ShowPurifies(SpinBoardView board, CascadeStep step)
        {
            var markers = FindAnyObjectByType<PurifyMarkerView>();
            if (step.Purifies == null) return 0;

            // 강조를 1.0 으로 줬더니 9칸 중 8칸이 계조 없는 순백으로 날아가
            // 심볼도 정화 원인도 사라졌다. 연출에서 1.0 은 사인파가 **스치는**
            // 정점이라 눈이 잔상으로 형태를 유지하지만, 정지 화면에서는 그 값이
            // 계속 걸려 있다. 움직이는 연출의 최댓값을 정지 샷에 그대로 쓸 수 없다.
            //
            // 0.42 로 낮췄더니 형태는 살아났지만 이번엔 **사건 둘이 8칸을 같은 값으로
            // 칠했다.** 어느 칸이 어느 사건에 속하는지 갈리지 않으면 그 채널은 정보를
            // 나르지 않는다. 사건마다 밝기를 달리해 칸이 원인별로 묶이게 한다.
            // 막대(표식)가 "왜"를 말하고, 밝기 층이 "어느 것끼리"를 말한다.
            var levels = new[] { 0.46f, 0.22f, 0.34f, 0.16f };

            markers?.Begin();
            int count = 0;
            foreach (PurifyEvent purify in step.Purifies)
            {
                float level = levels[count % levels.Length];
                if (board != null && purify.Cells != null)
                    foreach (int cell in purify.Cells) board.SetHighlight(cell, level);
                markers?.Add(in purify, 1f);   // 막대는 밝아야 한다 — 이게 주 신호다
                count++;
            }
            markers?.End();
            return count;
        }

        /// <summary>열마다 한 종류. 통관–열 대응과 심볼 3종 구분을 한 장에 담는다.</summary>
        private static SpinBoard ColumnPerKind()
        {
            var board = default(SpinBoard);
            for (int row = 0; row < SpinBoard.Rows; row++)
            {
                board[0, row] = SymbolKind.NormalSoul;
                board[1, row] = SymbolKind.Absorber;
                board[2, row] = SymbolKind.Proliferator;
            }
            return board;
        }

        /// <summary>실제 추첨 결과 하나. 인위적 배열만으로는 "이렇게 나온다"를 못 보인다.</summary>
        private static SpinBoard DrawSample(int runSeed, int floor, int spinIndex)
        {
            FloorPlan plan = PrototypeCurriculum.For(floor);
            SpinRuleSet rules = PrototypeCurriculum.BuildRules(in plan);
            var engine = new SpinEngine(runSeed);
            ResistanceContract none = ResistanceContract.None;
            ResidualState residual = ResidualState.Empty;
            SpinResolution resolution = engine.SpinWithSeed(
                SpinSeed.Derive(runSeed, floor, spinIndex), rules, in none, in residual, floor, spinIndex);
            return resolution.InitialBoard;
        }

        /// <summary>과적 위에 과수확을 얹어 Critical 문턱을 넘긴다.</summary>
        private IEnumerator ForceCritical(RunSessionBehaviour run,
            RouletteInteractionBridge bridge, RiskStateView risk)
        {
            int guard = 0;
            while (guard++ < 8)
            {
                FloorSession floor = run.Session.Current;
                if (floor == null) break;
                if (risk != null && risk.Level >= RiskLevel.Critical) break;

                if (floor.Phase == FloorPhase.Boarding) run.FinishBoarding();
                if (floor.Phase == FloorPhase.ContractSelection) run.SelectContract(0);

                while (floor.Phase == FloorPhase.Spinning && floor.SpinsRemaining > 0)
                {
                    run.Spin();
                    yield return WaitWhileLocked(bridge);
                }
                if (floor.Phase == FloorPhase.Decision && floor.CanBank && floor.SpinsRemaining > 0)
                {
                    run.PushYourLuck();
                    run.Spin();
                    yield return WaitWhileLocked(bridge);
                }
                else break;
                yield return WaitSeconds(0.4f);
            }
            yield return WaitSeconds(2.5f);
        }

        private IEnumerator DriveToFloor(RunSessionBehaviour run,
            RouletteInteractionBridge bridge, int target)
        {
            int guard = 0;
            while (run.Session.CurrentFloor < target && !run.Session.IsComplete &&
                   !run.Session.IsFailed && guard++ < 60)
            {
                FloorSession floor = run.Session.Current;
                if (floor == null) break;
                if (floor.Plan.Floor >= target) break;

                if (floor.Phase == FloorPhase.Boarding) run.FinishBoarding();
                if (floor.Phase == FloorPhase.ContractSelection) run.SelectContract(0);
                while (floor.Phase == FloorPhase.Spinning && floor.SpinsRemaining > 0) run.Spin();
                if (floor.CanBank) run.Bank();
                else if (floor.SpinsRemaining == 0) run.ForceResolve();
                else break;
                yield return null;
            }
            yield return WaitWhileLocked(bridge);
        }

        private IEnumerator SpinUntilBankable(RunSessionBehaviour run, RouletteInteractionBridge bridge)
        {
            FloorSession floor = run.Session.Current;
            if (floor == null) yield break;
            if (floor.Phase == FloorPhase.Boarding) run.FinishBoarding();
            if (floor.Phase == FloorPhase.ContractSelection) run.SelectContract(0);

            int guard = 0;
            while (floor.Phase == FloorPhase.Spinning && floor.SpinsRemaining > 0 && guard++ < 12)
            {
                run.Spin();
                yield return WaitWhileLocked(bridge);
                if (floor.CanBank) break;
            }
        }

        /// <summary>연쇄가 깊게 터진 스핀을 찾아 그 재생 중에 찍는다.</summary>
        private IEnumerator CaptureDeepCascade(RunSessionBehaviour run,
            RouletteInteractionBridge bridge, RiskStateView risk)
        {
            var presenter = FindAnyObjectByType<SpinPresenter>();
            // 깊은 연쇄는 자연 발생이 드물다. 성능 측정이 1000스핀 평균 연쇄 **1.74**를
            // 보고했고, 1층·4층에서 시드 10개를 훑어 최대 4단계에 그쳤다.
            //
            // 그래서 **실제 빌드로 확률을 올린다.** 연출된 상황이 아니라 플레이어가 만들 수
            // 있는 판이어야 캡처가 증거가 된다:
            //   사선 결속기 — 대각 연결을 열어 4칸 덩어리가 훨씬 자주 성립한다
            //   연쇄 조속기 — 연쇄 배수 증분을 올린다
            //   증식체 계약 — 대상 저항의 출현률을 1.5배로
            // 셋 다 카탈로그와 커리큘럼에 실재하는 것이고, 8층은 FullPool + 계약 3종이다.
            int[] seeds = { 12, 7, 1, 99, 2024, 31415, 271828, 8675309, 42, 1234567, 20260731, 555 };
            int bestDepth = 0;

            foreach (int seed in seeds)
            {
                run.ResetRun(RunMode.TenFloor, seed);
                yield return WaitFrames(2);
                run.Session.Loadout.Add(BuildCatalog.ById("PRT_DIAGONAL_BINDER"));
                run.Session.Loadout.Add(BuildCatalog.ById("PRT_CASCADE_GOVERNOR"));
                yield return DriveToFloor(run, bridge, 8);
                FloorSession floor = run.Session.Current;
                if (floor == null) continue;
                if (floor.Phase == FloorPhase.Boarding) run.FinishBoarding();
                if (floor.Phase == FloorPhase.ContractSelection)
                    run.SelectContract(floor.Plan.ContractChoices.Length - 1);   // 증식체 계약

                int guard = 0;
                while (floor.Phase == FloorPhase.Spinning && floor.SpinsRemaining > 0 && guard++ < 8)
                {
                    SpinResolution resolution = run.Spin();
                    int depth = resolution.Steps != null ? resolution.Steps.Length : 0;
                    if (depth > bestDepth) bestDepth = depth;
                    if (depth >= 5)
                    {
                        // 연출이 끝나기를 기다렸다가 찍으면 판이 비어 있다 — 엔진이 수확·정화된
                        // 칸을 전부 비우기 때문이다. 첫 촬영이 "회색 상자 하나"로 나온 이유다.
                        //
                        // 그래서 **연쇄 중간 단계의 보드를 직접 밀어 넣는다.** 깊이의 절반쯤이
                        // 가장 판이 꽉 찬 시점이고, 그것이 "왜 연쇄가 이어졌는가"를 보여준다.
                        int step = Mathf.Clamp(depth / 2, 0, resolution.Steps.Length - 1);
                        var boardView = FindAnyObjectByType<SpinBoardView>();
                        ShowBoard(boardView, resolution.Steps[step].BoardBefore);

                        // 판만 밀어 넣으면 "심볼이 놓여 있다"까지밖에 안 보인다. 독립
                        // 평가자가 정확히 그걸 지적했다 — "연쇄를 시사하는 것이 하나도 없다.
                        // 이 그림만 보면 02_device_front 와 구별되는 건 심볼 배치뿐이다."
                        //
                        // 정화 표식은 `SpinPresenter`가 재생 중에 세운다. 판을 직접
                        // 밀어 넣으면 그 경로를 건너뛰므로 표식도 안 선다. 같은 단계의
                        // 정화 사건을 표식 뷰에 직접 먹인다 — 어느 칸이 왜 터졌는지가
                        // 선과 연결로 남는다(B-2.6). 강조는 맥동의 정점(1.0)으로 고정한다.
                        int purifies = ShowPurifies(boardView, resolution.Steps[step]);

                        yield return WaitFrames(3);
                        yield return Shot("15_cascade_deep", DeviceFront, risk,
                            $"시드 {seed} / 8층 / 사선 결속기+연쇄 조속기+증식체 계약 / " +
                            $"연쇄 {depth}단계 중 {step + 1}단계 진입 시점의 판 / " +
                            $"이 단계의 정화 {purifies}건이 표식으로 서 있다");

                        // **`UP-CORE-13` 은 HUD 가 증거의 대상이다.** 「한 화면에 모든 숫자를
                        // 띄우지 않는다」를 판정하려면 그 화면에 숫자가 보여야 하는데,
                        // 위 샷은 전용 카메라의 RenderTexture 렌더라 `ScreenSpaceOverlay`
                        // 캔버스가 통째로 빠진다. 같은 순간을 화면 캡처로 한 장 더 찍는다 —
                        // **판정 불가능한 증거를 걸어 두는 것이 미충족보다 나쁘다.**
                        yield return ScreenShot("19_cascade_deep_screen",
                            $"15번과 같은 순간의 **화면 캡처** — 연쇄 {depth}단계 / " +
                            "HUD 를 포함한다. `UP-CORE-13`(한 화면에 모든 숫자를 띄우지 않는다)은 " +
                            "이 장으로 판정한다. 해상도는 주장하지 않는다 — 위 줄의 실측값이 답이다");
                        yield break;
                    }
                    yield return WaitWhileLocked(bridge);
                }
            }

            yield return Shot("15_cascade_deep", DeviceFront, risk,
                $"5연쇄 이상을 찾지 못했다 — 시도한 시드 중 최대 {bestDepth}단계");
        }

        private IEnumerator CaptureCollapse(RunSessionBehaviour run, RouletteInteractionBridge bridge,
            RiskStateView risk, AccidentRecorder recorder)
        {
            // 사고를 만든다. **몇 층은 올라간 뒤에** 무너져야 한다 —
            // 처음에는 1층에서 곧바로 220kg 을 실어 그 층에서 죽었고, 사고 기록기 캡처가
            // "기록 1건 / 도달 0층"이 됐다. 아무 일도 없었던 런의 기록은 기록기가 무엇을
            // 설명할 수 있는지 보여주지 못한다.
            //
            // 4층까지 정상 진행한 뒤 과적을 걸어 요구 전력을 감당 못 하게 만든다.
            // 실제 플레이에서 "욕심내서 싣다가 무너지는" 경로와 같은 모양이다.
            // 시드 하나에 고정하면 안 된다. 이 자리는 원래 `555555`를 4층까지 몰고
            // 220kg 을 얹어 사고를 내는 코드였는데, 커리큘럼 재배치와 건너뛰기 금지
            // (D-20260801-01·02) 이후 그 시드는 거기서 죽지 않았다. 그래서 파일 이름은
            // `16_risk_collapse` 인데 매니페스트에는 **"실제 단계 Strain / 실패 False"**
            // 가 적힌 채로 나갔고, 독립 평가자가 "네 번째 상태가 세트에 아예 없다"고 잡았다.
            //
            // 캡처가 자기 파일 이름을 못 지키면 그 세트 전체의 신뢰가 무너진다.
            // 그래서 여러 시드·여러 층·여러 중량으로 **실제로 Collapse 에 닿을 때까지** 찾고,
            // 못 찾으면 아래에서 그 사실을 매니페스트에 그대로 적는다. 이름을 지키는 것보다
            // 못 지켰다고 말하는 것이 낫다.
            bool reachedCollapse = false;
            int usedSeed = 555555, usedFloor = 4;
            float usedWeight = 220f;

            foreach (int seed in new[] { 555555, 1337, 8675309, 31415, 90210, 20260731, 4242, 7 })
            {
                foreach (int targetFloor in new[] { 4, 6, 8 })
                {
                    foreach (float extra in new[] { 220f, 320f, 460f })
                    {
                        run.ResetRun(RunMode.TenFloor, seed);
                        yield return WaitFrames(2);
                        yield return DriveToFloor(run, bridge, targetFloor);
                        yield return WaitFrames(2);
                        run.Session.AddWeight(extra);

                        int guard = 0;
                        while (!run.Session.IsFailed && !run.Session.IsComplete && guard++ < 40)
                        {
                            FloorSession floor = run.Session.Current;
                            if (floor == null) break;
                            if (floor.Phase == FloorPhase.Boarding) run.FinishBoarding();
                            if (floor.Phase == FloorPhase.ContractSelection) run.SelectContract(0);
                            while (floor.Phase == FloorPhase.Spinning && floor.SpinsRemaining > 0) run.Spin();
                            if (floor.SpinsRemaining == 0 && !floor.CanBank) { run.ForceResolve(); break; }
                            if (floor.CanBank) run.Bank();
                            else break;
                            yield return null;
                        }

                        // 위험 뷰가 붕괴로 수렴할 시간을 준 뒤에 판정한다. 블렌딩(2.2/초)이
                        // 끝나기 전에 읽으면 도달했는데 아니라고 판정한다.
                        yield return WaitSeconds(1.6f);
                        if (risk != null && risk.Level >= RiskLevel.Collapse)
                        {
                            reachedCollapse = true;
                            usedSeed = seed; usedFloor = targetFloor; usedWeight = extra;
                        }
                        if (reachedCollapse) break;
                    }
                    if (reachedCollapse) break;
                }
                if (reachedCollapse) break;
            }

            if (!reachedCollapse)
                Debug.LogWarning("[상승] 16_risk_collapse — 어떤 조합으로도 Collapse 에 닿지 못했다. " +
                                 "매니페스트에 그대로 적는다.");

            // 붕괴에서도 결과가 읽혀야 한다 — `VISUAL_SPEC` §6이 "단순한 암전이나
            // 즉사 연출로 정보를 숨기지 않는다"고 요구한다. 그런데 사고 런은 판을
            // 비운 상태로 끝나서 "무엇이 터졌는지 알 수 없다"는 지적을 받았다.
            ShowBoard(FindAnyObjectByType<SpinBoardView>(), DrawSample(usedSeed, 2, usedFloor));
            yield return WaitSeconds(2.5f);
            yield return Shot("16_risk_collapse", Risk, risk,
                (reachedCollapse
                    ? $"Collapse 도달 — 시드 {usedSeed} / {usedFloor}층 / 추가 중량 {usedWeight:F0}kg. "
                    : "**Collapse 미도달** — 시드 8개 × 층 3개 × 중량 3종을 전부 시도했으나 " +
                      "붕괴 상태를 만들지 못했다. 이 장은 파일 이름이 주장하는 상태가 아니다. ") +
                $"실제 단계 {LevelName(risk)} / 실패 {run.Session.IsFailed} " +
                $"사유 {run.Session.FailureReason ?? "—"} / 06·09·10 과 같은 좌표");

            // 사고 기록기는 `GameHudView`가 화면에 그린다. 그런데 이 리그의 다른 샷은
            // 전용 카메라의 RenderTexture 렌더라 **화면 UI가 들어가지 않는다** — 그래서
            // 첫 촬영에서 17이 16과 픽셀 단위로 같은 그림이 됐다. 독립 평가자가
            // "사고 기록기라는 물체도, 기록 1건이라는 표시도 화면에 없다"고 지적했다.
            //
            // 이 한 장만 화면 캡처로 찍는다. 해상도가 게임 뷰에 종속되므로
            // 고정 비교 세트가 아니라는 것을 매니페스트에 남긴다.
            string record = recorder != null && recorder.Latest != null
                ? "사고 기록 있음" : "사고 기록 없음";
            yield return ScreenShot("17_accident_recorder",
                $"{record} / 기록 {(recorder != null ? recorder.Records.Count : 0)}건 / " +
                $"시드 {run.Session.Seed} / 도달 {run.Session.HighestFloorReached}층 / " +
                "**화면 캡처** — 전용 카메라 렌더에는 화면 UI 가 들어가지 않으므로 게임 뷰를 그대로 찍는다. " +
                "여기에는 「이 한 장만 방식이 다르다 / 나머지 18장」이라고 적혀 있었다. **둘 다 틀렸다** — " +
                "같은 경로를 쓰는 장이 여럿이고 세트는 18장이 아니다. 장수도 해상도도 이제 " +
                "**세고 재서** 적는다(위 줄과 파일 끝)");

            // 완주 직전 — 10층에 **실제로 서 있는** 런을 찾는다. 시드 하나로 몰다가
            // 중간에 사고가 나면 "도달 8층"이 찍히고, 그건 §12가 요구한 그림이 아니다.
            FloorSession last = null;
            int finalSeed = 0;
            foreach (int seed in new[] { 1337, 4242, 90210, 7, 31415, 271828, 8675309, 20260731 })
            {
                run.ResetRun(RunMode.TenFloor, seed);
                yield return WaitFrames(2);
                yield return DriveToFloor(run, bridge, 10);
                yield return WaitFrames(3);
                FloorSession candidate = run.Session.Current;
                if (candidate != null && candidate.Plan.Floor == 10)
                {
                    last = candidate;
                    finalSeed = seed;
                    break;
                }
            }
            // 직전 샷이 Collapse 라 위험 연출이 붉게 물들어 있다. 정착 시간을 안 주면
            // "위험도 안정"인데 램프가 빨간 모순된 그림이 나온다 — 지적받은 그대로다.
            ShowBoard(FindAnyObjectByType<SpinBoardView>(), DrawSample(4242, 10, 0));
            yield return WaitSeconds(2.5f);
            yield return Shot("18_final_floor", Risk, risk,
                last != null
                    ? $"시드 {finalSeed} / 10층 도달 — 요구 {last.RequiredPower:F0} / 완주 직전"
                    : $"10층에 선 런을 찾지 못했다 — 마지막 도달 {run.Session.HighestFloorReached}층");
        }

        // ── 촬영 ────────────────────────────────────────────────────────────

        private void SetupCamera()
        {
            Camera source = Camera.main;
            var go = new GameObject("CaptureCamera");
            go.transform.SetParent(transform, false);
            _camera = go.AddComponent<Camera>();

            if (source != null)
            {
                _camera.clearFlags = source.clearFlags;
                _camera.backgroundColor = source.backgroundColor;
                _camera.cullingMask = source.cullingMask;
                _camera.nearClipPlane = source.nearClipPlane;
                _camera.farClipPlane = source.farClipPlane;
            }
            // ── 포스트 처리를 **이 카메라에도** 건다 ──────────────────────────
            //
            // 이 카메라는 `Camera.main` 의 복사본이 아니라 **새로 만든 것**이고, 위에서
            // 옮겨 적는 것은 clearFlags·배경색·컬링·클립 넷뿐이다. URP 의 포스트 설정은
            // `Camera` 가 아니라 `UniversalAdditionalCameraData` 라는 **다른 컴포넌트**에
            // 있어서 그 넷에 딸려 오지 않는다. 새 카메라의 기본값은 `renderPostProcessing
            // = false` 다.
            //
            // 그래서 씬 카메라에 포스트를 켜도 **이 경로로 찍는 스무 장에는 한 픽셀도
            // 반영되지 않는다.** 화면 캡처(`ScreenShot`) 넉 장만 달라져서, 같은 세트 안에서
            // 네 장과 스무 장이 서로 다른 렌더 파이프를 통과한 그림이 된다 —
            // 그 상태로 비교하면 "포스트가 좋아졌나"를 판정할 수 없다.
            var srcData = source != null ? source.GetUniversalAdditionalCameraData() : null;

            // 진단 세트는 **화면 경로도 함께** 꺼야 한다. 전용 카메라만 끄면 같은 세트
            // 안에서 스무 장은 포스트가 없고 넉 장은 있는 그림이 나온다 — 그 상태의
            // G-1·G-4 는 두 파이프의 평균이라 어느 쪽도 판정하지 못한다.
            bool wantPost = srcData == null || srcData.renderPostProcessing;
#if UNITY_EDITOR
            if (PostDisabledForRun)
            {
                wantPost = false;
                if (srcData != null) srcData.renderPostProcessing = false;
            }
#endif

            var camData = _camera.GetUniversalAdditionalCameraData();
            if (camData != null)
            {
                camData.renderPostProcessing = wantPost;
                camData.antialiasing         = srcData != null ? srcData.antialiasing : AntialiasingMode.None;
                camData.antialiasingQuality  = srcData != null ? srcData.antialiasingQuality : AntialiasingQuality.High;
                camData.dithering            = srcData == null || srcData.dithering;
                camData.renderShadows        = srcData == null || srcData.renderShadows;
                camData.volumeLayerMask      = srcData != null ? srcData.volumeLayerMask : camData.volumeLayerMask;
            }
            _camera.allowHDR = source == null || source.allowHDR;

            _camera.fieldOfView = Fov;
            // 화면비를 **못박는다.** RenderTexture 가 1920×1080 이므로 자동값도 같지만,
            // 매니페스트의 프레임 실측이 `WorldToViewportPoint` 로 화면 위치를 계산하는데
            // 그 값이 `Camera.aspect` 에 걸려 있다. 자동 유추에 맡기면 렌더는 RT 를 따르고
            // 계산은 게임 뷰를 따르는 상황이 생길 수 있다 — 두 경로가 같은 수를 보게 한다.
            _camera.aspect = (float)Width / Height;
            _camera.enabled = false;

            _target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 1,
                name = "TenFloorCapture",
            };
            _readback = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            _camera.targetTexture = _target;
        }

        /// <param name="extra">
        /// 이 장에만 필요한 **추가 실측**. 문자열이 아니라 대리자를 받는 이유는
        /// 셔터가 열린 뒤 카메라가 그 자리에 있을 때 계산돼야 하기 때문이다 —
        /// 호출부에서 문자열로 만들면 카메라가 아직 이전 장의 자리에 있다.
        /// </param>
        private IEnumerator Shot(string name, Pose pose, RiskStateView risk, string note,
                                 Func<string> extra = null)
        {
            _camera.transform.position = pose.Position;
            _camera.transform.LookAt(pose.LookAt);
            _camera.enabled = true;

            yield return null;
            yield return new WaitForEndOfFrame();

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = _target;
            _readback.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            _readback.Apply(false);
            RenderTexture.active = previous;
            _camera.enabled = false;

            WritePng(name, _readback.EncodeToPNG());
            _renderShots++;

            // 실측 기준을 전용 카메라로 못박는다. 게임 뷰 화면 캡처와 화각이 다르므로
            // 이 한 줄을 빠뜨리면 매니페스트가 스스로 다른 장의 화각으로 이 장을 잰다.
            _measureCamera = _camera;

            _manifest.AppendLine($"{name,-26} 시점 {pose.Name,-12} pos {pose.Position:F2} m look {pose.LookAt:F2} m  " +
                                 $"FOV {Fov:F0}° · {Width}×{Height} px  " +
                                 $"위험 {(risk != null ? risk.Level.DisplayName() : "—")}");
            _manifest.AppendLine($"{"",-26} {note}");
            _manifest.AppendLine($"{"",-26} {GaugeFill()}");
            _manifest.AppendLine($"{"",-26} {FrameFacts()}");
            RecordBoardRoi(name, Width, Height);
            if (extra != null) _manifest.AppendLine($"{"",-26} {extra()}");
        }

        /// <summary>파일 하나를 쓰고 **이번 런이 쓴 것으로 기록한다.** 끝에서 폴더와 대조한다.</summary>
        private void WritePng(string name, byte[] png)
        {
            string directory = Path.Combine(Directory.GetCurrentDirectory(), OutputDirectory);
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(Path.Combine(directory, $"{name}.png"), png);
            _written.Add($"{name}.png");
            _shots++;
        }

        // ── 프레임 실측 ──────────────────────────────────────────────────────
        //
        // **매니페스트가 그림이 보이지 않는 것을 주장하면 그 세트는 증거가 아니라 목록이다**
        // (2회차 독립 판정). 이 세트는 실제로 「주장과 그림이 다른 장 9건」으로 반려됐고
        // (`UP-FIX-13`), 그중 하나는 **재보지도 않은 원인**(「문틀에 가림」)을 네 라운드 동안
        // 사실처럼 실어 날랐다.
        //
        // 그래서 아래 값은 전부 **셔터가 열린 그 순간의 카메라 행렬로 계산한 것**이다.
        // 사람이 쓴 주장이 아니고, 세트가 커져도 따라온다.
        //
        // **8차 판정이 이 절을 다시 열었다.** 열한 장(`06`·`08`·`09`·`10`·`11`·`12`·`13`·
        // `16`·`18`·`22`·`24`)에서 「전력」 두 글자가 반투명 통관에 먹혀 「…력 1616」으로
        // 남는데, **매니페스트는 그 열한 장을 전부 「온전 3 · 잘림 0」으로 적었다.**
        // 스스로 「가림은 재지 않았다」고 고지해 두고 초록불을 준 것이다 —
        // **측정하지 않은 항목에 대한 초록불이다.**
        //
        // 그래서 이제 **가림을 실제로 잰다.** 방식은 콜라이더가 아니다. 직전 판본이
        // 스스로 「콜라이더 유무를 확인하지 않아 거짓 그린이 날 수 있다」고 적었고 그
        // 고지가 옳았다 — 콜라이더는 **있을 수도 없을 수도** 있는 부속이다. 대신
        // **렌더러의 월드 AABB**(`Renderer.bounds`)를 모아 카메라 → 글자 선분과
        // 교차시킨다. 렌더러는 「그려지는 것」의 정의 그 자체라 「부속이 안 붙어서
        // 못 봤다」가 원리적으로 생기지 않고, 반투명이어도 잡힌다(글자를 흐리면 가림이다).
        //
        // **그래서 「온전」의 정의가 바뀌었다 — 프레임 안 *그리고* 가리는 것이 없음.**
        // 예전 정의(프레임 포함 여부)로 초록을 적던 자리가 여기서 갈린다.
        // 그리고 **재지 못한 축은 「온전」에 넣지 않는다** — 아래 「가림 계측 한계」줄이
        // 무엇을 못 쟀는지 이름과 개수로 적고, 그 항목에 걸린 줄은 `가림?` 으로 남는다.
        //
        // **단위 규칙**(8차 판정 §7-5 의 단위 혼동을 막는다):
        //   `ndc` = 정규화 좌표 −1(아래·왼끝) … +1(위·오른끝). 화면 전체가 **2.0 ndc** 다.
        //   `%`   = 프레임 비율. ndc 길이를 % 로 옮길 때는 <see cref="NdcSpanToFramePercent"/>
        //           **하나만** 쓴다(×50). 「0.58」을 「58%」로 읽은 것이 8차의 오독이었다 —
        //           0.58 ndc 는 프레임의 **29%** 다.
        //   그래서 이 파일이 내보내는 모든 수에는 단위가 붙는다. 붙지 않은 수는 버그다.

        /// <summary>
        /// 지금 실측이 기준으로 삼는 카메라. 전용 카메라 경로는 <see cref="_camera"/>(1920×1080 ·
        /// 화면비 못박음), 게임 뷰 화면 캡처 경로는 플레이어 카메라다. **두 경로의 화각이
        /// 다르므로 섞으면 안 된다** — 섞으면 「다른 장의 숫자를 이 장의 성과로 옮겨 적는」
        /// 8차 §7-1 과 같은 오류가 매니페스트 안에서 자동으로 일어난다.
        /// </summary>
        private Camera _measureCamera;
        private Camera MeasureCamera => _measureCamera != null ? _measureCamera : _camera;

        /// <summary>월드 한 점이 지금 카메라의 화각 안인가.</summary>
        private bool InFrame(Vector3 world)
        {
            Camera cam = MeasureCamera;
            if (cam == null) return false;
            Vector3 v = cam.WorldToViewportPoint(world);
            return v.z > 0f && v.x >= 0f && v.x <= 1f && v.y >= 0f && v.y <= 1f;
        }

        /// <summary>
        /// 정규화 세로 위치 **ndc**. −1 아래끝 · 0 한가운데 · +1 위끝. 뒤쪽이면 NaN.
        /// **이 값은 ndc 이지 프레임 비율(%)이 아니다** — 둘을 한 문장에 섞지 않는다.
        /// </summary>
        private float ScreenY(Vector3 world)
        {
            Camera cam = MeasureCamera;
            if (cam == null) return float.NaN;
            Vector3 v = cam.WorldToViewportPoint(world);
            return v.z > 0f ? (v.y - 0.5f) * 2f : float.NaN;
        }

        /// <summary>
        /// **ndc 길이 → 프레임 비율(%).** 화면 전체가 2.0 ndc 이므로 ×50 이다.
        /// ndc 좌표(위치)를 그대로 넣으면 안 된다 — 넣는 것은 **길이**다.
        /// 이 변환을 손으로 하지 않는 것이 이 함수가 존재하는 이유다.
        /// </summary>
        private static float NdcSpanToFramePercent(float ndcSpan) => ndcSpan * 50f;

        /// <summary>
        /// 이 장의 **결과판 화면공간 사각형**을 `board-roi.csv` 에 적는다.
        ///
        /// 이전 판은 칸마다 <c>InFrame(cell.position)</c> 으로 **한 점만** 검사하고
        /// 투영 좌표를 버렸다. 그래서 매니페스트에 「9칸 중 프레임 안 N칸」이라는
        /// **개수만** 남고 사각형은 어디에도 없었다 — 점 하나에는 넓이가 없다.
        /// 그 결과 `G-SLOT-A` 가 24/24 「측정 불가」로 앉아 게이트를 막았다.
        ///
        /// 여기서는 칸 렌더러의 **월드 AABB 8꼭짓점을 투영**해 x·y 의 min/max 를
        /// 취하고 아홉 칸을 합집합한다.
        ///
        /// ⚠ **원점은 좌하단이다.** Unity 의 <c>WorldToScreenPoint</c> 가 좌하단
        /// 기준이고 PNG 행은 좌상단 기준이라 서로 뒤집혀 있다. 도구는
        /// `-BoardRoiOrigin bottomleft` 로 받아야 하며, CSV 머리말에도 같은 사실을
        /// 적는다 — 도구는 어느 쪽을 썼는지 찍어 주지만 **틀린 것을 감지하지는 못한다.**
        /// </summary>
        private void RecordBoardRoi(string name, int pixelWidth, int pixelHeight)
        {
            Camera cam = MeasureCamera;
            var board = FindAnyObjectByType<SpinBoardView>();
            if (cam == null || board == null) { _roiUnmeasurable++; return; }

            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            int contributing = 0, centresInFrame = 0;

            for (int i = 0; i < SpinBoard.Cells; i++)
            {
                Transform cell = board.CellTransform(i);
                if (cell == null) continue;
                if (InFrame(cell.position)) centresInFrame++;

                // 빈 칸도 판의 일부다. 심볼이 꺼져 있어도 자리는 그 자리이므로
                // 비활성 렌더러까지 포함해 **판의 물리적 넓이**를 잡는다. 그래야
                // 회귀 ROI 가 심볼 유무에 따라 흔들리지 않는다.
                var renderers = cell.GetComponentsInChildren<Renderer>(true);
                bool any = false;
                for (int r = 0; r < renderers.Length; r++)
                {
                    if (renderers[r] == null) continue;
                    Bounds b = renderers[r].bounds;
                    if (ProjectBounds(cam, b, ref minX, ref minY, ref maxX, ref maxY)) any = true;
                }
                if (any) contributing++;
            }

            // 프레임 안 칸이 0개인 장은 행을 만들지 않는다 — 그 장은 「측정 불가」가 정답이다.
            if (centresInFrame == 0 || contributing == 0)
            {
                _roiUnmeasurable++;
                return;
            }

            // 뷰포트(0~1) → 이 장의 PNG 픽셀. 좌하단 원점 그대로다.
            float x0 = Mathf.Clamp(minX * pixelWidth, 0f, pixelWidth);
            float y0 = Mathf.Clamp(minY * pixelHeight, 0f, pixelHeight);
            float x1 = Mathf.Clamp(maxX * pixelWidth, 0f, pixelWidth);
            float y1 = Mathf.Clamp(maxY * pixelHeight, 0f, pixelHeight);
            int w = Mathf.RoundToInt(x1 - x0);
            int h = Mathf.RoundToInt(y1 - y0);
            if (w <= 0 || h <= 0) { _roiUnmeasurable++; return; }

            _boardRoi.AppendLine($"{name},{Mathf.RoundToInt(x0)},{Mathf.RoundToInt(y0)},{w},{h},bottomleft");
            _roiRows++;
        }

        /// <summary>
        /// 월드 AABB 의 **8꼭짓점**을 화면으로 투영해 min/max 를 넓힌다.
        /// 카메라 뒤쪽 꼭짓점(z ≤ 0)은 투영이 뒤집히므로 버린다.
        /// 하나라도 앞쪽이면 true.
        /// </summary>
        private static bool ProjectBounds(Camera cam, Bounds b,
                                          ref float minX, ref float minY, ref float maxX, ref float maxY)
        {
            Vector3 c = b.center, e = b.extents;
            bool any = false;
            for (int corner = 0; corner < 8; corner++)
            {
                var world = new Vector3(
                    c.x + ((corner & 1) == 0 ? -e.x : e.x),
                    c.y + ((corner & 2) == 0 ? -e.y : e.y),
                    c.z + ((corner & 4) == 0 ? -e.z : e.z));
                // **뷰포트(0~1)로 받는다.** 화면 캡처 경로는 카메라 픽셀 공간과 PNG
                // 크기가 다를 수 있어 `WorldToScreenPoint` 의 픽셀 값을 그대로 쓰면
                // 배율이 어긋난다. 정규화해 두고 호출부에서 PNG 크기를 곱한다.
                Vector3 s = cam.WorldToViewportPoint(world);
                if (s.z <= 0f) continue;              // 뒤쪽 — 투영이 뒤집힌다
                if (s.x < minX) minX = s.x;
                if (s.y < minY) minY = s.y;
                if (s.x > maxX) maxX = s.x;
                if (s.y > maxY) maxY = s.y;
                any = true;
            }
            return any;
        }

        /// <summary>매니페스트 여러 줄을 계기 줄 들여쓰기에 맞춘다.</summary>
        private const string FactIndent = "                           ";

        /// <summary>월드 AABB 여덟 꼭짓점 중 프레임 안인 개수. 8이면 온전, 0이면 밖.</summary>
        private int CornersInFrame(Bounds bounds)
        {
            int inside = 0;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(
                    (i & 1) == 0 ? bounds.min.x : bounds.max.x,
                    (i & 2) == 0 ? bounds.min.y : bounds.max.y,
                    (i & 4) == 0 ? bounds.min.z : bounds.max.z);
                if (InFrame(corner)) inside++;
            }
            return inside;
        }

        // ── 가림 계측 ────────────────────────────────────────────────────────

        /// <summary>이번 셔터에서 가림 후보로 쓸 렌더러와 그 월드 AABB. 매 장 새로 모은다.</summary>
        private readonly List<Renderer> _solidOccluders = new List<Renderer>();
        private readonly List<Bounds> _solidBounds = new List<Bounds>();

        /// <summary>
        /// 입자·선·궤적 렌더러. AABB 가 실제 그려지는 면적의 성긴 대리값이라 **확정으로 세지
        /// 않는다** — 그러나 후보에서 빼 버리면 「재지 않은 것을 초록으로 적는」 8차의 실패를
        /// 그대로 반복하게 된다. 그래서 별도로 세고 `가림?` 으로 남긴다.
        /// </summary>
        private readonly List<Renderer> _softOccluders = new List<Renderer>();
        private readonly List<Bounds> _softBounds = new List<Bounds>();

        /// <summary>계측에서 제외한 TMP 렌더러 수. 글자가 글자를 가린다고 세면 자기 자신이 걸린다.</summary>
        private int _skippedTextRenderers;

        /// <summary>AABB 가 글자를 **품어 버려** 판정할 수 없었던 렌더러 이름. 초록으로 적지 않는다.</summary>
        private readonly HashSet<string> _containingBounds = new HashSet<string>();

        /// <summary>
        /// **한 렌더러가 같은 줄에서 「판정 보류」이면서 「가림」일 수 없다.**
        ///
        /// 9차 판정이 잡은 거짓 레드 22건이 정확히 이 자기모순이었다 — 매니페스트가
        /// `PowerLabel` 을 「가림 100% ← `PanelBack`」으로 적어 놓고, 같은 출력의 한계 ④에
        /// `PanelBack` 을 「AABB 가 글자를 품어 판정 보류」 목록에 올려 뒀다. 그림에서는
        /// 라벨이 프레임 안인 14장 전부 전문 판독됐다.
        ///
        /// 원인은 표본점마다 `Contains` 를 따로 물은 것이다. 기울어진 얇은 판의 AABB 는
        /// 뚱뚱해서 같은 줄의 글자 하나는 품고 옆 글자는 스치기만 한다 — 그러면 앞 글자는
        /// 보류, 뒷 글자는 가림이 된다. 물리적으로 같은 물체인데 판정이 갈린다.
        ///
        /// 그래서 줄을 재기 **전에** 한 번 훑어 「이 줄의 글자를 하나라도 품는 렌더러」를
        /// 모으고, 본 계측에서는 그것들을 후보에서 뺀다. 빼는 방향이 「가림 → 보류」이지
        /// 「가림 → 온전」이 아니라는 점이 중요하다 — 보류는 이름과 개수가 한계 ④에 남는다.
        /// 초록불을 늘리는 완화가 아니라, **재지 못한 것을 재지 못했다고 적는 것**이다.
        /// </summary>
        private readonly HashSet<Renderer> _deferredForLabel = new HashSet<Renderer>();

        /// <summary>글자 한 칸에서 뽑는 표본 위치(0..1 사각형 안). 가로 절단과 세로 절단을 함께 잡는다.</summary>
        private static readonly Vector2[] CharSamples =
        {
            new Vector2(0.50f, 0.50f),
            new Vector2(0.22f, 0.50f),
            new Vector2(0.78f, 0.50f),
            new Vector2(0.50f, 0.24f),
            new Vector2(0.50f, 0.76f),
        };

        /// <summary>
        /// 씬의 렌더러를 가림 후보로 모은다. **콜라이더를 보지 않는다** — 콜라이더는 있을
        /// 수도 없을 수도 있는 부속이고, 그 불확실성이 직전 판본이 스스로 경고한
        /// 「거짓 그린」의 원인이었다. 렌더러는 그려지는 것의 정의 그 자체다.
        /// </summary>
        private void CollectOccluders()
        {
            _solidOccluders.Clear(); _solidBounds.Clear();
            _softOccluders.Clear();  _softBounds.Clear();
            _containingBounds.Clear();
            _skippedTextRenderers = 0;

            var solid = new List<Renderer>(256);
            var soft = new List<Renderer>(16);
            foreach (Renderer renderer in FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
            {
                if (renderer == null || !renderer.enabled) continue;
                if (!renderer.gameObject.activeInHierarchy) continue;
                if (renderer.GetComponent<TMPro.TMP_Text>() != null) { _skippedTextRenderers++; continue; }

                if (renderer is ParticleSystemRenderer || renderer is LineRenderer || renderer is TrailRenderer)
                    soft.Add(renderer);
                else
                    solid.Add(renderer);
            }

            // **순서를 못박는다.** `FindObjectsByType` 는 순서를 보장하지 않는다. 순서가 흔들리면
            // 겹친 후보 중 어느 이름이 보고되는지가 런마다 달라지고, 그러면 매니페스트가
            // 같은 씬·같은 시드에서 다른 문장을 낸다 — 고정 캡처 세트의 전제가 무너진다.
            solid.Sort(ByNameThenPosition);
            soft.Sort(ByNameThenPosition);

            // **월드 AABB 가 아니라 렌더러 자기 좌표계의 상자를 쓴다.**
            //
            // 9차 판정이 잡은 거짓 레드 22건의 원인이 이것이다. `PowerLabel` 을 24장 중
            // 22장에서 「가림 100% ← `PanelBack`」으로 적었는데, 라벨이 프레임 안인 14장은
            // 그림에서 전부 전문 판독된다. `PanelBack` 은 계기판의 **기울어진 얇은 배면**이고,
            // 기울어진 판의 월드 AABB 는 판 앞쪽 허공까지 삼키는 뚱뚱한 상자가 된다.
            // 그러면 카메라→글자 선분이 「판 앞의 빈 공간」에서 상자에 들어가고, 실제로는
            // 글자 **뒤에** 있는 판이 글자를 가린다고 보고된다.
            //
            // 직전 판본은 이 한계를 스스로 「①AABB 는 회전·오목 형상에서 과대평가라
            // 가림을 실제보다 많이 셀 수 있다(거짓 레드 쪽이다)」로 고지해 두고, 같은 출력에서
            // 그 과대평가를 **판정으로 썼다.** 고지는 면죄가 아니다.
            //
            // 로컬 상자 + 역변환한 선분으로 재면 회전이 상쇄되어 얇은 판이 얇게 남는다.
            // 척도가 아니라 **좌표계**를 고친 것이므로 가림을 덜 세는 완화가 아니다 —
            // 앞을 막는 물체는 로컬에서도 똑같이 막는다.
            foreach (Renderer renderer in solid) { _solidOccluders.Add(renderer); _solidBounds.Add(LocalBox(renderer)); }
            foreach (Renderer renderer in soft)  { _softOccluders.Add(renderer);  _softBounds.Add(LocalBox(renderer)); }
        }

        /// <summary>
        /// 렌더러 자기 좌표계의 상자. `Renderer.localBounds` 가 곧 그것이고, 없으면(입자 등)
        /// 월드 AABB 를 로컬로 되돌려 쓴다 — 그 경우엔 과대평가가 남지만 이름과 함께 남는다.
        /// </summary>
        private static Bounds LocalBox(Renderer renderer)
        {
            Bounds local = renderer.localBounds;
            if (local.size.sqrMagnitude > 0f) return local;
            Bounds world = renderer.bounds;
            return new Bounds(renderer.transform.InverseTransformPoint(world.center),
                              renderer.transform.InverseTransformVector(world.size));
        }

        /// <summary>이름 → 위치 순. 같은 이름이 여럿인 껍데기(`TubeFrame` 셋)까지 갈린다.</summary>
        private static int ByNameThenPosition(Renderer a, Renderer b)
        {
            int byName = string.CompareOrdinal(a.gameObject.name, b.gameObject.name);
            if (byName != 0) return byName;
            Vector3 pa = a.bounds.center, pb = b.bounds.center;
            if (pa.x != pb.x) return pa.x < pb.x ? -1 : 1;
            if (pa.y != pb.y) return pa.y < pb.y ? -1 : 1;
            if (pa.z != pb.z) return pa.z < pb.z ? -1 : 1;
            return 0;
        }

        /// <summary>
        /// 선분(origin→target)이 AABB 안으로 **들어가는 지점**. 슬랩 방식.
        /// 대상이 그 안에 있으면 가리는 게 아니라 품고 있는 것이므로 호출부가 먼저 거른다.
        /// </summary>
        private static bool SegmentEntersBounds(Vector3 origin, Vector3 target, in Bounds bounds, out float entry)
        {
            entry = 0f;
            Vector3 direction = target - origin;
            float enter = 0f, exit = 1f;
            for (int axis = 0; axis < 3; axis++)
            {
                float o = origin[axis], d = direction[axis];
                float low = bounds.min[axis], high = bounds.max[axis];
                if (Mathf.Abs(d) < 1e-8f)
                {
                    if (o < low || o > high) return false;
                    continue;
                }
                float t1 = (low - o) / d, t2 = (high - o) / d;
                if (t1 > t2) { float swap = t1; t1 = t2; t2 = swap; }
                if (t1 > enter) enter = t1;
                if (t2 < exit) exit = t2;
                if (enter > exit) return false;
            }
            // 카메라가 그 안에 있거나(enter≈0) 대상 뒤에서 시작하는 것(enter≈1)은 가림이 아니다.
            entry = enter;
            return enter > 0.001f && enter < 0.999f;
        }

        /// <summary>
        /// 선분을 막는 렌더러 중 **카메라에 가장 가까운 것.** 없으면 null.
        /// 첫 발견이 아니라 최근접인 이유는 둘이다 — ①플레이어가 실제로 보는 것이 최근접이다
        /// ②후보 순서가 결과를 바꾸지 않아 같은 씬이면 같은 문장이 나온다.
        /// 대상을 품는 AABB 는 가림이 아니므로 이름만 남기고 통과시킨다.
        /// </summary>
        private Renderer Blocker(Vector3 origin, Vector3 target,
                                 List<Renderer> pool, List<Bounds> bounds, bool recordContaining)
        {
            Renderer nearest = null;
            float nearestEntry = float.MaxValue;
            for (int i = 0; i < pool.Count; i++)
            {
                Renderer renderer = pool[i];
                if (renderer == null) continue;
                Bounds box = bounds[i];
                if (_deferredForLabel.Contains(renderer)) continue;
                Transform frame = renderer.transform;
                Vector3 localTarget = frame.InverseTransformPoint(target);
                if (box.Contains(localTarget))
                {
                    if (recordContaining) _containingBounds.Add(renderer.gameObject.name);
                    continue;
                }
                Vector3 localOrigin = frame.InverseTransformPoint(origin);
                if (!SegmentEntersBounds(localOrigin, localTarget, in box, out float entry)) continue;
                if (entry >= nearestEntry) continue;
                nearestEntry = entry;
                nearest = renderer;
            }
            return nearest;
        }

        /// <summary>
        /// 셔터 순간의 프레임 내용을 잰다 — 결과판·계기 글자줄·게이지.
        ///
        /// 계기면의 **겉보기 폭 계수**를 함께 적는다. 이것이 `B-5 #15` 의 실제 축이다:
        /// 카메라에서 라벨로 가는 광선과 라벨 면 법선의 사잇각이 0에 가까울수록 글자가
        /// 정면으로 보이고, 90°에 가까울수록 세로 파편으로 눌린다. 챔퍼 이전 실측이
        /// 0.327(70.9°)이었으므로 이 수 하나로 배치 변경이 들었는지 아닌지가 갈린다.
        /// **이 계수는 이 장의 값이다** — 8차 §7-1 이 잡은 오류가 정확히 이것을 다른 장의
        /// 값으로 바꿔 적은 것이었으므로, 출력에 「이 장」이라고 못박는다.
        /// </summary>
        /// <summary>
        /// **위험 단계를 실제로 나르는 값 둘.** `RiskStateView` 는 `RenderSettings.ambientLight`
        /// (`RiskAmbientLadder` 사다리)와 캐빈 광원 세기를 움직인다. 벽이 위험 단계마다
        /// 달라 보이는가는 전적으로 이 둘에 달려 있다.
        ///
        /// 이걸 적는 이유는 하나다 — 스타일 셰이더를 네 번 고쳤고 그중 세 번이
        /// **재지 않고 한 조정**이었다. 벽이 안 변할 때 「셰이더가 빛을 못 읽는다」와
        /// 「애초에 빛이 안 변한다」가 갈리지 않으면 다섯 번째도 같은 낭비가 된다.
        /// </summary>
        private static string LightState()
        {
            Color ambient = RenderSettings.ambientLight;
            var text = new StringBuilder("조명 — 앰비언트 ");
            text.Append($"({ambient.r:F3}, {ambient.g:F3}, {ambient.b:F3}) ")
                .Append($"휘도 {(0.2126f * ambient.r + 0.7152f * ambient.g + 0.0722f * ambient.b):F4}")
                .Append($" · 모드 {RenderSettings.ambientMode}");

            var lights = FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            System.Array.Sort(lights, (a, b) => string.CompareOrdinal(a.name, b.name));
            foreach (Light l in lights)
                text.Append($" · {l.name}({l.type}) 세기 {l.intensity:F3}");
            return text.ToString();
        }

        /// <summary>
        /// 계기판이 이 장에서 **어디에 어떤 상태로** 있는가. 가림 계측과 독립이다 —
        /// 가림은 「앞에 뭐가 있나」를 보고, 이건 「그게 거기 있기는 한가」를 본다.
        /// </summary>
        private string InstrumentPose()
        {
            var view = FindAnyObjectByType<InstrumentPanelView>();
            if (view == null) return "계기판 — **없다**";

            var pose = new StringBuilder("계기판 ");
            Transform root = view.transform;
            pose.Append($"pos {root.position:F2} · 활성 {(view.isActiveAndEnabled ? "예" : "**아니오**")}");

            var labels = view.GetComponentsInChildren<TMPro.TMP_Text>(true);
            int off = 0, transparent = 0;
            float minAlpha = 1f;
            var far = new StringBuilder();
            foreach (TMPro.TMP_Text label in labels)
            {
                if (!label.isActiveAndEnabled) { off++; continue; }
                float a = label.color.a;
                if (a < minAlpha) minAlpha = a;
                if (a < 0.05f) transparent++;
                // 부모에서 떨어져 나갔는지 — 라벨이 판에서 멀어지면 판 밖에 그려진다.
                float d = Vector3.Distance(label.transform.position, root.position);
                if (d > 3f) far.Append($" {label.name}({d:F2}m)");
            }
            pose.Append($" · 라벨 {labels.Length}개(꺼짐 {off} · 알파<0.05 {transparent} · 최소알파 {minAlpha:F2})");
            if (far.Length > 0) pose.Append($" · **판에서 3m 넘게 떨어진 라벨**{far}");

            var renderers = view.GetComponentsInChildren<Renderer>(true);
            int rendererOff = 0;
            foreach (Renderer r in renderers) if (!r.enabled || !r.gameObject.activeInHierarchy) rendererOff++;
            pose.Append($" · 판 렌더러 {renderers.Length}개(꺼짐 {rendererOff})");
            return pose.ToString();
        }

        private string FrameFacts()
        {
            Camera cam = MeasureCamera;
            if (cam == null) return "프레임 실측 — **확인 못 함**(카메라가 없다)";

            CollectOccluders();
            var line = new StringBuilder("프레임 실측 — ");

            // **글자가 사라진 장에서 「가려졌다」와 「안 그려졌다」를 가른다.**
            //
            // 11차 판정이 `13_overharvest_pulled` 에서 계기판 다섯 줄이 명도 6배
            // 증폭에도 0줄이라고 실측했다. 같은 카메라의 `11`·`12` 는 멀쩡하다.
            // 그런데 이 매니페스트는 그 장을 「온전 3줄 · 가림 2줄」로 적었다 —
            // 시리즈 최대의 거짓 그린이다.
            //
            // 내 가림 계측이 못 잡는 것이 있다는 뜻이고, 그러면 계측을 더 정교하게
            // 하기 전에 **무엇을 못 잡는지부터** 알아야 한다. 라벨이 그 자리에
            // 있는데 뭔가가 앞을 막은 것인지, 애초에 다른 곳으로 갔거나 꺼졌거나
            // 투명해진 것인지가 갈리지 않았다. 그 둘은 고치는 곳이 완전히 다르다.
            //
            // 그래서 장마다 계기판의 **월드 위치·활성·알파**를 적는다. `11`·`12`·`13`
            // 세 줄을 나란히 놓으면 무엇이 달라졌는지가 한눈에 나온다.
            line.Append(InstrumentPose()).Append(" / ").Append(LightState()).Append(" / ");

            var board = FindAnyObjectByType<SpinBoardView>();
            if (board == null) line.Append("결과판 없음");
            else
            {
                int cells = 0, inside = 0, filled = 0;
                var symbols = new StringBuilder();
                for (int i = 0; i < SpinBoard.Cells; i++)
                {
                    Transform cell = board.CellTransform(i);
                    if (cell == null) { symbols.Append('?'); continue; }
                    cells++;
                    if (InFrame(cell.position)) inside++;
                    char mark = CellSymbolMark(cell);
                    if (mark != '·') filled++;
                    symbols.Append(mark);
                    if (i % SpinBoard.Rows == SpinBoard.Rows - 1 && i < SpinBoard.Cells - 1)
                        symbols.Append('|');
                }
                // 인덱스 = 열*3 + 행 이므로 `|` 로 끊은 덩어리 하나가 통관 한 개다.
                // **결과판에는 「온전」을 쓰지 않는다** — 심볼은 통관 유리 *안*에 있어
                // 유리가 늘 선분에 걸린다. 그건 결함이 아니라 의도된 표현이라 가림으로
                // 세면 매 장 거짓 경보가 난다. 여기서 재는 것은 프레임 포함뿐이고,
                // 그 사실을 단어 선택으로 드러낸다.
                line.Append($"결과판 {cells}칸 중 프레임 안 {inside}칸 · 심볼 선 칸 {filled}칸 " +
                            $"[열별 {symbols} · 영=영혼 흡=흡수체 증=증식체 ·=빈칸] " +
                            "(결과판은 통관 유리 안이라 가림 계측 대상이 아니다)");
            }

            var panel = FindAnyObjectByType<InstrumentPanelView>();
            if (panel == null) { line.Append(" / 계기판 없음"); return line.ToString(); }

            int objects = 0, plaquesSkipped = 0, visualLines = 0;
            int whole = 0, occluded = 0, softOnly = 0, clipped = 0, offFrame = 0, blank = 0, unknown = 0, unresolved = 0;
            float facingSum = 0f;
            int facingCount = 0;
            var detail = new StringBuilder();

            foreach (TMPro.TMP_Text label in panel.GetComponentsInChildren<TMPro.TMP_Text>(true))
            {
                if (label == null) continue;
                // 계약 명판은 다른 벽이다. 섞으면 계기면 각도가 엉뚱해진다.
                if (label.name.StartsWith("ContractPlaque", StringComparison.Ordinal)) { plaquesSkipped++; continue; }
                if (!label.gameObject.activeInHierarchy) continue;
                if (label.GetComponent<Renderer>() == null) continue;   // 화면공간 TMP 면 월드 경계가 없다

                objects++;

                Vector3 toLabel = label.transform.position - cam.transform.position;
                if (toLabel.sqrMagnitude > 1e-6f)
                {
                    facingSum += Mathf.Abs(Vector3.Dot(toLabel.normalized, label.transform.forward));
                    facingCount++;
                }

                // TMP 의 글자 기하는 메시가 갱신된 뒤에만 유효하다. PNG 는 이미 읽은 뒤이므로
                // 여기서 강제 갱신해도 찍힌 그림에 영향이 없다 — 대신 문자열과 기하가 어긋날
                // 여지가 사라진다.
                label.ForceMeshUpdate();
                TMPro.TMP_TextInfo info = label.textInfo;
                if (info == null || info.lineInfo == null || info.characterInfo == null)
                {
                    unknown++; visualLines++;
                    detail.Append(FactIndent).Append($"· {label.name} — **확인 못 함**(TMP 글자 기하 없음)\n");
                    continue;
                }

                int lines = Mathf.Min(info.lineCount, info.lineInfo.Length);
                if (lines <= 0)
                {
                    blank++;
                    detail.Append(FactIndent).Append($"· {label.name} — 빈 줄(글자 0자). 온전에 넣지 않는다\n");
                    continue;
                }

                for (int l = 0; l < lines; l++)
                {
                    visualLines++;
                    detail.Append(FactIndent).Append(ProbeLine(cam, label, info, l,
                        ref whole, ref occluded, ref softOnly, ref clipped, ref offFrame, ref blank,
                        ref unresolved)).Append('\n');
                }
            }

            line.Append($" / 계기 글자줄 — TMP 오브젝트 {objects}개가 **시각 줄 {visualLines}줄**을 그린다 " +
                        $"(예전 판본은 오브젝트 수를 「글자줄」이라 적어 `_statusLabel` 의 둘째 줄을 세지 않았다). " +
                        $"온전 {whole}줄 · 가림 {occluded}줄 · 가림? {softOnly}줄 · 잘림 {clipped}줄 · " +
                        $"프레임밖 {offFrame}줄 · 빈줄 {blank}줄 · **보류(품음) {unresolved}줄** · 확인못함 {unknown}줄 " +
                        $"[**온전 = 프레임 안 그리고 가리는 것 없음**] " +
                        $"(계약 명판 라벨 {plaquesSkipped}개는 다른 벽이라 이 계수에서 뺐다)");

            if (facingCount > 0)
            {
                float facing = facingSum / facingCount;
                float degrees = Mathf.Acos(Mathf.Clamp01(facing)) * Mathf.Rad2Deg;
                line.Append($" / 계기면 겉보기 폭 ×{facing:F3} (법선각 {degrees:F1}° · 라벨 {facingCount}개 평균 · " +
                            "**이 장의 값이다 — 다른 장의 계수를 이 장의 성과로 옮겨 적지 말 것**)");
            }
            else line.Append(" / 계기면 각도 — **확인 못 함**(잴 라벨이 없다)");

            Transform pivot = panel.BarPivot;
            if (pivot == null) line.Append(" / 게이지 피벗 없음");
            else
            {
                bool pivotIn = InFrame(pivot.position);
                Renderer pivotBlocker = Blocker(cam.transform.position, pivot.position,
                                                _solidOccluders, _solidBounds, false);
                line.Append($" / 게이지 피벗 {(pivotIn ? "프레임 안" : "프레임 밖")} · " +
                            (pivotBlocker != null
                                ? $"가림 있음 ← `{pivotBlocker.gameObject.name}`"
                                : "가림 없음") + " (표본 1점 — 글자줄만큼 촘촘히 재지 않았다)");

                Renderer bar = pivot.GetComponentInChildren<Renderer>();
                line.Append(bar != null
                    ? $" · 게이지 막대 AABB 꼭짓점 8개 중 프레임 안 {CornersInFrame(bar.bounds)}개"
                    : " · 게이지 막대 렌더러 — **확인 못 함**");
            }

            if (detail.Length > 0) line.Append('\n').Append(detail.ToString().TrimEnd('\n'));

            line.Append('\n').Append(FactIndent).Append(OcclusionMethodNote());
            return line.ToString();
        }

        /// <summary>
        /// 시각 줄 하나를 잰다 — 글자마다 표본 5점을 카메라에서 쏴 프레임 포함과 가림을 함께 본다.
        /// **한 점만 막혀도 그 글자는 가림이다**(안전한 쪽으로 틀린다: 거짓 그린이 아니라 거짓 레드).
        /// </summary>
        private string ProbeLine(Camera cam, TMPro.TMP_Text label, TMPro.TMP_TextInfo info, int lineIndex,
                                 ref int whole, ref int occluded, ref int softOnly,
                                 ref int clipped, ref int offFrame, ref int blank,
                                 ref int unresolved)
        {
            TMPro.TMP_LineInfo lineInfo = info.lineInfo[lineIndex];
            Vector3 eye = cam.transform.position;
            Transform space = label.transform;

            var text = new StringBuilder();
            var blockedText = new StringBuilder();
            var flags = new List<bool>(32);
            var tally = new Dictionary<string, int>();
            int visible = 0, outside = 0, hardBlocked = 0, softBlocked = 0;

            // **`isVisible` 로는 이걸 못 잰다.** 직전 판본이 「글자상자 밖으로 밀려난 글자」를
            // `isVisible == false` 로 세려 했는데, 이 라벨들의 `overflowMode` 는 `Overflow` 다 —
            // TMP 가 넘친 글자를 **그대로 그린다.** 그래서 그 계수기는 24장 전부에서 0 이었고,
            // 10차 판정은 같은 다섯 장에서 잉크가 x=1273 px 에서 끊기는 것을 실측했다.
            // 재지 못하는 축에 0 을 적어 초록불을 준 것이고, 그건 이 매니페스트가 이미
            // 두 번 지적받은 실패다.
            //
            // 그래서 **잉크의 실제 오른쪽 끝**을 글자상자의 쓸 수 있는 오른쪽 끝과 견준다.
            // 넘치면 화면 어디에 있든 그 줄은 상자 밖으로 나간 것이다.
            float inkRight = float.MinValue, lastInkRight = float.MinValue;
            char lastInk = ' ';

            int last = Mathf.Min(lineInfo.lastCharacterIndex, info.characterCount - 1);

            // 선행 훑기 — 이 줄의 글자를 하나라도 품는 렌더러는 이 줄 전체에서 판정 보류다.
            // (자기모순 방지. `_deferredForLabel` 의 주석에 이유를 적었다.)
            _deferredForLabel.Clear();
            for (int c = lineInfo.firstCharacterIndex; c <= last && c < info.characterInfo.Length; c++)
            {
                TMPro.TMP_CharacterInfo pre = info.characterInfo[c];
                if (!pre.isVisible) continue;
                for (int s = 0; s < CharSamples.Length; s++)
                {
                    Vector3 probe = space.TransformPoint(new Vector3(
                        Mathf.Lerp(pre.bottomLeft.x, pre.topRight.x, CharSamples[s].x),
                        Mathf.Lerp(pre.bottomLeft.y, pre.topRight.y, CharSamples[s].y),
                        pre.bottomLeft.z));
                    for (int i = 0; i < _solidOccluders.Count; i++)
                    {
                        Renderer candidate = _solidOccluders[i];
                        if (candidate == null) continue;
                        if (!_solidBounds[i].Contains(candidate.transform.InverseTransformPoint(probe))) continue;
                        _deferredForLabel.Add(candidate);
                        _containingBounds.Add(candidate.gameObject.name);
                    }
                }
            }
            for (int c = lineInfo.firstCharacterIndex; c <= last && c < info.characterInfo.Length; c++)
            {
                TMPro.TMP_CharacterInfo character = info.characterInfo[c];
                // 줄바꿈·제어문자를 그대로 흘리면 매니페스트 한 줄이 두 줄로 쪼개져
                // 「어느 줄의 실측인가」가 사라진다. 보이는 글자만 미리보기에 남긴다.
                if (!character.isVisible)
                {
                    if (text.Length < 28 && !char.IsControl(character.character)) text.Append(character.character);

                    continue;
                }

                visible++;
                if (text.Length < 28 && !char.IsControl(character.character)) text.Append(character.character);
                if (character.topRight.x > inkRight) inkRight = character.topRight.x;
                if (!char.IsWhiteSpace(character.character))
                {
                    lastInk = character.character;
                    lastInkRight = character.topRight.x;
                }

                bool anyOut = false;
                Renderer hard = null;
                bool soft = false;
                for (int s = 0; s < CharSamples.Length; s++)
                {
                    Vector3 local = new Vector3(
                        Mathf.Lerp(character.bottomLeft.x, character.topRight.x, CharSamples[s].x),
                        Mathf.Lerp(character.bottomLeft.y, character.topRight.y, CharSamples[s].y),
                        character.bottomLeft.z);
                    Vector3 world = space.TransformPoint(local);

                    if (!InFrame(world)) anyOut = true;
                    if (hard == null) hard = Blocker(eye, world, _solidOccluders, _solidBounds, true);
                    if (hard == null && !soft && Blocker(eye, world, _softOccluders, _softBounds, false) != null)
                        soft = true;
                }

                if (anyOut) outside++;
                if (hard != null)
                {
                    hardBlocked++;
                    string name = hard.gameObject.name;
                    tally.TryGetValue(name, out int count);
                    tally[name] = count + 1;
                    if (blockedText.Length < 14) blockedText.Append(character.character);
                }
                else if (soft) softBlocked++;
                flags.Add(hard != null);
            }

            if (visible == 0)
            {
                blank++;
                return $"· {label.name} 줄{lineIndex + 1} — 빈 줄(보이는 글자 0자). 온전에 넣지 않는다";
            }

            // 판정 우선순위: 상자넘침 > 프레임밖 > 잘림 > 가림 > 가림? > 온전.
            // 상자넘침이 맨 앞인 이유 — 카메라를 어디에 두어도 돌아오지 않는다.
            Rect box = label.rectTransform.rect;
            float usableRight = box.xMax - label.margin.z;
            float overflow = inkRight > float.MinValue ? inkRight - usableRight : 0f;
            bool overran = overflow > 0.001f;

            string verdict;
            if (overran)            { clipped++; verdict = "상자넘침"; }
            else if (outside == visible) { offFrame++; verdict = "프레임밖"; }
            else if (outside > 0)   { clipped++; verdict = "잘림"; }
            else if (hardBlocked > 0) { occluded++; verdict = "가림"; }
            else if (softBlocked > 0) { softOnly++; verdict = "가림?"; }
            else if (_deferredForLabel.Count > 0)
            {
                // **품은 것을 무죄로 접지 않는다.**
                //
                // 직전 판본은 「AABB 가 글자를 품으면 가리는 게 아니라 품고 있는 것」이라며
                // 후보에서 빼고 그 줄을 「온전」으로 적었다. 기울어진 배면(`PanelBack`)에
                // 대해서는 맞는 처리였고 9차의 거짓 레드 22건이 그렇게 사라졌다.
                //
                // 그런데 **불투명한 판이 글자를 품으면 그건 안 보인다는 뜻이다.**
                // 11차가 `13_overharvest_pulled` 에서 계기판 다섯 줄이 명도 6배 증폭에도
                // 0줄인 것을 실측했는데, 이 매니페스트는 같은 장을 「온전 3줄」로 적었다.
                // 「시리즈 최대의 거짓 그린」이라고 적힌 것이 내가 두 커밋 전에 넣은 이 예외다.
                // 거짓 레드를 고치면서 거짓 그린을 만들었다 — 가림을 고치며 잘림을 만든 것과
                // 같은 형태의 실패다.
                //
                // 옳은 처리는 **재지 못했다고 적는 것**이다. 품은 렌더러가 배면인지 덮개인지는
                // AABB 만으로 못 가른다. 못 가르는 것을 초록으로도 빨강으로도 적지 않는다.
                unresolved++;
                var names = new List<string>();
                foreach (Renderer r in _deferredForLabel) if (r != null) names.Add(r.gameObject.name);
                names.Sort(StringComparer.Ordinal);
                if (names.Count > 3) names.RemoveRange(3, names.Count - 3);
                verdict = $"보류(품음 ← {string.Join(", ", names)})";
            }
            else { whole++; verdict = "온전"; }

            var report = new StringBuilder();
            report.Append($"· {label.name} 줄{lineIndex + 1} 「{text}」 → **{verdict}** · ")
                  .Append($"보이는 글자 {visible}자 · 프레임밖 {outside}자({Percent(outside, visible)}) · ")
                  .Append($"가림 {hardBlocked}자({Percent(hardBlocked, visible)})");

            report.Append($" · 잉크 오른끝 {inkRight:F2} / 상자 {usableRight:F2}");
            if (overran)
                report.Append($" — **{overflow:F2} 단위 넘침**. 마지막 잉크 「{lastInk}」가 {lastInkRight:F2} 에 있다 " +
                              "(카메라를 옮겨도 돌아오지 않는다 — 배치가 아니라 글자상자의 문제다)");

            if (hardBlocked > 0)
            {
                report.Append('[').Append(Side(flags, hardBlocked)).Append(']');
                string worst = null; int worstCount = 0;
                foreach (KeyValuePair<string, int> pair in tally)
                    if (pair.Value > worstCount) { worst = pair.Key; worstCount = pair.Value; }
                report.Append($" ← `{worst}` 가 {worstCount}자");
                if (tally.Count > 1) report.Append($" 외 {tally.Count - 1}종");
                if (blockedText.Length > 0) report.Append($" · 먹힌 글자 「{blockedText}」");
            }
            if (softBlocked > 0)
                report.Append($" · 의심 가림 {softBlocked}자({Percent(softBlocked, visible)} · 입자/선 렌더러 — AABB 대리값이라 확정 아님)");

            return report.ToString();
        }

        private static string Percent(int part, int total)
            => total > 0 ? $"{part * 100f / total:F1}%" : "—%";

        /// <summary>가려진 글자가 줄의 어느 쪽에 몰려 있는가. 8차가 요구한 「좌측 34%」의 좌·우다.</summary>
        private static string Side(List<bool> flags, int blocked)
        {
            int lead = 0;
            while (lead < flags.Count && flags[lead]) lead++;
            int trail = 0;
            while (trail < flags.Count && flags[flags.Count - 1 - trail]) trail++;
            if (lead == blocked) return "좌측";
            if (trail == blocked) return "우측";
            if (lead > 0 && trail > 0) return "양끝";
            return "중간";
        }

        /// <summary>
        /// **재지 못한 것을 이름과 개수로 적는다.** 8차의 핵심 지적이 「측정하지 않은 항목에
        /// 초록불을 줬다」였으므로, 무엇을 못 쟀는지가 초록불 옆에 늘 붙어 있어야 한다.
        /// </summary>
        private string OcclusionMethodNote()
        {
            var note = new StringBuilder("가림 계측 — 렌더러 월드 AABB × (카메라→글자) 선분 교차. ");
            note.Append($"후보 {_solidOccluders.Count}개(확정) + {_softOccluders.Count}개(입자/선/궤적 — 의심만). ")
                .Append($"글자당 표본 {CharSamples.Length}점, 한 점만 막혀도 그 글자는 가림. ")
                .Append("콜라이더를 쓰지 않는다 — 콜라이더는 붙어 있을 수도 없을 수도 있어 거짓 그린이 난다. ");
            note.Append("**한계(초록으로 적지 않는 것)**: ")
                .Append("①상자는 **렌더러 자기 좌표계**에서 잰다(회전 상쇄). 오목 형상은 아직 과대평가라 ")
                .Append("가림을 실제보다 **많이** 셀 수 있다 — 거짓 레드 쪽이다 ")
                .Append($"②TMP 렌더러 {_skippedTextRenderers}개는 후보에서 뺐다(글자가 글자를 가린다고 세면 자기 자신이 걸린다) ")
                .Append("③입자·선·궤적은 `가림?` 까지만 간다 ");
            if (_containingBounds.Count > 0)
            {
                var names = new List<string>(_containingBounds);
                names.Sort(StringComparer.Ordinal);
                if (names.Count > 6) names.RemoveRange(6, names.Count - 6);
                note.Append($"④AABB 가 글자를 **품어** 판정 보류한 렌더러 {_containingBounds.Count}개: ")
                    .Append(string.Join(", ", names))
                    .Append(_containingBounds.Count > 6 ? " …" : string.Empty)
                    .Append(" (대개 계기판 자신의 배면·챔퍼다. 이 목록에 낯선 이름이 있으면 그 줄의 「온전」을 믿지 말 것. ")
                    .Append("**이들은 그 줄 전체에서 가림 후보에서 빠진다** — 같은 물체를 한 글자는 보류, 옆 글자는 가림으로 적던 자기모순을 없앴다) ");
            }
            else note.Append("④글자를 품은 AABB 없음 ");
            note.Append("⑤알파가 0 인 재질도 가림으로 센다 — 그림에서 확인할 것");
            return note.ToString();
        }

        /// <summary>칸에 실제로 서 있는 심볼 한 글자. 빈칸은 `·`.</summary>
        private static char CellSymbolMark(Transform cell)
        {
            for (int c = 0; c < cell.childCount; c++)
            {
                Transform child = cell.GetChild(c);
                if (!child.gameObject.activeSelf) continue;
                switch (SpinBoardView.KindOf(child.name))
                {
                    case SymbolKind.NormalSoul:   return '영';
                    case SymbolKind.Absorber:     return '흡';
                    case SymbolKind.Proliferator: return '증';
                }
            }
            return '·';
        }

        /// <summary>
        /// `UP-FIX-01` 전용 실측. 「천장이 프레임에 들어왔다」를 **주장하지 않고 잰다.**
        ///
        /// 바닥 윗면과 천장 아랫면을 씬에서 직접 읽어 실내 높이를 구하고, 뒷벽 안쪽면에서
        /// 두 면이 화면 세로 어디에 찍혔는지를 적는다. 출입구 상단은 사람 치수 기준자다 —
        /// 「방이 문보다 얼마나 높은가」가 곧 「높이가 읽히는가」의 근거이기 때문이다.
        ///
        /// **8차 판정 §7-5 가 이 줄의 후속 문장에서 단위 혼동을 잡았다.** 「문 위로 프레임의
        /// 58%」라고 적혀 있었는데 0.58 은 **ndc 길이**였고 프레임 비율로는 **29%** 다
        /// (평가자가 그림에서 잰 값 ≈34% 는 문 위 여백 쪽 수치다). 그래서 이제
        /// **위치는 `ndc`, 길이는 `ndc` 와 `%` 를 함께** 적고 변환을 눈에 보이게 남긴다 —
        /// 한 문장 안에서 두 단위가 이름 없이 섞이지 않게 한다.
        /// </summary>
        private string RoomHeightFacts()
        {
            Camera cam = MeasureCamera;
            Renderer floor = ShellRenderer("Floor");
            Renderer ceiling = ShellRenderer("Ceiling");
            Renderer back = ShellRenderer("BackWall_Left");
            if (cam == null || floor == null || ceiling == null || back == null)
                return "높이 실측 — **확인 못 함.** 껍데기를 찾지 못했다 " +
                       $"(카메라={cam != null} 바닥={floor != null} 천장={ceiling != null} 뒷벽={back != null}). " +
                       "이름이 바뀌었으면 이 줄을 고쳐야 한다";

            float floorTop = floor.bounds.max.y;
            float ceilingBottom = ceiling.bounds.min.y;
            float wallZ = back.bounds.min.z;          // 뒷벽 안쪽 면
            float x = cam.transform.position.x;       // 카메라 정면의 세로선에서 잰다

            var low = new Vector3(x, floorTop, wallZ);
            var high = new Vector3(x, ceilingBottom, wallZ);
            float floorNdc = ScreenY(low);
            float ceilingNdc = ScreenY(high);
            float wallSpanNdc = ceilingNdc - floorNdc;

            var text = new StringBuilder(
                $"높이 실측 — 바닥 윗면 y={floorTop:F2} m · 천장 아랫면 y={ceilingBottom:F2} m → " +
                $"실내 높이 {ceilingBottom - floorTop:F2} m. 뒷벽 안쪽면 z={wallZ:F2} m 에서 " +
                $"바닥선 {floorNdc:F2} ndc({(InFrame(low) ? "프레임 안" : "프레임 밖")}) · " +
                $"천장선 {ceilingNdc:F2} ndc({(InFrame(high) ? "프레임 안" : "프레임 밖")}) · " +
                $"두 선 사이 {wallSpanNdc:F2} ndc = 프레임세로의 {NdcSpanToFramePercent(wallSpanNdc):F1}%");

            Renderer lintel = ShellRenderer("BackWall_Lintel");
            if (lintel != null)
            {
                float doorTop = lintel.bounds.min.y;
                var door = new Vector3(x, doorTop, wallZ);
                float doorNdc = ScreenY(door);
                float aboveDoorNdc = 1f - doorNdc;                 // 문 상단 ~ 프레임 위끝
                float doorToCeilingNdc = ceilingNdc - doorNdc;     // 문 상단 ~ 천장선
                text.Append($" / 출입구 상단 y={doorTop:F2} m · {doorNdc:F2} ndc. " +
                            $"문 위 여백 {aboveDoorNdc:F2} ndc = 프레임세로의 {NdcSpanToFramePercent(aboveDoorNdc):F1}% · " +
                            $"문 상단~천장선 {doorToCeilingNdc:F2} ndc = 프레임세로의 {NdcSpanToFramePercent(doorToCeilingNdc):F1}% " +
                            "[**ndc 와 % 는 다른 단위다** — ndc 길이 ×50 = 프레임 %. " +
                            "예전 판본이 0.58 ndc 를 「58%」라 적어 실제(29%)의 두 배로 부풀렸다] " +
                            $"— 방이 문의 {(doorTop > 0f ? (ceilingBottom - floorTop) / doorTop : 0f):F2} 배 높다(배율, 무단위)");
            }
            else text.Append(" / 출입구 상단 — **확인 못 함**(BackWall_Lintel 을 찾지 못했다)");

            return text.ToString();
        }

        /// <summary>껍데기 조각 하나. 못 찾으면 null 을 돌려주고 호출부가 그 사실을 적는다.</summary>
        private static Renderer ShellRenderer(string name)
        {
            GameObject go = GameObject.Find("GrayboxWorld/Car/" + name);
            return go != null ? go.GetComponent<Renderer>() : null;
        }

        /// <summary>
        /// 셔터가 열린 순간 게이지가 **실제로 얼마나 차 있었는지**를 적는다.
        ///
        /// 독립 평가자가 "16은 전력 88%인데 채움이 0%인 18과 구별되지 않는다"고 했다.
        /// 그런데 그것이 가시성 문제인지(막대가 작고 대비가 낮다) 상태 문제인지
        /// (셔터가 열렸을 때 판정이 이미 끝나 전력이 0이었다) 그림만으로는 못 가른다.
        /// 판이 비어 나온 것도, 위험 단계가 안 물든 것도 전부 후자였다.
        ///
        /// 그러니 캡처가 스스로 증언하게 한다. 막대의 실제 폭이 여기 남으면 다음
        /// 평가는 "안 보인다"와 "없다"를 구분할 수 있다.
        /// </summary>
        private static string GaugeFill()
        {
            var panel = FindAnyObjectByType<InstrumentPanelView>();
            Transform pivot = panel != null ? panel.BarPivot : null;
            if (panel == null || pivot == null) return "게이지 — 계기판 없음";

            float width = panel.BarWidth;
            float filled = pivot.localScale.x;
            // **분모가 다른 두 %를 같은 괄호에 넣지 않는다.** 예전 판본은
            // 「(83% 길이, 최대 300% 기준)」이라 적었는데 앞은 *막대 길이* 대비이고
            // 뒤는 *요구 전력* 대비다. 8차 §7-5 의 단위 혼동과 같은 형태다.
            float lengthPercent = width > 0f ? filled / width * 100f : 0f;
            float spanPercent = panel.MaxRatio * 100f;
            return $"게이지 실측 — 채움 {filled:F3} m / 막대 전체 {width:F2} m → " +
                   $"막대길이비 {lengthPercent:F0}% (분모: 막대 전체 길이) · " +
                   $"게이지 상한 {spanPercent:F0}% (분모: 요구 전력) → " +
                   $"이 채움이 나타내는 전력비 {lengthPercent * panel.MaxRatio:F0}% (분모: 요구 전력)";
        }

        /// <summary>
        /// 게임 뷰를 그대로 찍는다. 화면 UI(사고 기록기·HUD)가 포함되는 유일한 경로다.
        /// 전용 카메라 렌더와 달리 해상도가 에디터 창에 종속되므로 비교용이 아니다.
        /// </summary>
        /// <summary>
        /// **`UP-SPACE-03` 은 조준 프롬프트가 증거의 대상이다.** 하이라이트도 프롬프트도
        /// `ScreenSpaceOverlay` 라 전용 카메라 렌더에는 들어가지 않고, 무엇보다
        /// **플레이어가 실제로 겨눠야** 나타난다 — 리그 카메라를 어디에 두든 소용없다.
        ///
        /// 그래서 플레이어를 레버 앞에 세워 겨누게 하고, `CrosshairInteractor` 가 실제로
        /// 대상을 잡았는지 확인한 뒤 화면 캡처를 찍는다. 못 잡았으면 **그 사실을
        /// 매니페스트에 적는다** — 프롬프트가 없는 그림에 「프롬프트가 보인다」라고
        /// 적어 두는 것이 미충족보다 나쁘다.
        /// </summary>
        private IEnumerator AimPromptScreenShot()
        {
            var player = FindAnyObjectByType<Ascend.Prototype.Player.FirstPersonController>();
            var interactor = FindAnyObjectByType<Ascend.Prototype.Player.CrosshairInteractor>();
            var lever = FindAnyObjectByType<Ascend.Prototype.Player.InteractableLever>();

            if (player == null || interactor == null || lever == null)
            {
                _manifest.AppendLine($"{"20_aim_prompt_screen",-26} 건너뜀 — " +
                    $"플레이어={player != null} 조준기={interactor != null} 레버={lever != null}");
                yield break;
            }

            Transform root = player.transform;
            Vector3 leverPoint = lever.transform.position;

            // 레버에서 한 걸음 물러난 자리에 세우고 레버를 본다. 높이는 건드리지 않는다 —
            // 눈높이는 계층이 소유하고, 여기서 다시 계산하면 그 소유권이 둘로 갈린다.
            Vector3 back = root.position - leverPoint;
            back.y = 0f;
            if (back.sqrMagnitude < 0.01f) back = -lever.transform.forward;
            Vector3 stand = leverPoint + back.normalized * 0.9f;
            stand.y = root.position.y;

            Vector3 savedPosition = root.position;
            Quaternion savedRotation = root.rotation;
            var controller = root.GetComponent<CharacterController>();
            bool hadController = controller != null && controller.enabled;

            // **컨트롤러를 끄는 동안 그것을 모는 쪽도 함께 세운다.**
            //
            // 끄는 이유는 콜라이더가 텔레포트를 막기 때문인데, 이 메서드는 끈 채로
            // `WaitFrames(4)` 와 `ScreenShot` 을 **yield** 한다. 그 프레임마다
            // `FirstPersonController.Update` 가 돌아 꺼진 컨트롤러에 `Move()` 를 부르고
            // 콘솔에 `CharacterController.Move called on inactive controller` 가 쌓였다
            // (한 런에 200건). `EvidenceClipRecorder` 의 같은 껐다 켜기가 멀쩡한 이유는
            // 그쪽이 **동기**라 프레임을 넘지 않기 때문이다 — 차이는 `yield` 하나다.
            //
            // 세워 두면 부수 효과도 사라진다. 연출로 잡아 둔 시점을 마우스 입력이
            // 흔들거나 중력이 좌표를 끌어내리지 못한다.
            bool hadPlayer = player.enabled;
            if (hadPlayer) player.enabled = false;
            if (hadController) controller.enabled = false;   // 텔레포트를 콜라이더가 막는다

            root.position = stand;
            Vector3 toLever = leverPoint - (player.ViewCamera != null
                ? player.ViewCamera.transform.position : stand);
            toLever.y = 0f;
            if (toLever.sqrMagnitude > 0.0001f) root.rotation = Quaternion.LookRotation(toLever);

            if (player.ViewCamera != null)
            {
                Vector3 eye = player.ViewCamera.transform.position;
                player.ViewCamera.transform.rotation = Quaternion.LookRotation(leverPoint - eye);
            }

            yield return WaitFrames(4);   // 조준기의 Update 가 레이캐스트할 시간

            bool aimed = interactor.CurrentInteractable != null;
            string target = aimed ? interactor.CurrentInteractable.Prompt : "(없음)";
            yield return ScreenShot("20_aim_prompt_screen",
                $"**화면 캡처** — 플레이어를 레버 앞 0.9m 에 세워 겨눈 상태. " +
                $"조준 대상 {(aimed ? "있음" : "**없음**")} / 프롬프트 「{target}」. " +
                "`UP-SPACE-03`(조준 하이라이트와 행동 프롬프트)은 이 장으로 판정한다 — " +
                "전용 카메라 렌더에는 ScreenSpaceOverlay 가 들어가지 않는다. " +
                "해상도는 주장하지 않는다 — 위 줄의 실측값이 답이다");

            root.position = savedPosition;
            root.rotation = savedRotation;
            if (hadController) controller.enabled = true;
            if (hadPlayer) player.enabled = true;
            yield return WaitFrames(2);
        }

        /// <summary>
        /// **연출이 도는 중**의 화면 캡처. `UP-CORE-13` 의 유일한 판정 자료다.
        ///
        /// 왜 따로 필요한가: 이 리그의 다른 연쇄 장(`15`·`19`)은 `run.Spin()` 을 **직접**
        /// 부르고 판을 손으로 밀어 넣는다. 그건 의도된 우회다 — 연출이 끝나기를 기다리면
        /// 엔진이 수확·정화된 칸을 전부 비워 판이 「회색 상자 하나」로 찍힌다.
        /// 그러나 그 우회 때문에 `SpinPresenter` 가 **한 번도 돌지 않고**,
        /// `IsPresenting` 이 영원히 false 라 `GameHudView.cs:152` 의 연출 중 힌트 페이드가
        /// 걸리지 않는다. 독립 감사가 `19` 의 하단 힌트가 또렷한 것을 근거로 이 사실을 짚었다 —
        /// **판정 대상(연출 중 화면)이 그림에서 구조적으로 빠져 있었다.**
        ///
        /// 그래서 이 장만은 **레버를 실제로 당기고**, 잠금이 걸린 동안 찍는다.
        /// 판이 비는 문제는 여기서는 상관없다 — 재는 것이 판이 아니라 **HUD** 이기 때문이다.
        /// </summary>
        private IEnumerator CapturePresentingScreen(RunSessionBehaviour run,
                                                    RouletteInteractionBridge bridge,
                                                    Risk.RiskStateView risk)
        {
            var lever = FindAnyObjectByType<Ascend.Prototype.Player.InteractableLever>();
            if (lever == null || bridge == null)
            {
                _manifest.AppendLine($"{"22_presenting_screen",-26} **찍지 못했다** — " +
                                     $"레버={lever != null} 브리지={bridge != null}");
                yield break;
            }

            run.ResetRun(RunMode.TenFloor, 4242);
            yield return WaitFrames(2);
            yield return DriveToFloor(run, bridge, 3);

            FloorSession floor = run.Session.Current;
            if (floor == null) yield break;
            if (floor.Phase == FloorPhase.Boarding) run.FinishBoarding();
            if (floor.Phase == FloorPhase.ContractSelection)
                run.SelectContract(floor.Plan.ContractChoices.Length - 1);
            yield return WaitFrames(2);

            if (floor.Phase != FloorPhase.Spinning || floor.SpinsRemaining <= 0)
            {
                _manifest.AppendLine($"{"22_presenting_screen",-26} **찍지 못했다** — " +
                                     $"단계 {floor.Phase} / 남은 스핀 {floor.SpinsRemaining}");
                yield break;
            }

            lever.Interact(gameObject);

            // 잠금이 걸리기를 기다린다. 걸리지 않으면 연출자가 배선되지 않은 것이고,
            // 그 사실을 **매니페스트에 적는다** — 조용히 아무 장도 안 남기지 않는다.
            int guard = 0;
            while (!bridge.IsLocked && guard++ < 120) yield return null;
            if (!bridge.IsLocked)
            {
                _manifest.AppendLine($"{"22_presenting_screen",-26} **찍지 못했다** — " +
                                     "레버를 당겼는데 연출 잠금이 걸리지 않았다 " +
                                     "(연출자 미배선 의심)");
                yield break;
            }

            // 잠긴 상태 한가운데. 너무 이르면 첫 열도 안 열렸고, 끝나면 잠금이 풀린다.
            for (int i = 0; i < 12 && bridge.IsLocked; i++) yield return null;

            // **계기판을 프레임에 넣는다.** 첫 판본은 플레이어가 서 있던 방향 그대로 찍었고,
            // 그 결과 상태 패널의 **상단 두 줄(층·위험도·전력·요구·%)이 프레임 위로 잘렸다** —
            // 독립 평가가 이 장에 세트 최저 판독(1/5)을 주며 「결과를 보는 그 순간에
            // 판단 근거가 화면에 없다」고 적었다. 연출 중임을 증명하려던 장이
            // 정작 판정에 필요한 숫자를 빼먹은 것이다.
            //
            // **첫 수정은 순손실이었다 — 그 사실을 남긴다.**
            // 계기판과 결과판의 *중점*을 겨눴더니 그 중점이 눈높이보다 아래라
            // 카메라가 **바닥을 봤다.** 잘린 두 줄을 되찾으려다 전부 잃었다.
            // 「직전보다 나빠지면 채택하지 않는다」(CLAUDE.md)에 걸린다.
            //
            // **두 번째 수정도 실패했고, 원인은 겨눈 대상이었다.**
            // `InstrumentPanelView.transform` 은 `(0,0,0)` 이다 — 컴포넌트가 붙은 루트일 뿐
            // 글자가 있는 곳이 아니다. 그래서 두 번 다 원점(=바닥)을 봤다.
            // 실제 라벨은 `(-1.04, 1.50~1.76, 1.38)` 에 있다.
            //
            // 그리고 눈높이가 **2.60m** 였다 — 앞 장이 남긴 부감 자세를 그대로 물려받았다.
            // 회전만 고쳐서는 이 둘 다 못 고친다. **세워 놓고 겨눈다.**
            var panel = FindAnyObjectByType<InstrumentPanelView>();
            var view = FindAnyObjectByType<Ascend.Prototype.Player.FirstPersonController>();
            if (panel != null && view != null && view.ViewCamera != null)
            {
                // 라벨들의 중심. 루트가 아니라 **글자가 실제로 있는 곳**이다.
                TMPro.TMP_Text[] labels = panel.GetComponentsInChildren<TMPro.TMP_Text>(true);
                Vector3 center = Vector3.zero;
                int counted = 0;
                for (int i = 0; i < labels.Length; i++)
                {
                    // 계약 명판은 반대편 벽이라 뺀다 — 섞으면 또 중점이 엉뚱한 데로 간다.
                    if (labels[i] == null || labels[i].name.StartsWith("ContractPlaque")) continue;
                    center += labels[i].transform.position;
                    counted++;
                }

                if (counted > 0)
                {
                    center /= counted;

                    // 라벨에서 방 안쪽으로 물러선 자리에 선다. 눈높이는 컨트롤러 설정값을 쓴다 —
                    // 앞 장의 부감(2.60m)을 물려받으면 계기판을 내려다보게 된다.
                    Vector3 inward = new Vector3(-center.x, 0f, -center.z);
                    if (inward.sqrMagnitude < 0.0001f) inward = Vector3.back;
                    Vector3 stand = center + inward.normalized * 1.45f;

                    Transform root = view.transform;
                    root.position = new Vector3(stand.x, root.position.y, stand.z);
                    yield return WaitFrames(1);

                    Vector3 eye = view.ViewCamera.transform.position;
                    Vector3 toLabels = center - eye;
                    if (toLabels.sqrMagnitude > 0.0001f)
                    {
                        root.rotation = Quaternion.LookRotation(
                            new Vector3(toLabels.x, 0f, toLabels.z), Vector3.up);
                        view.ViewCamera.transform.rotation = Quaternion.LookRotation(toLabels);
                    }
                    yield return WaitFrames(1);
                }
            }

            bool stillLocked = bridge.IsLocked;
            yield return ScreenShot("22_presenting_screen",
                $"**연출이 도는 중**의 화면 캡처 (촬영 순간 잠금 {stillLocked}) — " +
                "시드 4242 / 3층 / 증식체 계약. `UP-CORE-13`(한 화면에 모든 숫자를 " +
                "띄우지 않는다)과 `UP-CORE-11`(순차 공개)은 **이 장으로 판정한다.** " +
                "15·19 는 `run.Spin()` 을 직접 불러 `SpinPresenter` 를 거치지 않으므로 " +
                "연출 중 HUD 를 담을 수 없다 — 그 두 장을 이 요구의 증거로 쓰지 말 것. " +
                "이 장은 레버를 실제로 당겨 찍었다");

            // ── `UP-SPACE-09` — 등을 돌려도 결과와 전력 변화를 알 수 있는가 ──
            //
            // 같은 잠금 구간 안에서 찍는다. 런을 다시 돌리면 다른 스핀이 되어
            // 「같은 순간을 앞뒤로 본 것」이 아니게 된다 — 그러면 대조가 성립하지 않는다.
            //
            // 요구가 지정한 채널은 셋이다(PRD §11 · N03): **사운드 · 점등 · 보조 UI.**
            // 그중 정지 화면이 담을 수 있는 것은 점등과 보조 UI 둘이고, 사운드는
            // 그림으로 판정할 수 없다 — 그 한계를 매니페스트에 적는다.
            var player = FindAnyObjectByType<Ascend.Prototype.Player.FirstPersonController>();
            if (player != null)
            {
                Transform root = player.transform;
                Quaternion saved = root.rotation;
                Vector3 toLever = lever.transform.position - root.position;
                toLever.y = 0f;
                if (toLever.sqrMagnitude > 0.0001f)
                {
                    // 레버를 등진다 — 장치가 화각에서 빠지도록 정확히 반대를 본다.
                    root.rotation = Quaternion.LookRotation(-toLever.normalized, Vector3.up);
                }
                yield return WaitFrames(2);

                bool lockedWhenTurned = bridge.IsLocked;
                yield return ScreenShot("23_back_turned_screen",
                    $"**장치를 등지고** 선 채의 화면 캡처 (촬영 순간 잠금 {lockedWhenTurned}) — " +
                    "22 와 **같은 스핀·같은 잠금 구간**이고 시점만 180° 돌렸다. " +
                    "`UP-SPACE-09`(등을 돌려도 결과와 전력 변화를 알 수 있다)는 이 장으로 판정한다. " +
                    "요구 채널 셋 중 **사운드는 정지 화면으로 판정할 수 없다** — " +
                    "이 장이 답할 수 있는 것은 점등과 보조 UI 둘뿐이다. " +
                    "판정 질문: 결과판이 화각에서 빠진 상태에서 **전력·스핀·연쇄 단계가 읽히는가**, " +
                    "그리고 위험 단계가 조명만으로 구분되는가");

                root.rotation = saved;
                yield return WaitFrames(1);
            }
            else
            {
                _manifest.AppendLine($"{"23_back_turned_screen",-26} **찍지 못했다** — " +
                                     "FirstPersonController 를 찾지 못했다");
            }
        }

        private IEnumerator ScreenShot(string name, string note)
        {
            yield return new WaitForEndOfFrame();
            Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                WritePng(name, shot.EncodeToPNG());
                _screenShots++;
                // 기준 해상도와 다르면 **매니페스트에 크게 적는다.** 화면 캡처는 게임 뷰
                // 크기로 나오므로, 뷰가 작으면 이 장들만 조용히 저해상도가 된다 —
                // 실제로 3장이 816×714 로 나가 판독성 평가에서 그 세 장만 불리하게 채점됐고,
                // 평가자가 「816px 게임 뷰 종속일 수 있다」는 단서를 달아야 했다.
                //
                // **몇 장이 이 경로인지도 세어서 적는다.** 예전에는 「이 한 장만 방식이 다르다 /
                // 나머지 18장」이 호출부에 하드코딩돼 있었고, 세트가 23장으로 커진 뒤에도
                // 그 문장이 매번 새로 찍혀 나갔다(`UP-REC-06`). 총계는 파일 끝에 있다.
                bool atSpec = shot.width == SpecCaptureWidth && shot.height == SpecCaptureHeight;
                _manifest.AppendLine($"{name,-26} 화면 캡처 {shot.width}×{shot.height} px " +
                    $"(게임 뷰 경로 {_screenShots}번째 · 전용 카메라 경로는 {Width}×{Height} px)" +
                    (atSpec ? string.Empty
                            : $"  ⚠ 기준 {SpecCaptureWidth}×{SpecCaptureHeight} px 가 아니다 — " +
                              "전용 카메라 경로와 해상도가 다르므로 판독성 비교에 그대로 쓰지 말 것"));
                _manifest.AppendLine($"{"",-26} {note}");

                // **화면 캡처에도 실측을 붙인다.** 8차가 「전력 줄이 먹혔다」고 잡은 열한 장 중
                // `22_presenting_screen` 은 이 경로인데, 예전 판본은 이 경로에 실측을 한 줄도
                // 붙이지 않아 그 장에 대해서는 **주장도 측정도 없었다.**
                // 재는 카메라는 플레이어 시점이다 — 전용 카메라로 재면 그림과 다른 화각을
                // 이 장의 실측이라고 적게 된다(8차 §7-1 이 잡은 오류의 자동화된 형태다).
                Camera view = ScreenCamera();
                if (view == null)
                    _manifest.AppendLine($"{"",-26} 프레임 실측 — **확인 못 함**(화면 캡처를 그린 카메라를 찾지 못했다)");
                else
                {
                    _measureCamera = view;
                    _manifest.AppendLine($"{"",-26} {GaugeFill()}");
                    _manifest.AppendLine($"{"",-26} {FrameFacts()}");
                    RecordBoardRoi(name, shot.width, shot.height);
                    _manifest.AppendLine($"{"",-26} 실측 기준 카메라 — `{view.name}` " +
                                         $"pos {view.transform.position:F2} m · FOV {view.fieldOfView:F1}° · " +
                                         $"화면비 {view.aspect:F3} (캡처 {(float)shot.width / Mathf.Max(1, shot.height):F3}) " +
                                         "— 전용 카메라 경로의 값과 섞어 쓰지 말 것");
                    _measureCamera = null;
                }
            }
            finally
            {
                Destroy(shot);
            }
        }

        /// <summary>게임 뷰 화면 캡처를 실제로 그린 카메라. 못 찾으면 null 을 돌려주고 호출부가 적는다.</summary>
        private static Camera ScreenCamera()
        {
            var player = FindAnyObjectByType<Ascend.Prototype.Player.FirstPersonController>();
            if (player != null && player.ViewCamera != null) return player.ViewCamera;
            return Camera.main;
        }

        private static IEnumerator WaitFrames(int frames)
        {
            for (int i = 0; i < frames; i++) yield return null;
        }

        /// <summary>
        /// **시간**으로 기다린다. 프레임 수가 아니다.
        ///
        /// `RiskStateView._blendSpeed`는 2.2/초라 조명·험이 새 단계로 수렴하는 데 약 1.4초가
        /// 걸린다. 그런데 에디터 Play 모드는 100fps 넘게 돌기 때문에 `WaitFrames(30)`이
        /// 0.25초밖에 안 된다 — 블렌딩이 시작만 한 시점에 셔터가 열린다.
        ///
        /// 그래서 위험 4단계 캡처가 "텍스트와 경고등만 바뀌고 방 안은 그대로"로 나왔다.
        /// 독립 평가자가 "06→09→10 사이에서 방 안의 어떤 것도 변하지 않는다"고 지적했는데,
        /// 프리셋에는 밝기 1.0 → 0.80 → 0.58 → 0.34 의 차이가 실제로 들어 있다.
        /// 연출이 없었던 것이 아니라 **캡처가 기다리지 않았다.**
        ///
        /// `HeroSliceAutoPilot`이 이미 같은 함정을 주석으로 경고해 뒀다.
        /// </summary>
        /// <remarks>
        /// `Time.realtimeSinceStartup` 이 아니라 `Time.time` 을 쓴다.
        /// <see cref="Start"/>가 `Time.captureDeltaTime = 1/60` 을 세우므로 프레임당
        /// 게임 시간이 정확히 1/60초씩 흐른다 — 벽시계로 재면 기계 부하에 따라
        /// 프레임 수가 달라지고, 블렌딩이 프레임 수만큼 진행하므로 **같은 시드로 찍은
        /// 두 캡처가 픽셀 단위로 달라진다.** 게임 시간으로 재야 프레임 수가 고정된다.
        /// </remarks>
        private static IEnumerator WaitSeconds(float seconds)
        {
            float deadline = Time.time + seconds;
            while (Time.time < deadline) yield return null;
        }

        private static IEnumerator WaitWhileLocked(RouletteInteractionBridge bridge)
        {
            float deadline = Time.realtimeSinceStartup + 30f;
            while (bridge != null && bridge.IsLocked && Time.realtimeSinceStartup < deadline)
                yield return null;
        }

        /// <summary>
        /// 이 런이 실제로 통과한 포스트 체인. 주장하지 않고 **씬과 볼륨 스택에서 읽는다.**
        /// </summary>
        private string PostChainFacts()
        {
            var sb = new StringBuilder("포스트 체인 실측 — ");
            var data = _camera != null ? _camera.GetUniversalAdditionalCameraData() : null;
            sb.Append(data == null
                ? "전용 카메라 데이터 없음"
                : $"전용 카메라 post {(data.renderPostProcessing ? "ON" : "OFF")} · AA {data.antialiasing}");

            Camera screen = Camera.main;
            var sdata = screen != null ? screen.GetUniversalAdditionalCameraData() : null;
            sb.Append(sdata == null
                ? " / 화면 카메라 없음"
                : $" / 화면 카메라 post {(sdata.renderPostProcessing ? "ON" : "OFF")} · AA {sdata.antialiasing}");

            var rp = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            sb.Append(rp == null ? " / RP 없음" : $" / RP {rp.name} colorGrading {rp.colorGradingMode} · MSAA {rp.msaaSampleCount}");

            var vols = FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            sb.Append($" / 씬 Volume {vols.Length}개");
            foreach (var v in vols)
            {
                if (v.sharedProfile == null) { sb.Append($" [{v.gameObject.name}: 프로파일 없음]"); continue; }
                sb.Append($" [{v.gameObject.name} global {v.isGlobal} pri {v.priority:F0} → {v.sharedProfile.name}:");
                foreach (var c in v.sharedProfile.components) sb.Append(' ').Append(c.GetType().Name).Append(c.active ? "" : "(off)");
                sb.Append(']');
            }
            sb.Append($" / UnityRandom seed {RandomSeed}");
            return sb.ToString();
        }

        /// <summary>
        /// 이 캡처가 어느 커밋에서 나왔는가. `.git` 을 직접 읽는다 — 에디터 전용 경로라
        /// 프로세스를 띄우지 않는다(빌드에 들어가지 않도록 `UNITY_EDITOR` 로 감싼다).
        ///
        /// **더러운 워킹 트리는 커밋 해시로 표현되지 않는다.** 해시만 적으면 「그 커밋
        /// 상태」라고 오해되므로, 씬 파일이 커밋보다 새로우면 그 사실을 함께 적는다.
        /// </summary>
        private static string BuildStamp()
        {
#if UNITY_EDITOR
            try
            {
                string root = Directory.GetParent(Application.dataPath).FullName;
                string gitDir = Path.Combine(root, ".git");
                string head = Path.Combine(gitDir, "HEAD");
                if (!File.Exists(head)) return "빌드 스탬프 — .git 없음";

                string headText = File.ReadAllText(head).Trim();
                string sha;
                if (headText.StartsWith("ref:"))
                {
                    string refPath = headText.Substring(4).Trim();
                    string refFile = Path.Combine(gitDir, refPath.Replace('/', Path.DirectorySeparatorChar));
                    if (File.Exists(refFile)) sha = File.ReadAllText(refFile).Trim();
                    else
                    {
                        sha = "unknown";
                        string packed = Path.Combine(gitDir, "packed-refs");
                        if (File.Exists(packed))
                            foreach (string line in File.ReadAllLines(packed))
                                if (line.EndsWith(" " + refPath)) { sha = line.Substring(0, 40); break; }
                    }
                    headText = refPath + " @ " + sha;
                }
                else { sha = headText; headText = "detached @ " + sha; }

                string scenePath = Path.Combine(root, "Assets/Prototype_Elevator/Scenes/Prototype_Elevator.unity");
                string dirty = "";
                if (File.Exists(scenePath) && File.Exists(head))
                {
                    System.DateTime sceneTime = File.GetLastWriteTimeUtc(scenePath);
                    System.DateTime headTime = File.GetLastWriteTimeUtc(head);
                    if (sceneTime > headTime)
                        dirty = "  ⚠ **씬 파일이 HEAD 보다 새롭다** — 커밋되지 않은 편집분이 이 캡처에 들어 있다";
                }
                return "빌드 스탬프 — " + headText + dirty;
            }
            catch (System.Exception e)
            {
                return "빌드 스탬프 — 읽지 못했다 (" + e.GetType().Name + ")";
            }
#else
            return "빌드 스탬프 — 에디터가 아니다";
#endif
        }

        private void Header(RunSessionBehaviour run)
        {
            _manifest.AppendLine("=== 10층 고정 캡처 세트 ===");
            _manifest.AppendLine($"해상도 {Width}×{Height} / FOV {Fov} / 모드 {run.Mode}");
            // 베이스라인은 기기에 종속된다(`CLAUDE.md`, `TECH_SPEC.md` §14).
            // 이 줄이 달라지면 이전 캡처와 비교하지 않는다.
            _manifest.AppendLine("machineFingerprint: " +
                $"{SystemInfo.operatingSystemFamily}|{SystemInfo.graphicsDeviceType}|" +
                $"{SystemInfo.graphicsDeviceName}|{Application.unityVersion}");
            _manifest.AppendLine($"OS {SystemInfo.operatingSystem}");
            // 재현 조건. 프레임 시간이 고정되지 않으면 같은 시드도 다른 프레임 수에서
            // 찍혀 블렌딩 진행도가 달라진다 — 두 캡처가 바이트 단위로 갈라진다.
            _manifest.AppendLine($"captureDeltaTime {CaptureDeltaTime:F5} (vSync off) — " +
                                 "대기는 벽시계가 아니라 게임 시간으로 잰다");
            // **포스트 체인 상태를 적는다.** 2026-08-02 정찰 전까지 이 씬은 포스트가 통째로
            // 꺼진 채였는데(`m_RenderPostProcessing: 0` · 씬 Volume 0개) 매니페스트에는
            // 그 사실이 한 줄도 없었다. 14라운드 동안 평가자는 「포스트가 걸린 그림」이라고
            // 가정하고 채점했다. 재는 것만이 주장을 대신한다.
            _manifest.AppendLine(PostChainFacts());
            // **어느 빌드에서 찍었는지 적는다.** 지금까지는 파일 mtime 으로만 역추적했고,
            // 그래서 `TenFloor_NoPost`(13:06)와 `TenFloor`(14:01) 사이에 캐빈 확대와
            // M_Gray 배선이 끼어든 것을 아무도 즉시 알아채지 못했다 — 두 세트를
            // 「포스트만 다른 대조군」이라고 믿고 결론을 냈고 그 근거가 무효였다(`UP-FIX-48`).
            _manifest.AppendLine(BuildStamp());
            // **촬영 경로가 둘이라는 사실을 정확히 적는다.** 예전 머리말은 「전용 카메라의
            // RenderTexture 렌더다」 한 줄이었는데 세트에는 게임 뷰 화면 캡처도 섞여 있다.
            // 각 줄이 스스로 어느 경로인지 밝히고, 장수는 파일 끝에서 **센 값**을 적는다.
            _manifest.AppendLine("촬영 경로는 둘이다 — 전용 카메라(RenderTexture)는 화면 UGUI HUD 를 담지 않고, " +
                                 "게임 뷰 화면 캡처는 담는다. 각 줄이 어느 쪽인지 스스로 밝힌다.");
            _manifest.AppendLine("장수·해상도·프레임 내용은 **주장하지 않고 잰다** — 하드코딩된 개수 주장은 " +
                                 "세트가 커질 때마다 조용히 틀려진다(`UP-REC-06`).");
            _manifest.AppendLine("위험 단계는 연출이 아니라 실제 게임 상태다 — 무엇을 해서 도달했는지 각 줄에 적혀 있다.");
            // ── 8차 판정이 이 두 줄을 요구했다 ─────────────────────────────────
            _manifest.AppendLine(
                "**가림을 잰다.** 「온전」의 정의가 바뀌었다 — 이제 **프레임 안 그리고 가리는 것이 없음**이다. " +
                "직전 세트는 「가림은 재지 않았다」고 스스로 고지해 두고 열한 장을 「온전 3·잘림 0」으로 적었다. " +
                "측정하지 않은 항목에 대한 초록불이었다. 방식은 콜라이더가 아니라 **렌더러 월드 AABB × " +
                "(카메라→글자) 선분 교차**이고, 글자마다 표본 5점을 쏜다. 못 잰 축은 각 줄의 " +
                "「가림 계측」 꼬리에 이름과 개수로 남으며 **그 줄은 온전으로 세지 않는다.**");
            _manifest.AppendLine(
                "**단위 규칙.** `m` 미터 · `°` 도 · `px` 픽셀 · `ndc` 정규화 좌표(−1 아래·왼끝 … +1 위·오른끝, " +
                "화면 전체가 2.0 ndc) · `%` 프레임 또는 명시된 분모 대비 비율. " +
                "**ndc 길이 → 프레임 % 는 ×50 이다** — 0.58 ndc 는 58% 가 아니라 **29%** 다. " +
                "직전 세트가 그 둘을 한 문장에서 섞어 실제의 두 배를 적었다. " +
                "이 파일이 내보내는 모든 수에는 단위가 붙는다. 단위 없는 수가 보이면 그것이 버그다.");
            _manifest.AppendLine();
        }

        /// <summary>
        /// 폴더에 남아 있는데 **이번 런이 쓰지 않은** PNG 를 찾는다.
        ///
        /// 왜 필요한가: 캡처 한 장이 조용히 건너뛰어져도 옛 PNG 는 그대로 남는다.
        /// 그러면 평가자는 **이 매니페스트가 설명하지 않는 그림**을 이 세트의 일부로 보고
        /// 채점한다 — 「주장과 그림이 다르다」의 가장 조용한 형태다.
        /// </summary>
        private string StaleFiles()
        {
            try
            {
                string directory = Path.Combine(Directory.GetCurrentDirectory(), OutputDirectory);
                if (!Directory.Exists(directory)) return "잔존 검사 — 출력 폴더가 없다";

                var stale = new List<string>();
                foreach (string path in Directory.GetFiles(directory, "*.png", SearchOption.TopDirectoryOnly))
                {
                    string file = Path.GetFileName(path);
                    if (!_written.Contains(file)) stale.Add(file);
                }
                if (stale.Count == 0)
                    return $"잔존 검사 — 폴더의 PNG 가 이번 런이 쓴 {_written.Count}개와 정확히 같다. 옛 그림이 섞여 있지 않다";

                stale.Sort(StringComparer.Ordinal);
                return $"잔존 검사 — ⚠ 이번 런이 다시 찍지 않은 PNG {stale.Count}개가 폴더에 남아 있다: " +
                       $"{string.Join(", ", stale)}. **이 파일들은 이 매니페스트가 설명하는 그림이 아니다**";
            }
            catch (Exception exception)
            {
                return $"잔존 검사 — 실패: {exception.Message}. **확인 못 함**";
            }
        }

        private void Finish()
        {
            _manifest.AppendLine();
            // 하드코딩 금지. 셋 다 이번 런에서 센 값이다.
            _manifest.AppendLine($"촬영 {_shots}장 — 전용 카메라(RenderTexture {Width}×{Height}) {_renderShots}장 / " +
                                 $"게임 뷰 화면 캡처 {_screenShots}장");
            _manifest.AppendLine(StaleFiles());

            // 결과판 ROI. **원점을 여기에도 적는다** — 도구는 `-BoardRoiOrigin` 으로
            // 받은 대로 찍어 주지만 틀린 것을 감지하지는 못하므로, 생산자 쪽 사실을
            // 파일과 매니페스트 양쪽에 남긴다.
            _manifest.AppendLine(
                $"결과판 ROI — `{BoardRoiPath}` 에 {_roiRows}장 · 측정 불가 {_roiUnmeasurable}장 " +
                "(프레임 안 칸 0개인 장은 행을 만들지 않는다). " +
                "**좌표 원점은 좌하단**(Unity 뷰포트 기준)이라 " +
                "`capture-metrics.ps1 -BoardRoiCsv <세트>/board-roi.csv -BoardRoiOrigin bottomleft` 로 읽어야 한다");
            try
            {
                string roiPath = Path.Combine(Directory.GetCurrentDirectory(), BoardRoiPath);
                Directory.CreateDirectory(Path.GetDirectoryName(roiPath));
                // **머리말이 반드시 첫 줄이어야 한다.** 도구는 `Import-Csv` 로 읽고
                // 그것은 첫 줄을 무조건 헤더로 삼는다 — 앞에 `#` 주석을 붙였더니
                // 주석이 헤더가 되어 모든 필드가 null 이 됐고 「0 장 수록」이 나왔다.
                // 그래서 원점은 주석이 아니라 **열**로 싣는다. 사실이 파일과 함께
                // 이동하고, 기계가 읽을 수 있고, 파서를 깨뜨리지 않는다.
                //
                // BOM 도 붙이지 않는다 — 붙이면 첫 헤더 이름이 `﻿file` 이 되어
                // `$row.file` 이 조용히 null 이 된다. 헤더는 전부 ASCII 라 BOM 이 필요 없다.
                var roi = new StringBuilder();
                roi.AppendLine("file,x,y,w,h,origin");
                roi.Append(_boardRoi);
                File.WriteAllText(roiPath, roi.ToString(), new UTF8Encoding(false));
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[상승] 결과판 ROI 저장 실패: {exception.Message}");
            }

            try
            {
                string path = Path.Combine(Directory.GetCurrentDirectory(), ManifestPath);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, _manifest.ToString(), new UTF8Encoding(true));
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[상승] 매니페스트 저장 실패: {exception.Message}");
            }
            Debug.Log($"[상승] 캡처 완료\n{_manifest}");

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        private void OnDestroy()
        {
            RestoreClock();
            if (_target != null) { _target.Release(); Destroy(_target); }
            if (_readback != null) Destroy(_readback);
        }
    }
}
