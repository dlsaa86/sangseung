// HHUiKit.cs — 코드로 uGUI 를 짓는 최소 도구. 씬을 손으로 배선하지 않기 위해 존재한다.
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace HeavensHunger
{
    public static class HHUiKit
    {
        public static TMP_FontAsset Font;

        public static TMP_FontAsset LoadFont()
        {
            if (Font != null) return Font;
            Font = Resources.Load<TMP_FontAsset>("HH_KR SDF");
#if UNITY_EDITOR
            if (Font == null)
                Font = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/HeavensHunger/Art/Fonts/HH_KR SDF.asset");
#endif
            if (Font == null) Font = TMP_Settings.defaultFontAsset;
            return Font;
        }

        public static RectTransform Panel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
                                          Vector2 offMin, Vector2 offMax, Color bg)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = offMin; rt.offsetMax = offMax;
            var img = go.GetComponent<Image>();
            img.color = bg;
            img.raycastTarget = false;
            return rt;
        }

        public static TextMeshProUGUI Text(Transform parent, string name, string content, int size,
                                           Color color, TextAlignmentOptions align,
                                           Vector2 anchorMin, Vector2 anchorMax, Vector2 offMin, Vector2 offMax,
                                           FontStyles style = FontStyles.Normal)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.font = LoadFont();
            t.text = content;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.fontStyle = style;
            t.raycastTarget = false;
            t.overflowMode = TextOverflowModes.Overflow;
            t.enableWordWrapping = true;
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = offMin; rt.offsetMax = offMax;
            return t;
        }

        public static Image Bar(Transform parent, string name, Color color,
                                Vector2 anchorMin, Vector2 anchorMax, Vector2 offMin, Vector2 offMax)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = offMin; rt.offsetMax = offMax;
            var img = go.GetComponent<Image>();
            img.color = color;
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
            img.sprite = WhiteSprite();
            img.raycastTarget = false;
            return img;
        }

        static Sprite _white;
        public static Sprite WhiteSprite()
        {
            if (_white != null) return _white;
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var px = new Color[16];
            for (int i = 0; i < 16; i++) px[i] = Color.white;
            tex.SetPixels(px); tex.Apply();
            tex.hideFlags = HideFlags.DontSave;
            _white = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            _white.hideFlags = HideFlags.DontSave;
            return _white;
        }

        public static Button Btn(Transform parent, string name, string label, int size,
                                 Vector2 anchorMin, Vector2 anchorMax, Vector2 offMin, Vector2 offMax,
                                 Color bg, Color fg)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = offMin; rt.offsetMax = offMax;
            var img = go.GetComponent<Image>();
            img.color = bg; img.sprite = WhiteSprite(); img.type = Image.Type.Sliced;
            var t = Text(go.transform, "Label", label, size, fg, TextAlignmentOptions.Center,
                         Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            t.enableWordWrapping = false;
            var btn = go.GetComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            colors.disabledColor = new Color(1, 1, 1, 0.35f);
            btn.colors = colors;
            return btn;
        }

        // ── 팔레트 ──
        public static readonly Color Ink      = new Color(0.06f, 0.07f, 0.09f, 0.92f);
        public static readonly Color InkSoft  = new Color(0.09f, 0.11f, 0.14f, 0.88f);
        public static readonly Color Line     = new Color(0.20f, 0.24f, 0.29f, 1f);
        public static readonly Color Bone     = new Color(0.85f, 0.88f, 0.92f, 1f);
        public static readonly Color Dim      = new Color(0.55f, 0.60f, 0.66f, 1f);
        public static readonly Color Amber    = new Color(0.94f, 0.68f, 0.24f, 1f);
        public static readonly Color Blood    = new Color(0.83f, 0.32f, 0.27f, 1f);
        public static readonly Color Volt     = new Color(0.55f, 0.80f, 1.00f, 1f);
        public static readonly Color Gold     = new Color(1.00f, 0.84f, 0.43f, 1f);
        public static readonly Color Violet   = new Color(0.72f, 0.60f, 0.98f, 1f);
        public static readonly Color Green    = new Color(0.56f, 0.81f, 0.43f, 1f);

        /// <summary>문양별 색 — 말초는 창백, 장기는 붉고 깊게. 값 사다리를 색으로 읽게 한다.</summary>
        public static Color SymColor(SymKind k)
        {
            switch (k)
            {
                case SymKind.TOOTH:  return new Color(0.93f, 0.92f, 0.86f);
                case SymKind.BONE:   return new Color(0.86f, 0.83f, 0.74f);
                case SymKind.EAR:    return new Color(0.90f, 0.68f, 0.63f);
                case SymKind.TONGUE: return new Color(0.88f, 0.45f, 0.48f);
                case SymKind.HEART:  return new Color(0.79f, 0.20f, 0.24f);
                case SymKind.BRAIN:  return new Color(0.72f, 0.48f, 0.80f);
                case SymKind.LUNG:   return new Color(0.42f, 0.55f, 0.80f);
                default:             return new Color(0.95f, 0.78f, 0.35f); // 장치 = 금빛
            }
        }
    }
}
