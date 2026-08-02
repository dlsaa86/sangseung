<#
.SYNOPSIS
    캡처 PNG 지표 계산 코어. `tools/capture-metrics.ps1` 과 `selftest.ps1` 이 공유한다.

.DESCRIPTION
    docs/GRAPHICS_TARGET.md §2 의 측정 축을 픽셀에서 직접 잰다.

      G-1  국소 분산   — 8×8 블록 휘도 표준편차의 중앙값
      G-2  휘도 분포   — 5 / 50 / 95 퍼센타일
      G-3  발광        — 휘도 ≥ 200 화소 비율
      G-4  평탄 구간   — 지정 주사선에서 인접 화소 휘도차 ≤ 1 인 연속 구간 수·최장
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
    G1_LocalStdMedian   = 12.0   # G-1 대표 8장의 국소 분산 중앙값 ≥ 12.0
    G2_P5Max            = 24     # G-2 휘도 5퍼센타일 ≤ 24
    G2_P50Min           = 36     # G-2 휘도 중앙값 36 ~ 96
    G2_P50Max           = 96
    G2_P95Min           = 170    # G-2 휘도 95퍼센타일 ≥ 170
    G3_GlowMinPct       = 1.0    # G-3 발광 화소 비율 1.0% ~ 6.0%
    G3_GlowMaxPct       = 6.0
    G4_FlatRunCountMax  = 24     # G-4 평탄 구간 ≤ 24개
    G4_FlatRunLongMin   = 20     # G-4 최장 평탄 구간 ≥ 20px
    G4_StairFramesMin   = 12     # G-4 24장 중 계단이 관측되는 장 ≥ 12
    G5_EmptyPlaneMaxPct = 18.0   # G-5 빈 평면 비율 ≤ 18%
    MagentaMax          = 0      # 셰이더 오류 색은 0 이어야 한다
}

# 대표 8장 — GRAPHICS_TARGET §2 「대표 8장은 01·02·06·09·12·15·18·21」
$script:CM_RepresentativePrefixes = @('01','02','06','09','12','15','18','21')

# 주사선 기본값. G-4 는 「지정된 주사선」을 재므로 위치가 지표의 일부다.
#   y : 이미지 높이의 43.5% 지점  (GRAPHICS_TARGET §2 G-4 기본값)
#   x : 이미지 폭의 10% 지점부터 200px  (좌벽 — G-2 회귀 감시축과 같은 벽면)
$script:CM_DefaultScanYFraction = 0.435
$script:CM_DefaultScanXFraction = 0.10
$script:CM_DefaultScanLength    = 200

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

        public double LocalStdMedian;      // G-1  8x8 블록 표준편차의 중앙값
        public int    LumP5;               // G-2
        public int    LumP50;
        public int    LumP95;
        public double GlowPercent;         // G-3  휘도 >= 200 화소 비율(%)

        public int    ScanY;               // G-4  실제로 잰 주사선
        public int    ScanX0;
        public int    ScanLen;
        public int    FlatRunCount;
        public int    FlatRunLongest;

        public double EmptyPlanePercent;   // G-5  32x32 블록 중 std < 4 인 블록 비율(%)

        public long   GoldPixels;
        public long   MagentaPixels;

        public int    BlockCount8;
        public int    BlockCount32;
    }

    public static class Analyzer
    {
        public const int BlockG1 = 8;
        public const int BlockG5 = 32;
        public const double EmptyPlaneStdThreshold = 4.0;
        public const int GlowLuminance = 200;
        public const double FlatDelta = 1.0;

        public static ImageResult Analyze(string path, double scanYFrac, double scanXFrac, int scanLen)
        {
            ImageResult r = new ImageResult();
            r.Path = path;
            r.Name = System.IO.Path.GetFileNameWithoutExtension(path);

            int w, h;
            double[] lum;
            long gold = 0;
            long magenta = 0;
            long[] hist = new long[256];

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

                        int q = (int)Math.Round(L, MidpointRounding.AwayFromZero);
                        if (q < 0) q = 0; else if (q > 255) q = 255;
                        hist[q]++;

                        if (rr - b >= 60 && g - b >= 30) gold++;
                        if (rr > 200 && b > 200 && g < 80) magenta++;
                    }
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

            // ── G-1 국소 분산 ───────────────────────────────────────────────
            int n8;
            double[] std8 = BlockStdDevs(lum, w, h, BlockG1, out n8);
            r.BlockCount8 = n8;
            r.LocalStdMedian = Median(std8, n8);

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

            int runs = 0;
            int longest = 0;
            if (len > 0)
            {
                int baseIdx = scanY * w + x0;
                int cur = 1;
                for (int i = 1; i < len; i++)
                {
                    double d = lum[baseIdx + i] - lum[baseIdx + i - 1];
                    if (d < 0) d = -d;
                    if (d <= FlatDelta) { cur++; }
                    else { runs++; if (cur > longest) longest = cur; cur = 1; }
                }
                runs++;
                if (cur > longest) longest = cur;
            }
            r.FlatRunCount = runs;
            r.FlatRunLongest = longest;

            return r;
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
        [int]    $ScanLength    = $script:CM_DefaultScanLength
    )
    Initialize-CaptureMetrics
    return [CaptureMetrics.Analyzer]::Analyze($Path, $ScanYFraction, $ScanXFraction, $ScanLength)
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
