// HHSymbolArt.cs — 문양 2D 스프라이트를 코드로 그린다.
// 설계자 지시(2026-08-25): "문양은 3D 메시 말고 우선 2D로".
// 외부 이미지 없이 절차적으로 실루엣을 찍어 스프라이트로 만든다 — 나중에 그림으로 갈아끼우기 쉽게
// 같은 규격(정사각 · 알파 실루엣 · 흰색)으로 통일했다.
using System.Collections.Generic;
using UnityEngine;

namespace HeavensHunger
{
    public static class HHSymbolArt
    {
        const int SIZE = 160;
        static readonly Dictionary<SymKind, Sprite> _cache = new Dictionary<SymKind, Sprite>();

        public static Sprite Get(SymKind k)
        {
            Sprite s;
            if (_cache.TryGetValue(k, out s) && s != null) return s;
            var tex = Bake(k);
            s = Sprite.Create(tex, new Rect(0, 0, SIZE, SIZE), new Vector2(0.5f, 0.5f), SIZE);
            s.name = "HHSym_" + k;
            s.hideFlags = HideFlags.DontSave;
            _cache[k] = s;
            return s;
        }

        static Sprite _ring;
        /// <summary>당첨 칸 테두리 — 꽉 찬 사각형이 아니라 테두리여야 문양이 안 가려진다.</summary>
        public static Sprite Ring()
        {
            if (_ring != null) return _ring;
            var tex = new Texture2D(SIZE, SIZE, TextureFormat.RGBA32, false) { name = "HHRingTex" };
            tex.hideFlags = HideFlags.DontSave;
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            var px = new Color32[SIZE * SIZE];
            for (int y = 0; y < SIZE; y++)
                for (int x = 0; x < SIZE; x++)
                {
                    float u = (x + 0.5f) / SIZE * 2f - 1f;
                    float v = (y + 0.5f) / SIZE * 2f - 1f;
                    float outer = Box(u, v, 0, 0, 0.96f, 0.96f, 0.16f);
                    float inner = Box(u, v, 0, 0, 0.82f, 0.82f, 0.14f);
                    float d = S(outer, inner);              // 테두리만 남긴다
                    // 네 모서리 가이드
                    float corner = 1f;
                    for (int sx = -1; sx <= 1; sx += 2)
                        for (int sy = -1; sy <= 1; sy += 2)
                            corner = Mathf.Min(corner, Box(u, v, sx * 0.80f, sy * 0.80f, 0.22f, 0.22f, 0.05f));
                    d = U(d, S(corner, inner));
                    float a = Mathf.Clamp01(0.5f - d / 0.05f);
                    px[y * SIZE + x] = new Color(1f, 1f, 1f, a);
                }
            tex.SetPixels32(px);
            tex.Apply();
            _ring = Sprite.Create(tex, new Rect(0, 0, SIZE, SIZE), new Vector2(0.5f, 0.5f), SIZE);
            _ring.name = "HH_Ring";
            _ring.hideFlags = HideFlags.DontSave;
            return _ring;
        }

        static Texture2D Bake(SymKind k)
        {
            var tex = new Texture2D(SIZE, SIZE, TextureFormat.RGBA32, false) { name = "HHSymTex_" + k };
            tex.hideFlags = HideFlags.DontSave;
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            var px = new Color32[SIZE * SIZE];
            for (int y = 0; y < SIZE; y++)
                for (int x = 0; x < SIZE; x++)
                {
                    // -1..1 정규 좌표 (위가 +y)
                    float u = (x + 0.5f) / SIZE * 2f - 1f;
                    float v = (y + 0.5f) / SIZE * 2f - 1f;
                    float d = Shape(k, u, v);          // <0 이면 안쪽
                    // 안티에일리어싱: 경계 0.03 폭
                    float a = Mathf.Clamp01(0.5f - d / 0.045f);
                    // 안쪽은 살짝 밝게, 가장자리는 어둡게 — 판형 느낌
                    float shade = Mathf.Clamp01(0.72f + (-d) * 2.4f);
                    px[y * SIZE + x] = new Color(shade, shade, shade, a);
                }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        // ── 원시 도형 SDF ──
        static float Circle(float x, float y, float cx, float cy, float r)
        { return Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r; }

        static float Box(float x, float y, float cx, float cy, float w, float h, float round)
        {
            float dx = Mathf.Abs(x - cx) - (w - round);
            float dy = Mathf.Abs(y - cy) - (h - round);
            float ox = Mathf.Max(dx, 0), oy = Mathf.Max(dy, 0);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(dx, dy), 0) - round;
        }

        static float Ellipse(float x, float y, float cx, float cy, float rx, float ry)
        {
            float ax = (x - cx) / rx, ay = (y - cy) / ry;
            return (Mathf.Sqrt(ax * ax + ay * ay) - 1f) * Mathf.Min(rx, ry);
        }

        static float Capsule(float x, float y, float ax, float ay, float bx, float by, float r)
        {
            float pax = x - ax, pay = y - ay, bax = bx - ax, bay = by - ay;
            float h = Mathf.Clamp01((pax * bax + pay * bay) / (bax * bax + bay * bay));
            float dx = pax - bax * h, dy = pay - bay * h;
            return Mathf.Sqrt(dx * dx + dy * dy) - r;
        }

        static float U(float a, float b) { return Mathf.Min(a, b); }          // 합집합
        static float S(float a, float b) { return Mathf.Max(a, -b); }         // 차집합 (a에서 b를 뺀다)

        /// <summary>문양 실루엣. 값이 음수면 안쪽.</summary>
        static float Shape(SymKind k, float x, float y)
        {
            switch (k)
            {
                // ── 살(문양) 7종 ──
                case SymKind.TOOTH:   // 어금니: 관 + 두 뿌리
                    {
                        float crown = Box(x, y, 0f, 0.30f, 0.46f, 0.34f, 0.18f);
                        float rootL = Capsule(x, y, -0.24f, 0.02f, -0.30f, -0.62f, 0.14f);
                        float rootR = Capsule(x, y, 0.24f, 0.02f, 0.30f, -0.62f, 0.14f);
                        return U(crown, U(rootL, rootR));
                    }
                case SymKind.BONE:    // 뼈: 막대 + 네 혹
                    {
                        float bar = Capsule(x, y, -0.34f, -0.18f, 0.34f, 0.18f, 0.13f);
                        float k1 = U(Circle(x, y, -0.46f, -0.06f, 0.19f), Circle(x, y, -0.30f, -0.34f, 0.19f));
                        float k2 = U(Circle(x, y, 0.46f, 0.06f, 0.19f), Circle(x, y, 0.30f, 0.34f, 0.19f));
                        return U(bar, U(k1, k2));
                    }
                case SymKind.EAR:     // 귀: 바깥 고리 + 안쪽 소용돌이
                    {
                        float outer = Ellipse(x, y, -0.02f, 0.02f, 0.50f, 0.66f);
                        float hole = Ellipse(x, y, 0.10f, 0.00f, 0.26f, 0.40f);
                        float lobe = Circle(x, y, -0.06f, -0.60f, 0.20f);
                        float ring = S(outer, hole);
                        float inner = Ellipse(x, y, 0.02f, 0.10f, 0.16f, 0.24f);
                        return U(U(ring, lobe), inner);
                    }
                case SymKind.TONGUE:  // 혀: 둥근 잎 + 가운데 홈
                    {
                        float body = Ellipse(x, y, 0f, -0.05f, 0.42f, 0.62f);
                        float groove = Capsule(x, y, 0f, 0.42f, 0f, -0.30f, 0.045f);
                        return S(body, groove);
                    }
                case SymKind.HEART:   // 심장: 고전 하트 + 대동맥
                    {
                        float xx = x * 1.12f, yy = (y - 0.10f) * 1.12f;
                        float t = xx * xx + yy * yy - 0.36f;
                        float h = t * t * t - xx * xx * yy * yy * yy;   // 하트 음함수
                        float heart = h * 1.6f;
                        float aorta = Capsule(x, y, -0.06f, 0.44f, -0.18f, 0.74f, 0.09f);
                        return U(heart, aorta);
                    }
                case SymKind.BRAIN:   // 뇌: 반구 두 개 + 주름
                    {
                        float body = Ellipse(x, y, 0f, 0.06f, 0.56f, 0.48f);
                        float stem = Capsule(x, y, 0.02f, -0.30f, 0.10f, -0.66f, 0.11f);
                        float sulcus = 1f;
                        for (int i = -1; i <= 1; i++)
                        {
                            float cy = 0.06f + i * 0.24f;
                            float w = Mathf.Sin((x + 1f) * 7.5f) * 0.055f;
                            sulcus = Mathf.Min(sulcus, Mathf.Abs(y - (cy + w)) - 0.028f);
                        }
                        float mid = Mathf.Abs(x - 0.0f) - 0.028f;
                        return U(S(body, U(sulcus, mid)), stem);
                    }
                case SymKind.LUNG:    // 폐: 두 엽 + 기관
                    {
                        float trachea = Capsule(x, y, 0f, 0.78f, 0f, 0.12f, 0.085f);
                        float bl = Capsule(x, y, 0f, 0.18f, -0.30f, 0.06f, 0.07f);
                        float br = Capsule(x, y, 0f, 0.18f, 0.30f, 0.06f, 0.07f);
                        float lobeL = Ellipse(x, y, -0.32f, -0.24f, 0.28f, 0.46f);
                        float lobeR = Ellipse(x, y, 0.32f, -0.24f, 0.28f, 0.46f);
                        return U(U(trachea, U(bl, br)), U(lobeL, lobeR));
                    }

                // ── 장치 7종 (기하 아이콘) ──
                case SymKind.CAP:     // 축전지: 배터리 몸통 + 단자
                    {
                        float body = Box(x, y, 0f, -0.04f, 0.40f, 0.52f, 0.10f);
                        float cap = Box(x, y, 0f, 0.54f, 0.16f, 0.10f, 0.04f);
                        float bolt = Capsule(x, y, -0.10f, 0.24f, 0.02f, 0.00f, 0.075f);
                        float bolt2 = Capsule(x, y, 0.02f, 0.00f, -0.02f, -0.30f, 0.075f);
                        return U(S(body, U(bolt, bolt2)), cap);
                    }
                case SymKind.TRANS:   // 변압기: 번개
                    {
                        float a = Capsule(x, y, 0.16f, 0.72f, -0.16f, 0.02f, 0.12f);
                        float b = Capsule(x, y, -0.16f, 0.02f, 0.20f, 0.02f, 0.12f);
                        float c = Capsule(x, y, 0.20f, 0.02f, -0.10f, -0.72f, 0.12f);
                        return U(a, U(b, c));
                    }
                case SymKind.AMP:     // 안테나: 기둥 + 세 호
                    {
                        float mast = Capsule(x, y, 0f, 0.30f, 0f, -0.70f, 0.075f);
                        float dish = 1f;
                        for (int i = 1; i <= 3; i++)
                        {
                            float r = 0.20f + i * 0.16f;
                            float ring = Mathf.Abs(Circle(x, y, 0f, 0.28f, r)) - 0.045f;
                            if (y < 0.28f) ring = 1f;
                            dish = Mathf.Min(dish, ring);
                        }
                        return U(mast, dish);
                    }
                case SymKind.CORE:    // 융합 코어: 다이아 + 안쪽 다이아
                    {
                        float outer = Mathf.Abs(x) + Mathf.Abs(y) - 0.70f;
                        float inner = Mathf.Abs(x) + Mathf.Abs(y) - 0.40f;
                        float ring = S(outer, Mathf.Abs(x) + Mathf.Abs(y) - 0.54f);
                        return U(ring, inner);
                    }
                case SymKind.FURN:    // 소각로: 불꽃
                    {
                        float body = Ellipse(x, y, 0f, -0.22f, 0.42f, 0.42f);
                        float tip = Capsule(x, y, 0.0f, 0.10f, 0.06f, 0.72f, 0.16f);
                        float notch = Ellipse(x, y, -0.34f, 0.34f, 0.30f, 0.34f);
                        return S(U(body, tip), notch);
                    }
                case SymKind.LID:     // 눈꺼풀: 감긴 눈
                    {
                        float lid = Mathf.Abs(y + 0.02f - 0.30f * (1f - x * x)) - 0.075f;
                        if (Mathf.Abs(x) > 0.66f) lid = 1f;
                        float lash = 1f;
                        for (int i = -2; i <= 2; i++)
                        {
                            float lx = i * 0.26f;
                            float ly = -0.02f + 0.30f * (1f - lx * lx);
                            lash = Mathf.Min(lash, Capsule(x, y, lx, ly, lx * 1.12f, ly - 0.24f, 0.045f));
                        }
                        return U(lid, lash);
                    }
                case SymKind.GRIND:   // 분쇄기: 톱니바퀴
                    {
                        float body = Circle(x, y, 0f, 0f, 0.46f);
                        float hole = Circle(x, y, 0f, 0f, 0.17f);
                        float teeth = 1f;
                        for (int i = 0; i < 8; i++)
                        {
                            float a = i * Mathf.PI * 2f / 8f;
                            teeth = Mathf.Min(teeth, Box(x, y, Mathf.Cos(a) * 0.52f, Mathf.Sin(a) * 0.52f, 0.13f, 0.13f, 0.04f));
                        }
                        return S(U(body, teeth), hole);
                    }
            }
            return 1f;
        }
    }
}
