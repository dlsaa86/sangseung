using System;
using System.Collections.Generic;
using System.IO;

namespace Ascend.CaptureHarness
{
    /// <summary>
    /// 애니메이션 GIF 를 직접 쓴다. 외부 패키지도, 유료 에셋도, ffmpeg 도 쓰지 않는다.
    ///
    /// 왜 필요한가: `MASTER_PRD.md` §13.5 가 증거 산출물로 "핵심 흐름 **영상**"을 요구하는데
    /// 캡처 하네스는 정지 이미지만 만든다. 5연쇄와 Critical→과수확→결과는
    /// **시간 축이 있어야 성립하는 장면**이라 한 장으로는 증거가 되지 않는다.
    ///
    /// 왜 GIF 인가: Unity Recorder 패키지를 추가하려면 사용자 승인이 필요하고
    /// (`CLAUDE.md` — 패키지를 임의로 바꾸지 않는다), ffmpeg 은 이 기기에 있으리라는 보장이 없다.
    /// GIF 는 형식이 완결돼 있고 의존성이 0이며 어디서나 재생된다.
    /// 화질이 목적이 아니다 — **무엇이 일어났는지 따라갈 수 있으면 된다.**
    ///
    /// 팔레트는 고정 256색이다(6×6×6 RGB 큐브 216색 + 회색 40단계).
    /// 프레임마다 색을 다시 고르는 방식이 화질은 낫지만, **같은 런이 같은 파일을 만들어야**
    /// 회귀 비교가 가능하다(하네스의 존재 이유). 고정 팔레트는 그것을 공짜로 준다.
    /// 그레이박스 화면은 회색조가 대부분이라 회색 40단계가 실제로 크게 기여한다.
    /// </summary>
    public static class GifEncoder
    {
        private const int PaletteSize = 256;
        private const int CubeLevels = 6;               // 6^3 = 216
        private const int CubeCount = CubeLevels * CubeLevels * CubeLevels;
        private const int GrayCount = PaletteSize - CubeCount;   // 40

        /// <summary>
        /// 프레임들을 하나의 애니메이션 GIF 로 쓴다.
        /// </summary>
        /// <param name="path">쓸 파일 경로. 상위 디렉터리는 만들어 준다.</param>
        /// <param name="frames">
        /// 프레임별 RGB24 픽셀. 길이는 width*height*3 이어야 한다.
        /// **위에서 아래로** 정렬돼 있다고 본다 — Unity 의 `Texture2D.GetPixels32` 는
        /// 아래에서 위이므로 호출자가 뒤집어 넘긴다(<see cref="FromBottomUpRgba"/> 참고).
        /// </param>
        /// <param name="width">픽셀 너비.</param>
        /// <param name="height">픽셀 높이.</param>
        /// <param name="delayCentiseconds">프레임 간 지연(1/100초). GIF 형식의 단위다.</param>
        /// <param name="loop">무한 반복할 것인가.</param>
        public static void Write(string path, IReadOnlyList<byte[]> frames,
            int width, int height, int delayCentiseconds, bool loop = true)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("path is empty", nameof(path));
            if (frames == null || frames.Count == 0) throw new ArgumentException("no frames", nameof(frames));
            if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));

            int expected = width * height * 3;
            for (int i = 0; i < frames.Count; i++)
            {
                if (frames[i] == null || frames[i].Length != expected)
                    throw new ArgumentException(
                        $"frame {i} has {frames[i]?.Length ?? -1} bytes, expected {expected} (width*height*3)");
            }

            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            byte[] palette = BuildPalette();
            byte[] indexed = new byte[width * height];

            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                WriteHeader(stream, width, height);
                WriteGlobalColorTable(stream, palette);
                if (loop) WriteNetscapeLoop(stream);

                for (int f = 0; f < frames.Count; f++)
                {
                    Quantize(frames[f], indexed);
                    WriteGraphicControl(stream, delayCentiseconds);
                    WriteImageDescriptor(stream, width, height);
                    WriteLzwImageData(stream, indexed);
                }

                stream.WriteByte(0x3B);   // trailer
            }
        }

        /// <summary>
        /// Unity 의 아래→위 RGBA32 픽셀을 이 인코더가 기대하는 위→아래 RGB24 로 바꾼다.
        ///
        /// 이 변환을 호출자에게 맡기면 매번 다시 틀린다 — 뒤집힌 GIF 는 "화면이 뒤집혔다"가
        /// 아니라 "연출이 이상하다"로 오독되기 쉽다.
        /// </summary>
        public static byte[] FromBottomUpRgba(byte[] rgba, int width, int height)
        {
            if (rgba == null) throw new ArgumentNullException(nameof(rgba));
            if (rgba.Length < width * height * 4)
                throw new ArgumentException($"rgba has {rgba.Length} bytes, need {width * height * 4}");

            var rgb = new byte[width * height * 3];
            for (int y = 0; y < height; y++)
            {
                int src = (height - 1 - y) * width * 4;
                int dst = y * width * 3;
                for (int x = 0; x < width; x++)
                {
                    rgb[dst]     = rgba[src];
                    rgb[dst + 1] = rgba[src + 1];
                    rgb[dst + 2] = rgba[src + 2];
                    src += 4;
                    dst += 3;
                }
            }
            return rgb;
        }

        // ── 팔레트 ────────────────────────────────────────────────────────────────

        private static byte[] BuildPalette()
        {
            var p = new byte[PaletteSize * 3];
            int i = 0;
            for (int r = 0; r < CubeLevels; r++)
            for (int g = 0; g < CubeLevels; g++)
            for (int b = 0; b < CubeLevels; b++)
            {
                p[i++] = (byte)(r * 255 / (CubeLevels - 1));
                p[i++] = (byte)(g * 255 / (CubeLevels - 1));
                p[i++] = (byte)(b * 255 / (CubeLevels - 1));
            }
            // 남은 자리는 회색조. 어두운 산업 공간이라 회색 해상도가 화질을 좌우한다.
            for (int k = 0; k < GrayCount; k++)
            {
                byte v = (byte)(k * 255 / (GrayCount - 1));
                p[i++] = v; p[i++] = v; p[i++] = v;
            }
            return p;
        }

        /// <summary>
        /// 픽셀 하나를 팔레트 인덱스로. 큐브 후보와 회색 후보 중 오차가 작은 쪽을 고른다.
        /// 정확한 최근접 탐색(256회 비교)을 하지 않는 이유는 프레임당 수십만 픽셀이기 때문이다 —
        /// 두 후보 비교로 충분히 가깝고, 무엇보다 **결정론적**이다.
        /// </summary>
        private static void Quantize(byte[] rgb, byte[] indexed)
        {
            for (int i = 0, p = 0; i < indexed.Length; i++, p += 3)
            {
                int r = rgb[p], g = rgb[p + 1], b = rgb[p + 2];

                int cr = (r * (CubeLevels - 1) + 127) / 255;
                int cg = (g * (CubeLevels - 1) + 127) / 255;
                int cb = (b * (CubeLevels - 1) + 127) / 255;
                int cubeIndex = (cr * CubeLevels + cg) * CubeLevels + cb;
                int qr = cr * 255 / (CubeLevels - 1);
                int qg = cg * 255 / (CubeLevels - 1);
                int qb = cb * 255 / (CubeLevels - 1);
                int cubeError = Sq(r - qr) + Sq(g - qg) + Sq(b - qb);

                int luma = (r * 299 + g * 587 + b * 114) / 1000;
                int gk = (luma * (GrayCount - 1) + 127) / 255;
                int gv = gk * 255 / (GrayCount - 1);
                int grayError = Sq(r - gv) + Sq(g - gv) + Sq(b - gv);

                indexed[i] = grayError < cubeError
                    ? (byte)(CubeCount + gk)
                    : (byte)cubeIndex;
            }
        }

        private static int Sq(int v) => v * v;

        // ── GIF 블록 ──────────────────────────────────────────────────────────────

        private static void WriteHeader(Stream s, int width, int height)
        {
            s.WriteByte((byte)'G'); s.WriteByte((byte)'I'); s.WriteByte((byte)'F');
            s.WriteByte((byte)'8'); s.WriteByte((byte)'9'); s.WriteByte((byte)'a');
            WriteShort(s, width);
            WriteShort(s, height);
            // 전역 색표 있음(0x80) | 색 해상도 7 (0x70) | 크기 7 → 256색
            s.WriteByte(0x80 | 0x70 | 0x07);
            s.WriteByte(0);     // 배경색 인덱스
            s.WriteByte(0);     // 픽셀 종횡비
        }

        private static void WriteGlobalColorTable(Stream s, byte[] palette) =>
            s.Write(palette, 0, palette.Length);

        private static void WriteNetscapeLoop(Stream s)
        {
            s.WriteByte(0x21); s.WriteByte(0xFF); s.WriteByte(0x0B);
            byte[] app = { (byte)'N', (byte)'E', (byte)'T', (byte)'S', (byte)'C', (byte)'A',
                           (byte)'P', (byte)'E', (byte)'2', (byte)'.', (byte)'0' };
            s.Write(app, 0, app.Length);
            s.WriteByte(0x03); s.WriteByte(0x01);
            WriteShort(s, 0);   // 0 = 무한 반복
            s.WriteByte(0x00);
        }

        private static void WriteGraphicControl(Stream s, int delayCentiseconds)
        {
            s.WriteByte(0x21); s.WriteByte(0xF9); s.WriteByte(0x04);
            s.WriteByte(0x00);  // 처리 방식 없음, 투명 없음
            WriteShort(s, Math.Max(1, delayCentiseconds));
            s.WriteByte(0x00);  // 투명색 인덱스 (쓰지 않음)
            s.WriteByte(0x00);
        }

        private static void WriteImageDescriptor(Stream s, int width, int height)
        {
            s.WriteByte(0x2C);
            WriteShort(s, 0); WriteShort(s, 0);
            WriteShort(s, width); WriteShort(s, height);
            s.WriteByte(0x00);  // 지역 색표 없음, 인터레이스 없음
        }

        private static void WriteShort(Stream s, int value)
        {
            s.WriteByte((byte)(value & 0xFF));
            s.WriteByte((byte)((value >> 8) & 0xFF));
        }

        // ── LZW ───────────────────────────────────────────────────────────────────

        /// <summary>
        /// GIF 의 가변 코드 길이 LZW.
        ///
        /// **코드 폭을 언제 올리는가가 이 함수의 전부다.** 디코더는 인코더보다
        /// 사전 항목 하나만큼 뒤처진다 — 인코더는 코드를 내보낼 때마다 항목을 만들지만
        /// 디코더는 Clear 직후의 첫 코드에서는 항목을 만들지 않기 때문이다.
        /// 그래서 인코더가 폭을 즉시 올리면 디코더가 아직 9비트로 읽고 있는 코드를
        /// 10비트로 써 버리고, **그 지점부터 파일 전체가 쓰레기가 된다.**
        ///
        /// 처음에 그렇게 만들었고, 같은 알고리즘을 파이썬으로 옮겨 Pillow 로 되읽는
        /// 왕복 검사에서 `broken data stream` 으로 잡혔다. 눈으로는 찾을 수 없는 종류의
        /// 어긋남이다 — 첫 511개 코드까지는 완벽하게 맞기 때문이다.
        ///
        /// 올바른 순서: **코드를 내보낸 뒤, 증가 전 nextCode 로 판정한다.**
        /// compress.c 계열 참조 구현의 output() 과 같다. GIF 는 TIFF 와 달리
        /// early change 를 쓰지 않는다.
        /// </summary>
        private static void WriteLzwImageData(Stream s, byte[] indexed)
        {
            const int minCodeSize = 8;
            const int clearCode = 1 << minCodeSize;          // 256
            const int endCode = clearCode + 1;               // 257

            s.WriteByte(minCodeSize);

            var packer = new BlockPacker(s);
            var dictionary = new Dictionary<int, int>(4096);
            int nextCode = endCode + 1;
            int codeSize = minCodeSize + 1;

            void Emit(int code)
            {
                packer.Write(code, codeSize);
                if (nextCode > (1 << codeSize) - 1 && codeSize < 12) codeSize++;
            }

            Emit(clearCode);

            int prefix = indexed[0];
            for (int i = 1; i < indexed.Length; i++)
            {
                int suffix = indexed[i];
                int key = (prefix << 8) | suffix;

                if (dictionary.TryGetValue(key, out int found))
                {
                    prefix = found;
                    continue;
                }

                Emit(prefix);

                if (nextCode < 4096)
                {
                    dictionary[key] = nextCode;
                    nextCode++;
                }
                else
                {
                    // 사전이 찼다. Clear 는 12비트로 나가고(폭은 아직 12다) 그 뒤에 초기화한다.
                    Emit(clearCode);
                    dictionary.Clear();
                    nextCode = endCode + 1;
                    codeSize = minCodeSize + 1;
                }

                prefix = suffix;
            }

            Emit(prefix);
            Emit(endCode);
            packer.Flush();
            s.WriteByte(0x00);   // 블록 종료
        }

        /// <summary>
        /// 비트를 모아 255바이트 이하의 서브블록으로 흘려보낸다.
        /// GIF 는 이미지 데이터를 길이 접두 서브블록으로 나눠 담는다.
        /// </summary>
        private sealed class BlockPacker
        {
            private readonly Stream _stream;
            private readonly byte[] _block = new byte[255];
            private int _blockLength;
            private int _bitBuffer;
            private int _bitCount;

            public BlockPacker(Stream stream) { _stream = stream; }

            public void Write(int code, int codeSize)
            {
                _bitBuffer |= code << _bitCount;
                _bitCount += codeSize;
                while (_bitCount >= 8)
                {
                    PushByte((byte)(_bitBuffer & 0xFF));
                    _bitBuffer >>= 8;
                    _bitCount -= 8;
                }
            }

            public void Flush()
            {
                if (_bitCount > 0)
                {
                    PushByte((byte)(_bitBuffer & 0xFF));
                    _bitBuffer = 0;
                    _bitCount = 0;
                }
                FlushBlock();
            }

            private void PushByte(byte value)
            {
                _block[_blockLength++] = value;
                if (_blockLength == 255) FlushBlock();
            }

            private void FlushBlock()
            {
                if (_blockLength == 0) return;
                _stream.WriteByte((byte)_blockLength);
                _stream.Write(_block, 0, _blockLength);
                _blockLength = 0;
            }
        }
    }
}
