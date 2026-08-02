<#
.SYNOPSIS
    캡처 PNG 지표 계산 코어. `tools/capture-metrics.ps1` 과 `selftest.ps1` 이 공유한다.

.DESCRIPTION
    docs/GRAPHICS_TARGET.md §2 의 측정 축을 픽셀에서 직접 잰다.

      G-1a 국소 분산   — 8×8 블록 휘도 표준편차의 **전체** 중앙값 (직전 정의 그대로)
      G-1b 텍스처 블록 — 블록 std ≥ 4.0 인 블록만 모은 중앙값 (무지 면을 분모에서 뺀다)
      G-1c 선명 블록   — 전체 블록 중 std ≥ 8.0 인 것의 비율(%)
      G-2  휘도 분포   — 5 / 50 / 95 퍼센타일
      G-3  발광        — 휘도 ≥ 200 화소 비율
      G-4  계단 셰이딩 — 지정 주사선에서 「경계가 있는 평탄면」을 센다
                        계단 = 인접 휘도차 ≤ δ 인 연속 구간 중 길이 ≥ 8px
                        단차 = 인접한 두 계단의 평균 휘도차 절대값 ≥ 4
                        관측 = **측정 가능** 그리고 계단 ≥ 3 그리고 단차 ≥ 2
                        측정 불가 = 주사선 휘도 동적 범위(max−min) < 8
                        (평탄 구간 수·최장은 진단용으로 계속 낸다 — 판정에는 쓰지 않는다)
      G-5  빈 평면     — 32×32 블록 중 표준편차 < 4 인 블록 비율
      금색 화소        — R−B ≥ 60 그리고 G−B ≥ 30
      마젠타           — R>200 그리고 B>200 그리고 G<80 (셰이더 오류 색 회귀 감시)

    휘도는 0.2126R + 0.7152G + 0.0722B 다. 가중치 합이 정확히 1.0 이므로
    회색 v 의 휘도는 v 다 — 자체 검사의 기댓값이 정수로 떨어지는 근거다.

    ── 왜 C# 인가 ──────────────────────────────────────────────────────────────
    24장 × 1920×1080 = 5,000만 화소다. Windows PowerShell 5.1 의 스크립트 루프는
    초당 수십만 회 수준이라 이것만으로 수 분이 걸린다. `System.Drawing.Bitmap.GetPixel`
    은 그보다 한 자릿수 더 느리다. 그래서 픽셀 순회 전체를 Add-Type 으로 컴파일한
    C# 안에 두고, PowerShell 은 결과 구조체만 받는다. 비트맵 접근은 `LockBits` +
    `Marshal.Copy` 로 한 번에 바이트 배열을 꺼내 쓴다.

    ⚠ Windows PowerShell 5.1 의 Add-Type 은 C# 5 컴파일러다. 문자열 보간(`$"..."`),
      식 본문 멤버, `out var`, null 조건 연산자를 쓰면 컴파일이 깨진다.
#>

Set-StrictMode -Off
$ErrorActionPreference = 'Stop'

# ── 통과선 (docs/GRAPHICS_TARGET.md §2) ───────────────────────────────────────
# 스크립트 안에 상수로 두되 출처를 함께 적는다. 통과선을 바꾸려면 문서를 먼저 바꾼다.
$script:CM_Thresholds = [ordered]@{
    G1_LocalStdMedian   = 12.0   # G-1a 대표 8장의 국소 분산 중앙값 ≥ 12.0
    G2_P5Max            = 24     # G-2 휘도 5퍼센타일 ≤ 24
    G2_P50Min           = 36     # G-2 휘도 중앙값 36 ~ 96
    G2_P50Max           = 96
    G2_P95Min           = 170    # G-2 휘도 95퍼센타일 ≥ 170
    G3_GlowMinPct       = 1.0    # G-3 발광 화소 비율 1.0% ~ 6.0%
    G3_GlowMaxPct       = 6.0
    G4_StepMinLength    = 8      # G-4 계단 한 칸의 최소 길이 8px
    G4_BoundaryMinDelta = 4      # G-4 단차로 인정하는 인접 계단 평균 휘도차 ≥ 4
    G4_StepsMinPerFrame = 3      # G-4 한 장에서 계단 ≥ 3개
    G4_BoundsMinPerFrame= 2      # G-4 한 장에서 단차 ≥ 2개
    G4_StairFramesMin   = 12     # G-4 24장 중 계단이 관측되는 장 ≥ 12
    G5_EmptyPlaneMaxPct = 18.0   # G-5 빈 평면 비율 ≤ 18%
    MagentaMax          = 0      # 셰이더 오류 색은 0 이어야 한다
    # ── G-SLOT (VISUAL_VERDICT.md §10 — 16차 독립 평가자가 정한 정의) ──────────
    SlotA_BandMax       = 0      # G-SLOT-A 「띠」 개수 = 0 (전 24장)
    SlotB_ColorMaxPct   = 2.0    # G-SLOT-B ROI 안 색 화소 ≤ 2%
}

# ── 분류 임계 — **통과선이 아니다** (GRAPHICS_TARGET §5 축 정정 2026-08-02) ────
#
# 아래 셋은 「무엇을 무엇으로 셀 것인가」를 정할 뿐, 그 자체로 합격/불합격을 만들지 않는다.
# G-1b·G-1c 의 통과선은 **아직 없다** — 실측을 보고 사용자가 정한다. 근거 없는 숫자를
# 먼저 박아 넣지 않는다(이 저장소가 UP-FIX-35 에서 거짓 그린을 만든 경로가 그것이다).
$script:CM_Classify = [ordered]@{
    # G-1b 텍스처 블록으로 인정하는 8×8 블록 std 하한.
    # 실측 근거: 무지 표면 위 블록은 구조적으로 std ≈ 1.75 에 고정되고(대표 8장의 28.6%),
    # 텍스처 표면 위 블록은 그보다 위에 분포한다 (GRAPHICS_TARGET §5.1).
    # 4.0 이 실제로 두 집단을 가르는지는 블록 std 히스토그램으로 매번 확인한다.
    G1b_TexturedBlockStd = 4.0
    # G-1c 「선명 블록」으로 인정하는 하한. GRAPHICS_TARGET §2 G-1 의
    # 「조명 그라디언트만으로는 8을 못 넘는다」에서 온 값이다.
    G1c_SharpBlockStd    = 8.0
    # G-4 「측정 가능」의 하한. 주사선 구간의 휘도 동적 범위(max−min)가 이 값 미만이면
    # 그 장은 「미관측」이 아니라 **「측정 불가」**다 — 평평한 언릿 면 위이거나 완전 단색이라
    # 계단이 원리적으로 만들어질 수 없다 (GRAPHICS_TARGET §5.4: 19장 중 9장이 Unlit TubeFrame).
    G4_MeasurableMinSpan = 8

    # ── G-SLOT-A 형상 조건 (VISUAL_VERDICT.md §10) ────────────────────────────
    # ROI = 결과판 아홉 칸의 화면 AABB 합집합. **ROI 밖은 세지 않는다** —
    # 이것이 천장등을 배제하는 장치이고, PD-22(천장등 유지)와 충돌하지 않는 이유다.
    SlotA_ContrastMinDelta  = 25     # ① 주변 대비 |ΔL| ≥ 25 로 이진화
    SlotA_AspectMin         = 4.0    # ② 연결 성분 장축/단축 ≥ 4
    SlotA_MajorMinFraction  = 0.35   # ③ 장축 길이 ≥ ROI 폭의 35%
    SlotA_CrossingsMin      = 2      # ④ 칸 경계를 2개 이상 횡단

    # ── G-SLOT-B 색 조건 ──────────────────────────────────────────────────────
    # ⚠ `max−min ≥ 55` 는 `R−B ≥ 60` 에 **수학적으로 함의된다** (max ≥ R, min ≤ B
    #   이므로 max−min ≥ R−B ≥ 60 > 55). 즉 이 절은 **아무것도 걸러내지 못한다.**
    #   14차 식과 실제로 달라지는 것은 `R ≥ 120` 하나뿐이다 — 어두운 따뜻한 회색벽을
    #   빼는 것은 채도가 아니라 밝기 하한이다. 정의대로 넷 다 구현하되 이 사실을 보고한다.
    #   (자체 검사 케이스가 이 함의를 반증 형태로 고정한다.)
    SlotB_RminusB           = 60
    SlotB_GminusB           = 30
    SlotB_SaturationMin     = 55
    SlotB_RedMin            = 120
}

# ── 제안 (적용하지 않는다) ────────────────────────────────────────────────────
# 도구는 이 값으로 판정하지 않는다. 보고서에 「이렇게 바꾸는 것을 제안한다」로만 찍는다.
$script:CM_Proposals = [ordered]@{
    G4_ObservedRatioMin = 0.50   # 관측됨 / (관측됨 + 미관측) ≥ 50%
}

# 대표 8장 — GRAPHICS_TARGET §2 「대표 8장은 01·02·06·09·12·15·18·21」
$script:CM_RepresentativePrefixes = @('01','02','06','09','12','15','18','21')

# 주사선 기본값. G-4 는 「지정된 주사선」을 재므로 위치가 지표의 일부다.
#   y : 이미지 높이의 43.5% 지점  (GRAPHICS_TARGET §2 G-4 기본값)
#   x : 이미지 폭의 10% 지점부터 200px  (좌벽 — G-2 회귀 감시축과 같은 벽면)
$script:CM_DefaultScanYFraction = 0.435
$script:CM_DefaultScanXFraction = 0.10
$script:CM_DefaultScanLength    = 200

# 평탄 판정의 허용 휘도차. 기본 1 = 「인접 화소 휘도차 ≤ 1」.
#
# ⚠ 이 값이 지표의 의미를 바꾼다. δ=1 은 8비트 양자화된 **매끄러운 그라디언트**도
#   하나의 긴 평탄 구간으로 본다 (인접 레벨차가 0 또는 1 뿐이기 때문이다).
#   δ=0 은 완전히 같은 값만 평탄으로 보므로 양자화 계단 하나하나를 센다.
#   두 값 모두 항상 계산해 나란히 보고한다 — 어느 쪽을 판정에 쓰는지가 결론을 뒤집는다.
$script:CM_DefaultFlatDelta = 1

# ── G-4 가 「평탄 구간 수·최장」이 아닌 이유 ──────────────────────────────────
# 직전 정의는 「평탄 구간 ≤ 24개 그리고 최장 ≥ 20px」였다. 이 조건은 **완전히 평평한
# 벽을 자동으로 통과시킨다** — 무지 벽의 주사선은 평탄 구간 1개, 최장 200px 이라
# 두 조건을 여유롭게 만족한다. 실제로 TenFloor 24장이 「23/24 장에서 계단 관측」으로
# 통과했는데, 같은 세트의 국소 분산 중앙값은 0.00 이었고 14차 사람 판정은
# 「24장 중 계단이 관측된 장 0장」이었다 (docs/runtime/VISUAL_VERDICT.md, UP-FIX-35).
# 거짓 그린이었다.
#
# 계단이 존재한다는 것은 **평탄면이 여럿 있고 그 사이에 뚜렷한 단차가 있다**는 뜻이다.
# 그래서 평탄면 하나만으로는 계단이 아니고, 매끄러운 그라디언트도 계단이 아니다.
$script:CM_DefaultStepMinLength    = 8
$script:CM_DefaultBoundaryMinDelta = 4

function Initialize-CaptureMetrics {
    <#
    .SYNOPSIS
        C# 분석기를 현재 세션에 로드한다. 이미 로드돼 있으면 아무것도 하지 않는다.
    #>
    if ('CaptureMetrics.Analyzer' -as [type]) { return }

    $cs = @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace CaptureMetrics
{
    public class ImageResult
    {
        public string Path;
        public string Name;
        public int    Width;
        public int    Height;
        public long   TotalPixels;

        public double LocalStdMedian;      // G-1a 8x8 블록 표준편차의 **전체** 중앙값

        // ── G-1b / G-1c (2026-08-02 축 정정) ──────────────────────────────
        // G-1a 는 「화면의 몇 %가 텍스처인가」를 재고 있었다. 무지 표면 위 블록이
        // 28.6% 를 차지하고 그 std 가 ~1.75 로 고정되므로, 전체 중앙값은 커버리지에
        // 지배된다. G-1b 는 그 무지 블록을 **분모에서 빼고** 텍스처가 얼마나 잘
        // 보이는지만 잰다. G-1c 는 반대로 「얼마나 넓은 면적이 선명한가」를 잰다.
        public double TexturedBlockStdMedian; // G-1b std >= G1bBlockStdMin 인 블록의 중앙값. 블록 0개면 NaN
        public int    TexturedBlockCount;     // G-1b 의 분모
        public double TexturedBlockPercent;   // 전체 블록 중 텍스처 블록 비율(%)
        public double SharpBlockPercent;      // G-1c std >= G1cBlockStdMin 인 블록 비율(%)
        public int    SharpBlockCount;
        public double G1bBlockStdMin;         // 실제로 쓴 텍스처 블록 임계
        public double G1cBlockStdMin;         // 실제로 쓴 선명 블록 임계

        // 블록 std 히스토그램 — 임계 4.0 이 실제로 두 집단을 가르는지 눈으로 확인하는 근거.
        // 마지막 칸은 넘침(>= BinWidth*(Bins-1)) 이다.
        public long[]  BlockStdHist;
        public double  BlockStdHistBinWidth;
        public int     BlockStdHistBins;

        public int    LumP5;               // G-2
        public int    LumP50;
        public int    LumP95;
        public double GlowPercent;         // G-3  휘도 >= 200 화소 비율(%)

        public int    ScanY;               // G-4  실제로 잰 주사선
        public int    ScanX0;
        public int    ScanLen;
        public int    FlatDeltaUsed;       // 평탄 판정에 쓴 허용 휘도차
        public int    FlatRunCount;        // 허용차 <= FlatDeltaUsed
        public int    FlatRunLongest;
        public int    FlatRunCountEq;      // 허용차 = 0 (완전히 같은 값만 평탄)
        public int    FlatRunLongestEq;
        public int    ScanSpan;            // 주사선 휘도 최대 - 최소

        public int    StepMinLengthUsed;   // G-4 계단으로 인정한 최소 길이(px)
        public int    BoundaryMinDeltaUsed;// G-4 단차로 인정한 평균 휘도차
        public int    StepCount;           // 길이 >= StepMinLengthUsed 인 평탄 구간 수
        public int    StepLongest;         // 그중 최장 길이
        public int    BoundaryCount;       // 인접 계단 평균 휘도차 >= BoundaryMinDeltaUsed 인 경계 수

        public double EmptyPlanePercent;   // G-5  32x32 블록 중 std < 4 인 블록 비율(%)

        public long   GoldPixels;
        public long   MagentaPixels;

        public int    BlockCount8;
        public int    BlockCount32;

        // ── G-SLOT (VISUAL_VERDICT.md §10) ────────────────────────────────
        // ROI 를 **주지 않으면 추정하지 않는다.** SlotRoiProvided=false 는
        // 「띠 0」이 아니라 **「측정 불가」**다. 둘을 같은 값으로 내보내면
        // 재지 않은 축이 초록으로 읽힌다 — G-4 가 무지 면을 자동 통과시킨 것과
        // 정확히 같은 구조의 거짓 그린이 된다.
        public bool   SlotRoiProvided;
        public int    SlotRoiX, SlotRoiY, SlotRoiW, SlotRoiH;
        public long   SlotRoiPixels;
        public int    SlotRoiBackgroundLum;   // ROI 휘도 중앙값 = 「주변」의 기준값

        public int    SlotComponentCount;     // 이진화 후 연결 성분 수 (8-이웃)
        public int    SlotBandCount;          // G-SLOT-A 네 조건을 **모두** 만족한 성분 수
        public long   SlotColorPixels;        // G-SLOT-B 조건 화소 수
        public double SlotColorPercent;       // ROI 대비 비율(%)

        // 최대 면적 성분의 진단값 — 「왜 띠가 아닌가」를 조건별로 볼 수 있게 낸다.
        public int    SlotTopArea;
        public int    SlotTopBboxW, SlotTopBboxH;
        public int    SlotTopMajor, SlotTopMinor;
        public double SlotTopRatio;
        public double SlotTopMeanDelta;       // ① 의 여유를 보이는 값
        public int    SlotTopCrossings;
        public bool   SlotTopC2, SlotTopC3, SlotTopC4;
    }

    public static class Analyzer
    {
        public const int BlockG1 = 8;
        public const int BlockG5 = 32;
        public const double EmptyPlaneStdThreshold = 4.0;
        public const int GlowLuminance = 200;
        public const int FlatDelta = 1;
        public const int StepMinLength = 8;      // G-4 계단 한 칸의 최소 길이(px)
        public const int BoundaryMinDelta = 4;   // G-4 단차로 인정하는 평균 휘도차

        // G-1b / G-1c 분류 임계. 통과선이 아니라 **집단을 가르는 선**이다.
        public const double TexturedBlockStd = 4.0;
        public const double SharpBlockStd    = 8.0;

        // 블록 std 히스토그램: 폭 0.5 로 [0,32) 를 64칸, 마지막 1칸이 >= 32 넘침.
        public const double HistBinWidth = 0.5;
        public const int    HistBins     = 65;

        // ── G-SLOT (VISUAL_VERDICT.md §10) ────────────────────────────────
        public const int    SlotContrastMinDelta = 25;
        public const double SlotAspectMin        = 4.0;
        public const double SlotMajorMinFraction = 0.35;
        public const int    SlotCrossingsMin     = 2;
        public const int    SlotCellsPerAxis     = 3;   // 3×3 결과판
        public const int    SlotColorRminusB     = 60;
        public const int    SlotColorGminusB     = 30;
        public const int    SlotColorSaturation  = 55;
        public const int    SlotColorRedMin      = 120;

        // 0~255 로 반올림·클램프한 정수 휘도.
        public static int Q(double L)
        {
            int q = (int)Math.Round(L, MidpointRounding.AwayFromZero);
            if (q < 0) return 0;
            if (q > 255) return 255;
            return q;
        }

        public static ImageResult Analyze(string path, double scanYFrac, double scanXFrac, int scanLen)
        {
            return Analyze(path, scanYFrac, scanXFrac, scanLen, FlatDelta);
        }

        public static ImageResult Analyze(string path, double scanYFrac, double scanXFrac, int scanLen, int flatDelta)
        {
            return Analyze(path, scanYFrac, scanXFrac, scanLen, flatDelta, StepMinLength, BoundaryMinDelta);
        }

        public static ImageResult Analyze(string path, double scanYFrac, double scanXFrac, int scanLen,
                                          int flatDelta, int stepMinLength, int boundaryMinDelta)
        {
            return Analyze(path, scanYFrac, scanXFrac, scanLen, flatDelta, stepMinLength, boundaryMinDelta,
                           TexturedBlockStd, SharpBlockStd);
        }

        public static ImageResult Analyze(string path, double scanYFrac, double scanXFrac, int scanLen,
                                          int flatDelta, int stepMinLength, int boundaryMinDelta,
                                          double texturedBlockStd, double sharpBlockStd)
        {
            // ROI 를 주지 않은 호출이다 → G-SLOT 은 **측정 불가**로 남는다.
            // 여기서 ROI 를 추정하면 그 순간 「재지 않은 축」이 숫자를 갖게 된다.
            return Analyze(path, scanYFrac, scanXFrac, scanLen, flatDelta, stepMinLength, boundaryMinDelta,
                           texturedBlockStd, sharpBlockStd, false, 0, 0, 0, 0);
        }

        public static ImageResult Analyze(string path, double scanYFrac, double scanXFrac, int scanLen,
                                          int flatDelta, int stepMinLength, int boundaryMinDelta,
                                          double texturedBlockStd, double sharpBlockStd,
                                          bool hasRoi, int roiX, int roiY, int roiW, int roiH)
        {
            ImageResult r = new ImageResult();
            r.Path = path;
            r.Name = System.IO.Path.GetFileNameWithoutExtension(path);

            int w, h;
            double[] lum;
            long gold = 0;
            long magenta = 0;
            long[] hist = new long[256];

            // G-SLOT 용 ROI 절편. ROI 를 주지 않으면 전부 null 로 남는다.
            double[] roiLum = null;
            byte[] roiR = null, roiG = null, roiB = null;
            int rw = 0, rh = 0;

            // 파일 잠금을 남기지 않으려고 바이트로 먼저 읽는다.
            byte[] fileBytes = File.ReadAllBytes(path);
            using (MemoryStream ms = new MemoryStream(fileBytes, false))
            using (Bitmap bmp = new Bitmap(ms))
            {
                w = bmp.Width;
                h = bmp.Height;
                if (w <= 0 || h <= 0) throw new InvalidOperationException("빈 이미지: " + path);

                // 원본이 24bpp 든 8bpp 팔레트든 32bppArgb 로 변환해 받는다.
                BitmapData bd = bmp.LockBits(new Rectangle(0, 0, w, h),
                                             ImageLockMode.ReadOnly,
                                             PixelFormat.Format32bppArgb);
                byte[] buf;
                int stride;
                try
                {
                    stride = bd.Stride;
                    if (stride < 0)
                        throw new InvalidOperationException("아래에서 위로 저장된 비트맵은 지원하지 않는다: " + path);
                    buf = new byte[(long)stride * h > int.MaxValue ? 0 : stride * h];
                    if (buf.Length == 0) throw new InvalidOperationException("이미지가 너무 크다: " + path);
                    Marshal.Copy(bd.Scan0, buf, 0, buf.Length);
                }
                finally { bmp.UnlockBits(bd); }

                lum = new double[w * h];
                int li = 0;
                for (int y = 0; y < h; y++)
                {
                    int row = y * stride;
                    for (int x = 0; x < w; x++)
                    {
                        int p = row + (x << 2);
                        int b  = buf[p];       // Format32bppArgb 는 메모리에서 B,G,R,A 순이다
                        int g  = buf[p + 1];
                        int rr = buf[p + 2];

                        double L = 0.2126 * rr + 0.7152 * g + 0.0722 * b;
                        lum[li++] = L;

                        hist[Q(L)]++;

                        if (rr - b >= 60 && g - b >= 30) gold++;
                        if (rr > 200 && b > 200 && g < 80) magenta++;
                    }
                }

                // ── G-SLOT ROI 절편 ─────────────────────────────────────────
                // 프레임 밖으로 나간 ROI 는 **잘라서** 쓴다. 잘린 뒤 넓이가 0 이면
                // 「ROI 를 받았지만 화면에 없다」이므로 역시 측정 불가로 남긴다.
                if (hasRoi)
                {
                    int rx = roiX, ry = roiY;
                    rw = roiW; rh = roiH;
                    if (rx < 0) { rw += rx; rx = 0; }
                    if (ry < 0) { rh += ry; ry = 0; }
                    if (rx + rw > w) rw = w - rx;
                    if (ry + rh > h) rh = h - ry;
                    if (rw > 0 && rh > 0)
                    {
                        r.SlotRoiProvided = true;
                        r.SlotRoiX = rx; r.SlotRoiY = ry; r.SlotRoiW = rw; r.SlotRoiH = rh;
                        roiLum = new double[rw * rh];
                        roiR = new byte[rw * rh];
                        roiG = new byte[rw * rh];
                        roiB = new byte[rw * rh];
                        int k2 = 0;
                        for (int y = 0; y < rh; y++)
                        {
                            int row = (ry + y) * stride;
                            for (int x = 0; x < rw; x++)
                            {
                                int p = row + ((rx + x) << 2);
                                byte bch = buf[p];
                                byte gch = buf[p + 1];
                                byte rch = buf[p + 2];
                                roiB[k2] = bch; roiG[k2] = gch; roiR[k2] = rch;
                                roiLum[k2] = 0.2126 * rch + 0.7152 * gch + 0.0722 * bch;
                                k2++;
                            }
                        }
                    }
                    else { rw = 0; rh = 0; }
                }
            }

            long total = (long)w * h;
            r.Width = w;
            r.Height = h;
            r.TotalPixels = total;
            r.GoldPixels = gold;
            r.MagentaPixels = magenta;

            // ── G-2 휘도 분포 (히스토그램 최근접 순위) ──────────────────────
            r.LumP5  = Percentile(hist, total, 0.05);
            r.LumP50 = Percentile(hist, total, 0.50);
            r.LumP95 = Percentile(hist, total, 0.95);

            // ── G-3 발광 ────────────────────────────────────────────────────
            long glow = 0;
            for (int v = GlowLuminance; v < 256; v++) glow += hist[v];
            r.GlowPercent = total > 0 ? (100.0 * glow / total) : 0.0;

            // ── G-1a 국소 분산 (전체 블록 중앙값) ───────────────────────────
            int n8;
            double[] std8 = BlockStdDevs(lum, w, h, BlockG1, out n8);
            r.BlockCount8 = n8;
            r.LocalStdMedian = Median(std8, n8);

            // ── G-1b 텍스처 블록 중앙값 · G-1c 선명 블록 비율 ───────────────
            // 무지 표면 위 블록(std ≈ 1.75)을 분모에서 빼야 「텍스처가 잘 보이는가」가
            // 「화면의 몇 %가 텍스처인가」와 분리된다 (GRAPHICS_TARGET §5.1).
            r.G1bBlockStdMin = texturedBlockStd;
            r.G1cBlockStdMin = sharpBlockStd;
            int texN = 0, sharpN = 0;
            for (int i = 0; i < n8; i++)
            {
                if (std8[i] >= texturedBlockStd) texN++;
                if (std8[i] >= sharpBlockStd) sharpN++;
            }
            r.TexturedBlockCount = texN;
            r.SharpBlockCount = sharpN;
            r.TexturedBlockPercent = n8 > 0 ? (100.0 * texN / n8) : 0.0;
            r.SharpBlockPercent = n8 > 0 ? (100.0 * sharpN / n8) : 0.0;
            if (texN > 0)
            {
                double[] tex = new double[texN];
                int tk = 0;
                for (int i = 0; i < n8; i++) if (std8[i] >= texturedBlockStd) tex[tk++] = std8[i];
                r.TexturedBlockStdMedian = Median(tex, texN);
            }
            else
            {
                // 텍스처 블록이 하나도 없으면 중앙값은 **정의되지 않는다.**
                // 0 을 내면 「텍스처가 있는데 아주 평평하다」와 구분되지 않는다.
                r.TexturedBlockStdMedian = double.NaN;
            }

            // 블록 std 히스토그램 — 임계가 실제로 두 집단을 가르는지 확인하는 근거.
            r.BlockStdHistBinWidth = HistBinWidth;
            r.BlockStdHistBins = HistBins;
            long[] bh = new long[HistBins];
            for (int i = 0; i < n8; i++)
            {
                int bin = (int)(std8[i] / HistBinWidth);
                if (bin < 0) bin = 0;
                if (bin > HistBins - 1) bin = HistBins - 1;
                bh[bin]++;
            }
            r.BlockStdHist = bh;

            // ── G-5 빈 평면 ─────────────────────────────────────────────────
            int n32;
            double[] std32 = BlockStdDevs(lum, w, h, BlockG5, out n32);
            r.BlockCount32 = n32;
            int empty = 0;
            for (int i = 0; i < n32; i++) if (std32[i] < EmptyPlaneStdThreshold) empty++;
            r.EmptyPlanePercent = n32 > 0 ? (100.0 * empty / n32) : 0.0;

            // ── G-4 평탄 구간 ───────────────────────────────────────────────
            int scanY = (int)Math.Round(scanYFrac * h, MidpointRounding.AwayFromZero);
            if (scanY < 0) scanY = 0; else if (scanY > h - 1) scanY = h - 1;
            int x0 = (int)Math.Round(scanXFrac * w, MidpointRounding.AwayFromZero);
            if (x0 < 0) x0 = 0; else if (x0 > w - 1) x0 = w - 1;
            int len = (scanLen <= 0) ? (w - x0) : Math.Min(scanLen, w - x0);
            if (len < 0) len = 0;

            r.ScanY = scanY;
            r.ScanX0 = x0;
            r.ScanLen = len;

            // 휘도차는 **반올림한 정수 휘도**로 잰다. 화소값은 8비트 정수이고,
            // 배정밀도 그대로 비교하면 회색 v 의 휘도가 v 에서 1e-16 만큼 벗어나
            // 「차이가 정확히 1」인 경계가 부동소수 잔차로 갈린다.
            int[] scan = new int[len];
            int lo = 255, hi = 0;
            if (len > 0)
            {
                int baseIdx = scanY * w + x0;
                for (int i = 0; i < len; i++)
                {
                    int q = Q(lum[baseIdx + i]);
                    scan[i] = q;
                    if (q < lo) lo = q;
                    if (q > hi) hi = q;
                }
            }
            else { lo = 0; hi = 0; }
            r.ScanSpan = hi - lo;

            int c1, l1, c0, l0;
            FlatRuns(scan, flatDelta, out c1, out l1);
            FlatRuns(scan, 0, out c0, out l0);
            r.FlatDeltaUsed = flatDelta;
            r.FlatRunCount = c1;
            r.FlatRunLongest = l1;
            r.FlatRunCountEq = c0;
            r.FlatRunLongestEq = l0;

            int stepN, boundN, stepLong;
            StairRuns(scan, flatDelta, stepMinLength, boundaryMinDelta, out stepN, out boundN, out stepLong);
            r.StepMinLengthUsed = stepMinLength;
            r.BoundaryMinDeltaUsed = boundaryMinDelta;
            r.StepCount = stepN;
            r.BoundaryCount = boundN;
            r.StepLongest = stepLong;

            // ── G-SLOT ─────────────────────────────────────────────────────
            // ROI 가 없으면 **아무것도 채우지 않는다.** SlotRoiProvided=false 가
            // 「측정 불가」의 단일 신호다.
            if (r.SlotRoiProvided) ComputeSlots(r, roiLum, roiR, roiG, roiB, rw, rh);

            return r;
        }

        // ══════════════════════════════════════════════════════════════════
        // G-SLOT-A / G-SLOT-B — VISUAL_VERDICT.md §10 (16차 독립 평가자 정의)
        //
        // ROI = 결과판 아홉 칸의 화면 AABB 합집합. ROI 밖은 세지 않는다 —
        // 이것이 천장등을 배제하는 장치이고 PD-22(천장등 유지)와 충돌하지 않는 이유다.
        //
        // A(형상·주지표): ① |ΔL| ≥ 25 이진화 ② 장축/단축 ≥ 4
        //                 ③ 장축 ≥ ROI 폭의 35% ④ 칸 경계 2개 이상 횡단
        //                 넷을 **모두** 만족한 성분이 「띠」다. 통과선 = 0개.
        // B(색·보조):     R−B ≥ 60 · G−B ≥ 30 · max−min ≥ 55 · R ≥ 120. 통과선 ≤ 2%.
        //
        // ── 정의가 비워 둔 자리를 어떻게 메웠는가 (숨기지 않는다) ───────────
        // 「주변 대비」의 **주변**이 무엇인지 정의에 없다. 두 읽기가 가능하다.
        //   (a) ROI 자체의 배경 수준   (b) 각 화소 주변의 국소 고리
        // (b) 는 두꺼운 띠의 **가장자리만** 잡아 ③(장축 길이)을 구조적으로 못 넘게
        // 만든다 — 즉 정의가 잡으려는 대상을 스스로 놓친다. 그래서 (a) 를 쓰고,
        // 기준값은 **ROI 휘도 중앙값**이다. 그 값을 결과에 함께 내보내므로
        // 나중에 이 선택을 검증할 수 있다.
        //
        // 장축/단축은 성분의 **축정렬 경계상자** 긴 변/짧은 변이다. 대각선 띠는
        // 경계상자가 정사각형에 가까워져 비율이 과소평가된다 — 알려진 한계이고
        // 도구가 이것을 숨기지 않도록 채움률 대신 경계상자 값을 그대로 내보낸다.
        // ══════════════════════════════════════════════════════════════════
        public static void ComputeSlots(ImageResult r, double[] lum, byte[] rr, byte[] gg, byte[] bb,
                                        int rw, int rh)
        {
            ComputeSlots(r, lum, rr, gg, bb, rw, rh,
                         SlotContrastMinDelta, SlotAspectMin, SlotMajorMinFraction,
                         SlotCrossingsMin, SlotCellsPerAxis);
        }

        public static void ComputeSlots(ImageResult r, double[] lum, byte[] rr, byte[] gg, byte[] bb,
                                        int rw, int rh,
                                        int contrastMinDelta, double aspectMin, double majorMinFraction,
                                        int crossingsMin, int cellsPerAxis)
        {
            int n = rw * rh;
            r.SlotRoiPixels = n;
            if (n <= 0) return;

            // 「주변」의 기준값 = ROI 휘도 중앙값.
            long[] rhist = new long[256];
            for (int i = 0; i < n; i++) rhist[Q(lum[i])]++;
            int bg = Percentile(rhist, n, 0.50);
            r.SlotRoiBackgroundLum = bg;

            // ① 이진화
            bool[] mask = new bool[n];
            for (int i = 0; i < n; i++)
            {
                int d = Q(lum[i]) - bg;
                if (d < 0) d = -d;
                mask[i] = (d >= contrastMinDelta);
            }

            // 칸 경계 (ROI 로컬 좌표). 3×3 이면 축마다 내부 경계선 2개, 합 4개.
            if (cellsPerAxis < 2) cellsPerAxis = 2;
            int[] vx = new int[cellsPerAxis - 1];
            int[] hy = new int[cellsPerAxis - 1];
            for (int k = 1; k < cellsPerAxis; k++)
            {
                vx[k - 1] = (int)Math.Round((double)rw * k / cellsPerAxis, MidpointRounding.AwayFromZero);
                hy[k - 1] = (int)Math.Round((double)rh * k / cellsPerAxis, MidpointRounding.AwayFromZero);
            }

            // ③ 은 ROI **폭** 기준이다 (높이가 아니다) — 세로로 긴 띠도 같은 잣대로 잰다.
            double majorMin = majorMinFraction * rw;

            bool[] seen = new bool[n];
            int[] stack = new int[n];
            int comps = 0, bands = 0, topArea = 0;

            for (int start = 0; start < n; start++)
            {
                if (!mask[start] || seen[start]) continue;

                int sp = 0;
                stack[sp++] = start;
                seen[start] = true;

                int minX = rw, maxX = -1, minY = rh, maxY = -1, area = 0;
                double sumDelta = 0.0;

                while (sp > 0)
                {
                    int p = stack[--sp];
                    int py = p / rw;
                    int px = p - py * rw;
                    area++;
                    if (px < minX) minX = px;
                    if (px > maxX) maxX = px;
                    if (py < minY) minY = py;
                    if (py > maxY) maxY = py;
                    int dd = Q(lum[p]) - bg;
                    if (dd < 0) dd = -dd;
                    sumDelta += dd;

                    // 8-이웃
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        int ny = py + dy;
                        if (ny < 0 || ny >= rh) continue;
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            int nx = px + dx;
                            if (nx < 0 || nx >= rw) continue;
                            int q = ny * rw + nx;
                            if (mask[q] && !seen[q]) { seen[q] = true; stack[sp++] = q; }
                        }
                    }
                }

                comps++;

                int bw = maxX - minX + 1;
                int bh = maxY - minY + 1;
                int major = bw > bh ? bw : bh;
                int minor = bw > bh ? bh : bw;
                double ratio = (minor > 0) ? ((double)major / minor) : 0.0;

                // ④ 「횡단」은 **엄격한 걸침**이다 — 경계선 왼쪽(위)에 화소가 있고
                //    동시에 경계선 위치 이상에도 화소가 있어야 한다. 한 칸에 꼭 맞게
                //    들어찬 성분은 횡단 0 이다.
                int cross = 0;
                for (int k = 0; k < vx.Length; k++) if (minX < vx[k] && maxX >= vx[k]) cross++;
                for (int k = 0; k < hy.Length; k++) if (minY < hy[k] && maxY >= hy[k]) cross++;

                bool c2 = (ratio >= aspectMin);
                bool c3 = (major >= majorMin);
                bool c4 = (cross >= crossingsMin);
                if (c2 && c3 && c4) bands++;

                if (area > topArea)
                {
                    topArea = area;
                    r.SlotTopArea = area;
                    r.SlotTopBboxW = bw;
                    r.SlotTopBboxH = bh;
                    r.SlotTopMajor = major;
                    r.SlotTopMinor = minor;
                    r.SlotTopRatio = ratio;
                    r.SlotTopMeanDelta = sumDelta / area;
                    r.SlotTopCrossings = cross;
                    r.SlotTopC2 = c2; r.SlotTopC3 = c3; r.SlotTopC4 = c4;
                }
            }

            r.SlotComponentCount = comps;
            r.SlotBandCount = bands;

            // ── G-SLOT-B ────────────────────────────────────────────────────
            long col = 0;
            for (int i = 0; i < n; i++)
            {
                int R = rr[i], G = gg[i], B = bb[i];
                int mx = R > G ? R : G; if (B > mx) mx = B;
                int mn = R < G ? R : G; if (B < mn) mn = B;
                if (R - B >= SlotColorRminusB && G - B >= SlotColorGminusB &&
                    (mx - mn) >= SlotColorSaturation && R >= SlotColorRedMin) col++;
            }
            r.SlotColorPixels = col;
            r.SlotColorPercent = 100.0 * col / n;
        }

        // 인접 화소 휘도차가 delta 이하인 최대 연속 구간들. 개수와 최장 길이를 낸다.
        private static void FlatRuns(int[] v, int delta, out int count, out int longest)
        {
            count = 0; longest = 0;
            if (v == null || v.Length == 0) return;
            int cur = 1;
            for (int i = 1; i < v.Length; i++)
            {
                int d = v[i] - v[i - 1];
                if (d < 0) d = -d;
                if (d <= delta) { cur++; }
                else { count++; if (cur > longest) longest = cur; cur = 1; }
            }
            count++;
            if (cur > longest) longest = cur;
        }

        // ── G-4 계단·단차 ────────────────────────────────────────────────────
        // 계단 = 인접 화소 휘도차 <= delta 인 연속 구간 중 길이 >= minLen 인 것.
        // 단차 = **인접한 두 계단**의 평균 휘도차 절대값 >= minBoundary 인 경계.
        //        minLen 에 못 미쳐 버려진 짧은 조각은 계단이 아니므로 건너뛰고,
        //        채택된 계단끼리 이어서 비교한다.
        //
        // 이 두 값을 함께 봐야 하는 이유: 무지 벽은 계단 1·단차 0, 매끄러운 그라디언트는
        // 계단 1(또는 0)·단차 0, 진짜 4단 계단만 계단 4·단차 3 이 된다.
        private static void StairRuns(int[] v, int delta, int minLen, int minBoundary,
                                      out int stepCount, out int boundaryCount, out int stepLongest)
        {
            stepCount = 0; boundaryCount = 0; stepLongest = 0;
            if (v == null || v.Length == 0) return;
            if (minLen < 1) minLen = 1;

            bool hasPrev = false;
            double prevMean = 0.0;
            int start = 0;
            double sum = v[0];

            for (int i = 1; i <= v.Length; i++)
            {
                bool cont = false;
                if (i < v.Length)
                {
                    int d = v[i] - v[i - 1];
                    if (d < 0) d = -d;
                    cont = (d <= delta);
                }
                if (cont) { sum += v[i]; continue; }

                int len = i - start;
                if (len >= minLen)
                {
                    double mean = sum / len;
                    stepCount++;
                    if (len > stepLongest) stepLongest = len;
                    if (hasPrev && Math.Abs(mean - prevMean) >= minBoundary) boundaryCount++;
                    prevMean = mean;
                    hasPrev = true;
                }
                if (i < v.Length) { start = i; sum = v[i]; }
            }
        }

        // 완전한 블록만 센다. 우측·하단 나머지는 버린다 — 부분 블록은 표본 수가 달라
        // 분산 분포를 왜곡한다.
        private static double[] BlockStdDevs(double[] lum, int w, int h, int bs, out int count)
        {
            int bx = w / bs;
            int by = h / bs;
            count = bx * by;
            double[] outv = new double[count > 0 ? count : 1];
            if (count == 0) return outv;

            double inv = 1.0 / (bs * bs);
            int k = 0;
            for (int by_ = 0; by_ < by; by_++)
            {
                int y0 = by_ * bs;
                for (int bx_ = 0; bx_ < bx; bx_++)
                {
                    int x0 = bx_ * bs;
                    double sum = 0.0, sumsq = 0.0;
                    for (int y = 0; y < bs; y++)
                    {
                        int row = (y0 + y) * w + x0;
                        for (int x = 0; x < bs; x++)
                        {
                            double v = lum[row + x];
                            sum += v;
                            sumsq += v * v;
                        }
                    }
                    double mean = sum * inv;
                    double var = sumsq * inv - mean * mean;   // 모분산 (N 으로 나눈다)
                    if (var < 0.0) var = 0.0;                 // 부동소수 잔차
                    outv[k++] = Math.Sqrt(var);
                }
            }
            return outv;
        }

        private static double Median(double[] v, int n)
        {
            if (n <= 0) return 0.0;
            double[] copy = new double[n];
            Array.Copy(v, copy, n);
            Array.Sort(copy);
            if ((n & 1) == 1) return copy[n / 2];
            return (copy[n / 2 - 1] + copy[n / 2]) * 0.5;
        }

        // 최근접 순위 퍼센타일. rank = ceil(p * N) 번째 값.
        private static int Percentile(long[] hist, long total, double p)
        {
            if (total <= 0) return 0;
            long rank = (long)Math.Ceiling(p * total);
            if (rank < 1) rank = 1;
            long cum = 0;
            for (int v = 0; v < 256; v++)
            {
                cum += hist[v];
                if (cum >= rank) return v;
            }
            return 255;
        }
    }

    // 자체 검사용 합성 이미지 생성기. 손으로 계산 가능한 값만 만든다.
    public static class TestImages
    {
        public static void WriteBgra(string path, int w, int h, byte[] bgra)
        {
            using (Bitmap bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb))
            {
                BitmapData bd = bmp.LockBits(new Rectangle(0, 0, w, h),
                                             ImageLockMode.WriteOnly,
                                             PixelFormat.Format32bppArgb);
                try
                {
                    int rowBytes = w * 4;
                    if (bd.Stride == rowBytes)
                    {
                        Marshal.Copy(bgra, 0, bd.Scan0, bgra.Length);
                    }
                    else
                    {
                        for (int y = 0; y < h; y++)
                        {
                            IntPtr dst = new IntPtr(bd.Scan0.ToInt64() + (long)y * bd.Stride);
                            Marshal.Copy(bgra, y * rowBytes, dst, rowBytes);
                        }
                    }
                }
                finally { bmp.UnlockBits(bd); }
                bmp.Save(path, ImageFormat.Png);
            }
        }

        private static byte[] Alloc(int w, int h) { return new byte[w * h * 4]; }

        private static void Put(byte[] buf, int w, int x, int y, byte r, byte g, byte b)
        {
            int p = (y * w + x) * 4;
            buf[p] = b; buf[p + 1] = g; buf[p + 2] = r; buf[p + 3] = 255;
        }

        public static void Solid(string path, int w, int h, byte r, byte g, byte b)
        {
            byte[] buf = Alloc(w, h);
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) Put(buf, w, x, y, r, g, b);
            WriteBgra(path, w, h, buf);
        }

        // 화소마다 독립 난수. 결정론을 위해 시드를 받는다.
        public static void Noise(string path, int w, int h, int seed)
        {
            Random rnd = new Random(seed);
            byte[] buf = Alloc(w, h);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    Put(buf, w, x, y, (byte)rnd.Next(256), (byte)rnd.Next(256), (byte)rnd.Next(256));
            WriteBgra(path, w, h, buf);
        }

        // x 방향 계단. steps 개의 동일 폭 회색 띠. 레벨 간격은 levelStep.
        public static void StepGradient(string path, int w, int h, int steps, int levelStep)
        {
            byte[] buf = Alloc(w, h);
            int bandW = w / steps;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int band = x / bandW;
                    if (band > steps - 1) band = steps - 1;
                    byte v = (byte)(band * levelStep);
                    Put(buf, w, x, y, v, v, v);
                }
            }
            WriteBgra(path, w, h, buf);
        }

        // 열마다 회색값을 직접 지정한다. values.Length 가 폭이 된다.
        // 손으로 설계한 주사선을 그대로 이미지로 만들 때 쓴다.
        public static void Columns(string path, int h, byte[] values)
        {
            int w = values.Length;
            byte[] buf = Alloc(w, h);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    Put(buf, w, x, y, values[x], values[x], values[x]);
            WriteBgra(path, w, h, buf);
        }

        // x 방향 매끄러운 선형 램프. 화소 x 의 회색값 = clamp(start + x*slope).
        // slope=1 이면 인접 화소 휘도차가 정확히 1 이다 — 「계단이 아닌 그라디언트」의 정본.
        public static void LinearRamp(string path, int w, int h, int start, int slope)
        {
            byte[] buf = Alloc(w, h);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int val = start + x * slope;
                    if (val < 0) val = 0; else if (val > 255) val = 255;
                    byte v = (byte)val;
                    Put(buf, w, x, y, v, v, v);
                }
            }
            WriteBgra(path, w, h, buf);
        }

        // 좌측 noiseStartX 화소는 균일 회색(=무지 면), 그 오른쪽은 화소별 난수(=텍스처 면).
        // 「무지 블록이 분모에 섞이면 전체 중앙값이 눌린다」를 손으로 계산 가능하게 만든다.
        // 블록 경계가 noiseStartX 에 정렬되도록 호출자가 8·32 의 배수를 고른다.
        public static void SolidWithNoiseRight(string path, int w, int h, byte gray, int noiseStartX, int seed)
        {
            Random rnd = new Random(seed);
            byte[] buf = Alloc(w, h);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (x < noiseStartX) Put(buf, w, x, y, gray, gray, gray);
                    else Put(buf, w, x, y, (byte)rnd.Next(256), (byte)rnd.Next(256), (byte)rnd.Next(256));
                }
            }
            WriteBgra(path, w, h, buf);
        }

        // 균일 회색 바탕에 회색 사각 하나. G-SLOT-A 의 「띠」를 손으로 설계할 때 쓴다.
        // 좌표는 **이미지 좌상단 원점**이다 (PNG 행 순서와 같다).
        public static void RectOnGray(string path, int w, int h, byte bg,
                                      int rx, int ry, int rw, int rh, byte fg)
        {
            byte[] buf = Alloc(w, h);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    Put(buf, w, x, y, bg, bg, bg);
            for (int y = ry; y < ry + rh; y++)
            {
                if (y < 0 || y >= h) continue;
                for (int x = rx; x < rx + rw; x++)
                {
                    if (x < 0 || x >= w) continue;
                    Put(buf, w, x, y, fg, fg, fg);
                }
            }
            WriteBgra(path, w, h, buf);
        }

        // 균일 회색 바탕에 임의 RGB 사각 하나. G-SLOT-B 의 색 조건을 손으로 설계할 때 쓴다.
        public static void ColorRectOnGray(string path, int w, int h, byte bg,
                                           int rx, int ry, int rw, int rh,
                                           byte cr, byte cg, byte cb)
        {
            byte[] buf = Alloc(w, h);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    Put(buf, w, x, y, bg, bg, bg);
            for (int y = ry; y < ry + rh; y++)
            {
                if (y < 0 || y >= h) continue;
                for (int x = rx; x < rx + rw; x++)
                {
                    if (x < 0 || x >= w) continue;
                    Put(buf, w, x, y, cr, cg, cb);
                }
            }
            WriteBgra(path, w, h, buf);
        }

        // 완전 검정 바탕에 좌상단 흰 사각. 비율은 호출자가 정수로 떨어지게 고른다.
        public static void BlackWithWhiteRect(string path, int w, int h, int rw, int rh)
        {
            byte[] buf = Alloc(w, h);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    Put(buf, w, x, y, 0, 0, 0);
            for (int y = 0; y < rh; y++)
                for (int x = 0; x < rw; x++)
                    Put(buf, w, x, y, 255, 255, 255);
            WriteBgra(path, w, h, buf);
        }
    }
}
'@

    Add-Type -TypeDefinition $cs -ReferencedAssemblies 'System.Drawing' -ErrorAction Stop
}

function Measure-CaptureImage {
    <#
    .SYNOPSIS
        PNG 한 장의 지표를 잰다. 결과는 CaptureMetrics.ImageResult.
    #>
    param(
        [Parameter(Mandatory)][string] $Path,
        [double] $ScanYFraction = $script:CM_DefaultScanYFraction,
        [double] $ScanXFraction = $script:CM_DefaultScanXFraction,
        [int]    $ScanLength    = $script:CM_DefaultScanLength,
        [int]    $FlatDelta     = $script:CM_DefaultFlatDelta,
        [int]    $StepMinLength = $script:CM_DefaultStepMinLength,
        [int]    $BoundaryMinDelta = $script:CM_DefaultBoundaryMinDelta,
        [double] $TexturedBlockStd = $script:CM_Classify.G1b_TexturedBlockStd,
        [double] $SharpBlockStd    = $script:CM_Classify.G1c_SharpBlockStd
    )
    Initialize-CaptureMetrics
    return [CaptureMetrics.Analyzer]::Analyze($Path, $ScanYFraction, $ScanXFraction, $ScanLength,
                                              $FlatDelta, $StepMinLength, $BoundaryMinDelta,
                                              $TexturedBlockStd, $SharpBlockStd)
}

# ══════════════════════════════════════════════════════════════════════════════
# G-4 세 갈래 판정 (2026-08-02 축 정정 — GRAPHICS_TARGET §5.4)
#
# 직전 판정은 두 갈래였다: 관측됨 / 미관측. 그래서 **조명이 없는 면 위를 지나는
# 주사선**이 「미관측」으로 세어졌다. 언릿 면에서 계단은 원리적으로 만들어지지
# 않으므로 그것은 「고치면 되는 것」이 아니라 「이 축으로는 잴 수 없는 것」이다.
# 둘을 한 숫자에 합치면 개선이 어디서 왔는지 알 수 없다.
#
# 측정 가능 판정은 **주사선 구간의 휘도 동적 범위**로 한다 — 조명 유무를 PNG 에서
# 직접 알 수는 없지만, 범위가 없는 구간에는 어떤 정의로도 계단이 없다.
# ══════════════════════════════════════════════════════════════════════════════
function Get-CaptureG4Verdict {
    <#
    .SYNOPSIS
        한 장의 G-4 판정을 'OBSERVED' | 'UNOBSERVED' | 'UNMEASURABLE' 로 낸다.
    #>
    param(
        [Parameter(Mandatory)] $Metric,
        [int] $StepsMin  = 3,
        [int] $BoundsMin = 2,
        [int] $MinSpan   = 8
    )
    # 측정 가능 여부를 **먼저** 본다. 동적 범위가 없는 구간에서 계단·단차가 우연히
    # 조건을 만족하는 경우까지 「관측됨」으로 세면 정정의 의미가 사라진다.
    if ($Metric.ScanSpan -lt $MinSpan) { return 'UNMEASURABLE' }
    if (($Metric.StepCount -ge $StepsMin) -and ($Metric.BoundaryCount -ge $BoundsMin)) { return 'OBSERVED' }
    return 'UNOBSERVED'
}

function Get-CaptureG4VerdictLabel {
    param([string] $Verdict)
    switch ($Verdict) {
        'OBSERVED'     { return '관측됨' }
        'UNOBSERVED'   { return '미관측' }
        'UNMEASURABLE' { return '측정불가' }
        default        { return $Verdict }
    }
}

# ══════════════════════════════════════════════════════════════════════════════
# 블록 std 히스토그램 — G-1b 임계가 실제로 두 집단을 가르는가
# ══════════════════════════════════════════════════════════════════════════════
function Get-CaptureBlockStdHistogram {
    <#
    .SYNOPSIS
        여러 장의 블록 std 히스토그램을 합산한다. 결과는 long[] (칸 수는 코어 상수).
    #>
    param([Parameter(Mandatory)] $Metrics)
    $arr = @($Metrics)
    if ($arr.Count -eq 0) { return @() }
    $bins = [int]$arr[0].BlockStdHistBins
    $acc = New-Object 'long[]' $bins
    foreach ($m in $arr) {
        $h = $m.BlockStdHist
        if ($null -eq $h) { continue }
        for ($i = 0; $i -lt $bins -and $i -lt $h.Length; $i++) { $acc[$i] += $h[$i] }
    }
    return $acc
}

function Get-CaptureHistogramValley {
    <#
    .SYNOPSIS
        히스토그램에서 봉우리 둘과 그 사이 골을 찾는다. 쌍봉이 아니면 IsBimodal=$false.

    .DESCRIPTION
        임계 4.0 을 「내가 정한 값」이 아니라 「실측이 가리키는 값」으로 검증하기 위한 것이다.
        골이 4.0 근처가 아니거나 쌍봉이 아니면 그 사실을 보고해야 한다 —
        임계를 결과에 맞춰 조용히 옮기지 않는다.
    #>
    param(
        [Parameter(Mandatory)][long[]] $Hist,
        [double] $BinWidth = 0.5,
        [int]    $SearchMaxBin = 40,      # 0 ~ 20.0 구간에서 찾는다
        [int]    $MinPeakSeparation = 4,  # 봉우리끼리 최소 2.0 떨어져 있어야 별개다
        [double] $SecondPeakMinRatio = 0.05
    )
    $n = [Math]::Min($SearchMaxBin + 1, $Hist.Length)
    if ($n -le 2) { return $null }

    $p1 = 0
    for ($i = 1; $i -lt $n; $i++) { if ($Hist[$i] -gt $Hist[$p1]) { $p1 = $i } }

    $p2 = -1
    for ($i = 0; $i -lt $n; $i++) {
        if ([Math]::Abs($i - $p1) -lt $MinPeakSeparation) { continue }
        if ($p2 -lt 0 -or $Hist[$i] -gt $Hist[$p2]) { $p2 = $i }
    }
    if ($p2 -lt 0) { return $null }

    $lo = [Math]::Min($p1, $p2); $hi = [Math]::Max($p1, $p2)
    $valley = $lo
    for ($i = $lo; $i -le $hi; $i++) { if ($Hist[$i] -lt $Hist[$valley]) { $valley = $i } }

    $bimodal = ($Hist[$p1] -gt 0) -and (($Hist[$p2] / [double]$Hist[$p1]) -ge $SecondPeakMinRatio) -and
               ($Hist[$valley] -lt $Hist[$p2])

    return [pscustomobject]@{
        Peak1Bin    = $p1
        Peak1Value  = $p1 * $BinWidth
        Peak1Count  = $Hist[$p1]
        Peak2Bin    = $p2
        Peak2Value  = $p2 * $BinWidth
        Peak2Count  = $Hist[$p2]
        ValleyBin   = $valley
        ValleyValue = $valley * $BinWidth
        ValleyCount = $Hist[$valley]
        IsBimodal   = $bimodal
    }
}

function Format-CaptureBlockStdHistogram {
    <#
    .SYNOPSIS
        히스토그램을 콘솔 표 문자열 배열로 만든다. 표시 상한 위쪽은 한 칸으로 묶는다.
    #>
    param(
        [Parameter(Mandatory)][long[]] $Hist,
        [double] $BinWidth = 0.5,
        [int]    $DetailMaxBin = 24,      # 0 ~ 12.0 까지는 0.5 폭 그대로 보여준다
        [double[]] $Marks = @(4.0, 8.0),
        [int]    $BarWidth = 46
    )
    $lines = New-Object System.Collections.ArrayList
    $total = 0L
    foreach ($v in $Hist) { $total += $v }
    if ($total -le 0) { $null = $lines.Add('  (블록 없음)'); return $lines }

    $detail = [Math]::Min($DetailMaxBin, $Hist.Length - 1)
    $peak = 0L
    for ($i = 0; $i -le $detail; $i++) { if ($Hist[$i] -gt $peak) { $peak = $Hist[$i] } }
    $rest = 0L
    for ($i = $detail + 1; $i -lt $Hist.Length; $i++) { $rest += $Hist[$i] }
    if ($rest -gt $peak) { $peak = $rest }
    if ($peak -le 0) { $peak = 1 }

    $null = $lines.Add(('  {0,-13} {1,12} {2,8}   {3}' -f 'std 구간', '블록수', '비율', '분포'))
    $null = $lines.Add('  ' + ('-' * 88))
    for ($i = 0; $i -le $detail; $i++) {
        $lo = $i * $BinWidth
        $hi = $lo + $BinWidth
        $bar = '#' * [int][Math]::Round($BarWidth * $Hist[$i] / $peak)
        $mark = ''
        foreach ($mk in $Marks) { if ([Math]::Abs($lo - $mk) -lt 1e-9) { $mark = ' ← 임계 ' + ('{0:F1}' -f $mk) } }
        $null = $lines.Add(('  {0,5:F1}~{1,-6:F1} {2,12} {3,7:F2}%   {4}{5}' -f `
            $lo, $hi, $Hist[$i], (100.0 * $Hist[$i] / $total), $bar, $mark))
    }
    $restLo = ($detail + 1) * $BinWidth
    $bar = '#' * [int][Math]::Round($BarWidth * $rest / $peak)
    $null = $lines.Add(('  {0,5:F1}~{1,-6} {2,12} {3,7:F2}%   {4}' -f $restLo, '위', $rest, (100.0 * $rest / $total), $bar))
    $null = $lines.Add(('  총 블록 {0:N0} 개' -f $total))
    return $lines
}

function Format-CaptureG1b {
    <#
    .SYNOPSIS
        G-1b(텍스처 블록 중앙값)의 표시 문자열. 텍스처 블록이 0개면 「정의불가」다.

    .DESCRIPTION
        0 을 내면 「텍스처가 있는데 아주 평평하다」와 「텍스처 블록이 아예 없다」가
        같은 숫자가 된다. 그 둘은 완전히 다른 상태이므로 같은 칸에 넣지 않는다.
        CSV 에는 빈 칸으로 쓴다 — 숫자 열에 0 을 넣으면 집계에 섞인다.
    #>
    param($Value, [int] $Count)
    if ($Count -le 0) { return '정의불가' }
    if ($null -eq $Value) { return '정의불가' }
    if ([double]::IsNaN([double]$Value)) { return '정의불가' }
    return ('{0:F2}' -f [double]$Value)
}

function Get-CaptureMedian {
    <#
    .SYNOPSIS
        PowerShell 쪽 중앙값. 세트 집계(대표 8장)에 쓴다.
    #>
    param([double[]] $Values)
    if ($null -eq $Values -or $Values.Count -eq 0) { return [double]::NaN }
    $s = @($Values | Sort-Object)
    $n = $s.Count
    if ($n % 2 -eq 1) { return [double]$s[[int](($n - 1) / 2)] }
    return ([double]$s[$n / 2 - 1] + [double]$s[$n / 2]) / 2.0
}

function Test-CaptureRepresentative {
    <#
    .SYNOPSIS
        파일 이름의 숫자 접두사가 대표 8장에 속하는가.
    #>
    param([string] $Name)
    if ($Name -match '^(\d{2})') { return $script:CM_RepresentativePrefixes -contains $Matches[1] }
    return $false
}

function Write-Utf8Bom {
    <#
    .SYNOPSIS
        BOM 있는 UTF-8 로 텍스트 파일을 쓴다. Windows PowerShell 5.1 관례.
    #>
    param([string] $Path, [string[]] $Lines)
    $enc = New-Object System.Text.UTF8Encoding($true)
    [System.IO.File]::WriteAllText($Path, (($Lines -join "`r`n") + "`r`n"), $enc)
}
