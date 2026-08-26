// HHHud.cs — 화면 UI. 3D 공간에 넣기 어려운 것(상점·확률표·페이라인표)은 전부 여기로.
// 설계자 지시: "특히 중요한 정보 전력량이나 현재 층 등은 크게 표현해줘 잘보이게"
//   → 전력/목표와 현재 층은 화면에서 가장 큰 두 덩어리로 고정한다.
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace HeavensHunger
{
    public class HHHud : MonoBehaviour
    {
        public HHGame Game;

        Canvas _canvas;
        TextMeshProUGUI _floorBig, _floorSub, _powBig, _powSub, _lackTxt;
        Image _powBar, _powBarOver;
        TextMeshProUGUI _leverTxt, _coinTxt, _eyeTxt, _stopTxt, _bellTxt;
        TextMeshProUGUI _logTxt, _readTxt;
        RectTransform _shopPanel, _oddsPanel, _linePanel, _rosterPanel;
        TextMeshProUGUI _shopTxt, _oddsTxt, _lineTxt, _rosterTxt;
        Button _leverBtn, _departBtn;
        readonly List<Button> _shopButtons = new List<Button>();
        RectTransform _shopRow, _shopRow2;
        TextMeshProUGUI _banner;
        RectTransform _offerPanel;
        TextMeshProUGUI _offerTxt;
        Button _offerYes, _offerNo;
        float _bannerT;

        public void Build()
        {
            var canvasGo = new GameObject("HH_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            var root = canvasGo.transform;

            // ══════════ ① 현재 층 — 크게 ══════════
            var floorBox = HHUiKit.Panel(root, "FloorBox", new Vector2(0, 1), new Vector2(0, 1),
                                         new Vector2(24, -238), new Vector2(410, -20), HHUiKit.Ink);
            Row(floorBox, "cap", "현 재 층", 26, HHUiKit.Dim, TextAlignmentOptions.Left, 10, 36);
            _floorBig = Row(floorBox, "big", "1", 104, HHUiKit.Bone, TextAlignmentOptions.Left, 44, 140, FontStyles.Bold);
            _floorSub = Row(floorBox, "sub", "", 21, HHUiKit.Amber, TextAlignmentOptions.Left, 180, 40);

            // ══════════ ② 전력 / 목표 — 가장 크게 ══════════
            var powBox = HHUiKit.Panel(root, "PowerBox", new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                                       new Vector2(-440, -262), new Vector2(440, -20), HHUiKit.Ink);
            Row(powBox, "cap", "전 력 량", 28, HHUiKit.Dim, TextAlignmentOptions.Center, 8, 36);
            _powBig = Row(powBox, "big", "0", 116, HHUiKit.Volt, TextAlignmentOptions.Center, 40, 128, FontStyles.Bold);
            _powSub = Row(powBox, "sub", "", 32, HHUiKit.Bone, TextAlignmentOptions.Center, 168, 42);
            HHUiKit.Panel(powBox, "barBg", new Vector2(0, 0), new Vector2(1, 0),
                          new Vector2(20, 12), new Vector2(-20, 30), new Color(0.15f, 0.17f, 0.21f, 1f));
            _powBar = HHUiKit.Bar(powBox, "bar", HHUiKit.Volt, new Vector2(0, 0), new Vector2(1, 0),
                                  new Vector2(20, 12), new Vector2(-20, 30));
            _powBarOver = HHUiKit.Bar(powBox, "barOver", HHUiKit.Gold, new Vector2(0, 0), new Vector2(1, 0),
                                      new Vector2(20, 12), new Vector2(-20, 30));
            _powBar.fillAmount = 0; _powBarOver.fillAmount = 0;
            // 부족분은 별도 띄로 띄우지 않고 목표 줄에 붙인다 — 기계를 가리면 안 된다.
            _lackTxt = HHUiKit.Text(powBox, "lack", "", 1, HHUiKit.Blood, TextAlignmentOptions.Center,
                                    Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            _lackTxt.gameObject.SetActive(false);

            // ══════════ ③ 계기: 레버·동전·눈·종·풀 ══════════
            var meter = HHUiKit.Panel(root, "MeterBox", new Vector2(1, 1), new Vector2(1, 1),
                                      new Vector2(-400, -238), new Vector2(-24, -20), HHUiKit.Ink);
            _leverTxt = Row(meter, "lever", "", 42, HHUiKit.Gold, TextAlignmentOptions.Right, 8, 56, FontStyles.Bold);
            _coinTxt  = Row(meter, "coin",  "", 30, HHUiKit.Amber, TextAlignmentOptions.Right, 66, 40);
            _eyeTxt   = Row(meter, "eye",   "", 25, HHUiKit.Dim, TextAlignmentOptions.Right, 108, 36);
            _bellTxt  = Row(meter, "bell",  "", 25, HHUiKit.Violet, TextAlignmentOptions.Right, 146, 36);
            _stopTxt  = Row(meter, "stop",  "", 22, HHUiKit.Dim, TextAlignmentOptions.Right, 184, 32);

            // ══════════ ④ 판독기 (이번 레버 계산식) ══════════
            var readBox = HHUiKit.Panel(root, "ReadBox", new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                                        new Vector2(-660, 108), new Vector2(660, 158), HHUiKit.InkSoft);
            _readTxt = HHUiKit.Text(readBox, "read", "레버 = 전 칸 추첨 → 줄 지불 → 판은 그대로", 26, HHUiKit.Bone,
                                    TextAlignmentOptions.Center, Vector2.zero, Vector2.one,
                                    new Vector2(12, 4), new Vector2(-12, -4));

            // ══════════ ⑤ 로그 ══════════
            var logBox = HHUiKit.Panel(root, "LogBox", new Vector2(0, 0), new Vector2(0, 0),
                                       new Vector2(20, 106), new Vector2(352, 330), new Color(0.05f, 0.06f, 0.08f, 0.45f));
            _logTxt = HHUiKit.Text(logBox, "log", "", 19, HHUiKit.Dim, TextAlignmentOptions.BottomLeft,
                                   Vector2.zero, Vector2.one, new Vector2(12, 8), new Vector2(-12, -8));

            // ══════════ ⑥ 하단 버튼 바 ══════════
            var bar = HHUiKit.Panel(root, "BtnBar", new Vector2(0.06f, 0), new Vector2(0.94f, 0),
                                    new Vector2(0, 20), new Vector2(0, 92), new Color(0, 0, 0, 0));
            _leverBtn = HHUiKit.Btn(bar, "Lever", "\ub808 \ubc84", 28, new Vector2(0, 0), new Vector2(0.24f, 1),
                                    new Vector2(4, 0), new Vector2(-4, 0), new Color(0.55f, 0.20f, 0.17f), Color.white);
            _departBtn = HHUiKit.Btn(bar, "Depart", "\ucd9c \ubc1c", 28, new Vector2(0.24f, 0), new Vector2(0.48f, 1),
                                     new Vector2(4, 0), new Vector2(-4, 0), new Color(0.20f, 0.38f, 0.28f), Color.white);
            var shopBtn = HHUiKit.Btn(bar, "Shop", "\uc0c1\uc810 (Tab)", 24, new Vector2(0.48f, 0), new Vector2(0.61f, 1),
                                      new Vector2(4, 0), new Vector2(-4, 0), new Color(0.16f, 0.19f, 0.24f), HHUiKit.Bone);
            var rostBtn = HHUiKit.Btn(bar, "Roster", "\uba85\ubd80 (F)", 24, new Vector2(0.61f, 0), new Vector2(0.74f, 1),
                                      new Vector2(4, 0), new Vector2(-4, 0), new Color(0.16f, 0.19f, 0.24f), HHUiKit.Bone);
            var oddsBtn = HHUiKit.Btn(bar, "Odds", "\ud655\ub960\ud45c (Q)", 24, new Vector2(0.74f, 0), new Vector2(0.87f, 1),
                                      new Vector2(4, 0), new Vector2(-4, 0), new Color(0.16f, 0.19f, 0.24f), HHUiKit.Bone);
            var lineBtn = HHUiKit.Btn(bar, "Lines", "\uc904\ud45c (E)", 24, new Vector2(0.87f, 0), new Vector2(1f, 1),
                                      new Vector2(4, 0), new Vector2(0, 0), new Color(0.16f, 0.19f, 0.24f), HHUiKit.Bone);
            rostBtn.onClick.AddListener(() => Toggle(_rosterPanel));

            _leverBtn.onClick.AddListener(() => Game.DoLever());
            _departBtn.onClick.AddListener(() => Game.DoDepart());
            shopBtn.onClick.AddListener(() => Toggle(_shopPanel));
            oddsBtn.onClick.AddListener(() => Toggle(_oddsPanel));
            lineBtn.onClick.AddListener(() => Toggle(_linePanel));

            // ══════════ ⑦ 오버레이 패널들 (3D 로 옮기기 어려운 것 전부) ══════════
            _shopPanel = HHUiKit.Panel(root, "ShopPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                       new Vector2(-660, -420), new Vector2(660, 300), new Color(0.045f, 0.055f, 0.075f, 1f));
            HHUiKit.Text(_shopPanel, "title", "상 점  —  릴 풀 · 장치 · 아이템", 36, HHUiKit.Amber,
                         TextAlignmentOptions.TopLeft, Vector2.zero, Vector2.one, new Vector2(24, 0), new Vector2(-24, -16));
            _shopTxt = HHUiKit.Text(_shopPanel, "body", "", 22, HHUiKit.Bone, TextAlignmentOptions.TopLeft,
                                    Vector2.zero, Vector2.one, new Vector2(24, 16), new Vector2(-24, -78));
            _shopRow = HHUiKit.Panel(_shopPanel, "Row", new Vector2(0, 0), new Vector2(1, 0),
                                     new Vector2(24, 96), new Vector2(-24, 160), new Color(0, 0, 0, 0));
            _shopRow2 = HHUiKit.Panel(_shopPanel, "Row2", new Vector2(0, 0), new Vector2(1, 0),
                                      new Vector2(24, 18), new Vector2(-24, 82), new Color(0, 0, 0, 0));
            _shopPanel.gameObject.SetActive(false);

            _oddsPanel = HHUiKit.Panel(root, "OddsPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                       new Vector2(-520, -400), new Vector2(520, 300), new Color(0.045f, 0.055f, 0.075f, 1f));
            HHUiKit.Text(_oddsPanel, "title", "확 률 표  —  칸당 실제 추첨 확률", 36, HHUiKit.Violet,
                         TextAlignmentOptions.TopLeft, Vector2.zero, Vector2.one, new Vector2(24, 0), new Vector2(-24, -16));
            _oddsTxt = HHUiKit.Text(_oddsPanel, "body", "", 24, HHUiKit.Bone, TextAlignmentOptions.TopLeft,
                                    Vector2.zero, Vector2.one, new Vector2(24, 20), new Vector2(-24, -78));
            _oddsPanel.gameObject.SetActive(false);

            _linePanel = HHUiKit.Panel(root, "LinePanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                       new Vector2(-560, -380), new Vector2(560, 300), new Color(0.045f, 0.055f, 0.075f, 1f));
            HHUiKit.Text(_linePanel, "title", "판 정  —  줄이 서는 법", 36, HHUiKit.Gold,
                         TextAlignmentOptions.TopLeft, Vector2.zero, Vector2.one, new Vector2(24, 0), new Vector2(-24, -16));
            _lineTxt = HHUiKit.Text(_linePanel, "body", RulesText(), 24, HHUiKit.Bone, TextAlignmentOptions.TopLeft,
                                    Vector2.zero, Vector2.one, new Vector2(24, 20), new Vector2(-24, -78));
            _linePanel.gameObject.SetActive(false);

            _rosterPanel = HHUiKit.Panel(root, "RosterPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                         new Vector2(-560, -400), new Vector2(560, 300), new Color(0.045f, 0.055f, 0.075f, 1f));
            HHUiKit.Text(_rosterPanel, "title", "명 부  ·  설 비", 36, HHUiKit.Green,
                         TextAlignmentOptions.TopLeft, Vector2.zero, Vector2.one, new Vector2(24, 0), new Vector2(-24, -16));
            _rosterTxt = HHUiKit.Text(_rosterPanel, "body", "", 22, HHUiKit.Bone, TextAlignmentOptions.TopLeft,
                                      Vector2.zero, Vector2.one, new Vector2(24, 20), new Vector2(-24, -78));
            _rosterPanel.gameObject.SetActive(false);

            // ══════════ ⑧ 문 앞의 사람 · 인터폰 ══════════
            _offerPanel = HHUiKit.Panel(root, "OfferPanel", new Vector2(0, 0.5f), new Vector2(0, 0.5f),
                                        new Vector2(24, -170), new Vector2(560, 170), new Color(0.06f, 0.05f, 0.05f, 0.97f));
            _offerTxt = HHUiKit.Text(_offerPanel, "txt", "", 24, HHUiKit.Bone, TextAlignmentOptions.TopLeft,
                                     Vector2.zero, Vector2.one, new Vector2(20, 66), new Vector2(-20, -18));
            _offerYes = HHUiKit.Btn(_offerPanel, "Yes", "받는다", 26, new Vector2(0, 0), new Vector2(0.5f, 0),
                                    new Vector2(18, 16), new Vector2(-8, 62), new Color(0.24f, 0.36f, 0.26f), Color.white);
            _offerNo = HHUiKit.Btn(_offerPanel, "No", "끊는다", 26, new Vector2(0.5f, 0), new Vector2(1f, 0),
                                   new Vector2(8, 16), new Vector2(-18, 62), new Color(0.30f, 0.20f, 0.20f), Color.white);
            _offerYes.onClick.AddListener(() => { Game.AnswerOffer(true); Refresh(); });
            _offerNo.onClick.AddListener(() => { Game.AnswerOffer(false); Refresh(); });
            _offerPanel.gameObject.SetActive(false);

            // 배너
            _banner = HHUiKit.Text(root, "Banner", "", 46, HHUiKit.Gold, TextAlignmentOptions.Center,
                                   new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                   new Vector2(-700, 60), new Vector2(700, 170), FontStyles.Bold);
            _banner.gameObject.SetActive(false);
        }

        public void SetReadout(string s) { if (_readTxt != null) _readTxt.text = s; }

        public void ShowBanner(string s, Color c, int size)
        {
            if (_banner == null) return;
            _banner.gameObject.SetActive(true);
            _banner.text = s; _banner.color = c; _banner.fontSize = size;
            _bannerT = 1.5f;
        }

        void Update()
        {
            if (_bannerT > 0f)
            {
                _bannerT -= Time.deltaTime;
                if (_banner != null)
                {
                    var c = _banner.color; c.a = Mathf.Clamp01(_bannerT * 1.6f); _banner.color = c;
                    if (_bannerT <= 0f) _banner.gameObject.SetActive(false);
                }
            }
        }

        public void ToggleRoster() { Toggle(_rosterPanel); }

        /// <summary>패널 안에서 위에서부터 topPx 만큼 내려온 자리에 heightPx 짜리 한 줄을 놓는다.</summary>
        static TextMeshProUGUI Row(RectTransform box, string name, string content, int size, Color c,
                                   TextAlignmentOptions align, float topPx, float heightPx,
                                   FontStyles style = FontStyles.Normal)
        {
            return HHUiKit.Text(box, name, content, size, c, align,
                                new Vector2(0, 1), new Vector2(1, 1),
                                new Vector2(18, -(topPx + heightPx)), new Vector2(-18, -topPx), style);
        }

        void Toggle(RectTransform p)
        {
            bool on = !p.gameObject.activeSelf;
            _shopPanel.gameObject.SetActive(false);
            _oddsPanel.gameObject.SetActive(false);
            _linePanel.gameObject.SetActive(false);
            if (_rosterPanel != null) _rosterPanel.gameObject.SetActive(false);
            p.gameObject.SetActive(on);
            if (on && p == _shopPanel) BuildShopButtons();
            if (on && p == _rosterPanel) _rosterTxt.text = RosterText(Game.Run);
            // 1인칭 컨트롤러가 커서를 물고 있으므로 패널이 열리면 놓아준다
            SetCursorFree(AnyPanelOpen());
        }

        void SetOfferLabels(string yes, string no)
        {
            var y = _offerYes.GetComponentInChildren<TextMeshProUGUI>(); if (y != null) y.text = yes;
            var n = _offerNo.GetComponentInChildren<TextMeshProUGUI>(); if (n != null) n.text = no;
        }

        bool AnyPanelOpen()
        {
            return (_shopPanel != null && _shopPanel.gameObject.activeSelf)
                || (_oddsPanel != null && _oddsPanel.gameObject.activeSelf)
                || (_linePanel != null && _linePanel.gameObject.activeSelf)
                || (_rosterPanel != null && _rosterPanel.gameObject.activeSelf);
        }

        /// <summary>패널을 열면 마우스를 돌려주고, 닫으면 다시 1인칭에 넘긴다.</summary>
        public static void SetCursorFree(bool free)
        {
            Cursor.lockState = free ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = free;
            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (mb == null) continue;
                var n = mb.GetType().Name;
                if (n == "FirstPersonController" || n == "CrosshairInteractor") mb.enabled = !free;
            }
        }

        public void ToggleShop() { Toggle(_shopPanel); }
        public void ToggleOdds() { Toggle(_oddsPanel); }
        public void ToggleLines() { Toggle(_linePanel); }

        static string RulesText()
        {
            return
"<b>기본 — 직선 3연속</b>\n" +
"  · 가로: 한 행 안에서 어디든 3연속. <color=#ffd76e>4연속 ×2 · 5연속 ×4</color>\n" +
"  · 세로: 5개 열 각각 3칸\n" +
"  · 대각: 6방향 3칸\n\n" +
"<b><color=#ffd76e>희귀 — 꺾인 줄 완성형 (잭팟 ×4)</color></b>\n" +
"  브이 · 산 · 지붕 · 골짜기 · 내리막 · 오르막 — 5칸을 같은 문양으로 채우면 터진다\n\n" +
"<b>중복 정책</b>\n" +
"  · 같은 줄의 하위 조각은 안 센다 (5연속 = 1회)\n" +
"  · 다른 모양끼리는 겹쳐도 각각 지불한다\n" +
"  · 동시 N줄 = 전체 ×(1 + 0.2×(N−1))\n\n" +
"<b>장치는 릴에 없다 — 뱃지다</b>\n" +
"  레버마다 35%로 심볼 칸에 붙고, 그 심볼이 당첨 줄에 들면 발동. 레버당 1회(변압기만 줄마다).\n\n" +
"<b>눈</b>\n" +
"  눈이 앉은 칸에는 줄이 서지 않는다. 당첨 줄에 닿은 눈은 갈린다.\n" +
"  소각로는 눈을 태워 +2.5W/개로 바꾸고, 눈꺼풀은 그냥 감긴다.";
        }

        void BuildShopButtons()
        {
            foreach (var b in _shopButtons) if (b != null) Destroy(b.gameObject);
            _shopButtons.Clear();
            var run = Game.Run;
            if (run == null) return;
            int n = run.SymShop.Count;
            for (int i = 0; i < n; i++)
            {
                if (!run.SymShop[i].HasValue) continue;
                var d = HHSymbols.Get(run.SymShop[i].Value);
                int idx = i;
                float w = 1f / (n + 3);
                var b = HHUiKit.Btn(_shopRow, "Buy" + i, d.Name + "\n" + d.Price + "닢", 22,
                                    new Vector2(w * i, 0), new Vector2(w * (i + 1), 1),
                                    new Vector2(4, 0), new Vector2(-4, 0),
                                    run.Coins >= d.Price ? new Color(0.22f, 0.28f, 0.22f) : new Color(0.18f, 0.18f, 0.20f),
                                    run.Coins >= d.Price ? HHUiKit.Bone : HHUiKit.Dim);
                b.onClick.AddListener(() => { if (Game.Run.BuySymbol(idx)) { BuildShopButtons(); Refresh(); } });
                _shopButtons.Add(b);
            }
            for (int i = 0; i < run.ItemShop.Count; i++)
            {
                var it = HHItems.Get(run.ItemShop[i]);
                if (it == null) continue;
                string id = it.Id;
                float w = 1f / (n + 3);
                int slot = n + i;
                var b = HHUiKit.Btn(_shopRow, "Item" + i, it.Name + "\n" + it.Cost + "닢", 22,
                                    new Vector2(w * slot, 0), new Vector2(w * (slot + 1), 1),
                                    new Vector2(4, 0), new Vector2(-4, 0),
                                    run.Coins >= it.Cost ? new Color(0.26f, 0.22f, 0.34f) : new Color(0.18f, 0.18f, 0.20f),
                                    run.Coins >= it.Cost ? HHUiKit.Bone : HHUiKit.Dim);
                b.onClick.AddListener(() => { if (Game.Run.BuyItem(id)) { BuildShopButtons(); Refresh(); } });
                _shopButtons.Add(b);
            }
            {
                float w = 1f / (n + 3);
                int slot = n + run.ItemShop.Count;
                var b = HHUiKit.Btn(_shopRow, "Reroll", "새로고침\n1닢", 22,
                                    new Vector2(w * slot, 0), new Vector2(1f, 1),
                                    new Vector2(4, 0), new Vector2(0, 0),
                                    new Color(0.20f, 0.20f, 0.24f), HHUiKit.Bone);
                b.onClick.AddListener(() => { if (Game.Run.RerollShop()) { BuildShopButtons(); Refresh(); } });
                _shopButtons.Add(b);
            }

            // ── 설비 진열 (4칸) ──
            int pn = run.PartShop.Count;
            for (int i = 0; i < pn; i++)
            {
                var pd = HHContent.Part(run.PartShop[i]);
                if (pd == null) continue;
                string pid = pd.Id;
                float w = 1f / Mathf.Max(1, pn);
                bool afford = run.Coins >= pd.Cost && run.OwnedParts.Count < HHRun.PartHoldCap;
                var b = HHUiKit.Btn(_shopRow2, "Part" + i, pd.Name + "\n<size=17>" + pd.Family + " · " + pd.Cost + "닯</size>", 21,
                                    new Vector2(w * i, 0), new Vector2(w * (i + 1), 1),
                                    new Vector2(4, 0), new Vector2(-4, 0),
                                    afford ? new Color(0.20f, 0.26f, 0.32f) : new Color(0.18f, 0.18f, 0.20f),
                                    afford ? HHUiKit.Bone : HHUiKit.Dim);
                var lbl = b.GetComponentInChildren<TextMeshProUGUI>();
                if (lbl != null) lbl.enableWordWrapping = true;
                b.onClick.AddListener(() => { if (Game.Run.BuyPart(pid)) { BuildShopButtons(); Refresh(); } });
                _shopButtons.Add(b);
            }
        }

        static string RosterText(HHRun run)
        {
            if (run == null) return "";
            var sb = new StringBuilder();
            sb.AppendLine("<b>명부</b>  <color=#8b98a8>" + run.Aboard.Count + " / " + HHRun.AboardCap + " · 무게 " + run.TotalWeight.ToString("0") + " (무게는 문턱을 올린다)</color>");
            if (run.Aboard.Count == 0) sb.AppendLine("  <color=#5d6875>비어 있다</color>");
            foreach (var a in run.Aboard)
            {
                sb.Append("  <color=#8fce6e>").Append(a.Def.Name).Append("</color> <color=#8b98a8>(무게 ").Append(a.W);
                if (a.Def.IsQuest && a.Def.Dest > 0) sb.Append(" · ").Append(a.Def.Dest).Append("층까지 · 인도 ").Append(a.Def.Pay).Append("닯");
                sb.Append(")</color>\n     ").Append(a.Def.Fx).AppendLine();
                if (!string.IsNullOrEmpty(a.Def.FuseFamily))
                    sb.AppendLine(a.Fused
                        ? "     <color=#ffd76e>융합됨 — " + a.Def.FuseFx + "</color>"
                        : "     <color=#8b98a8>융합 대기: " + a.Def.FuseFamily + " 계열 설비를 건네면 터진다</color>");
            }
            sb.AppendLine();
            sb.AppendLine("<b>설비</b>  <color=#8b98a8>" + run.OwnedParts.Count + " / " + HHRun.PartHoldCap + "</color>");
            if (run.OwnedParts.Count == 0) sb.AppendLine("  <color=#5d6875>없다</color>");
            foreach (var p in run.OwnedParts)
                sb.AppendLine("  <color=#9ecbff>" + p.Name + "</color> <color=#8b98a8>(" + p.Family + ")</color>  " + p.Fx);
            if (run.Cargo.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("<b>화물</b>");
                foreach (var c in run.Cargo) sb.AppendLine("  " + c.Name + " (무게 " + c.W + ")");
            }
            return sb.ToString();
        }

        public void Refresh()
        {
            var run = Game != null ? Game.Run : null;
            if (run == null) return;

            // ① 층
            _floorBig.text = run.FloorNow.ToString("N0") + "<size=44><color=#8b98a8>층</color></size>";
            if (run.CanDepart)
                _floorSub.text = "출발 → <b>+" + run.JumpPreview + "층</b> (" + Mathf.Min(HHDial.FinalFloor, run.FloorNow + run.JumpPreview) + "층)"
                               + (run.Wraps > 1 ? " <color=#ffd76e>" + run.Wraps + "겹</color>" : "");
            else
                _floorSub.text = "문턱을 넘겨야 출발 · 상한 +" + run.JumpCap + "층/겹";

            // ② 전력
            int req = run.EffReq;
            _powBig.text = run.Power.ToString("N0") + "<size=48><color=#8b98a8>W</color></size>";
            _powBig.color = run.CanDepart ? HHUiKit.Green : HHUiKit.Volt;
            _powSub.text = "목표 <b>" + req.ToString("N0") + "W</b>   ·   정차 " + (run.StopIdx + 1)
                         + (run.CanDepart ? "   ·   <color=#8fce6e>출발 가능</color>"
                                          : "   ·   <color=#c96a5f>문턱까지 " + Mathf.Max(0, req - run.Power) + "W</color>");
            float ratio = req > 0 ? run.Power / (float)req : 0;
            _powBar.fillAmount = Mathf.Clamp01(ratio);
            _powBarOver.fillAmount = Mathf.Clamp01(ratio - 1f);


            // ③ 계기
            _leverTxt.text = "레버 " + run.LeversLeft + "<size=24><color=#8b98a8> / " + HHDial.LeverTank + "</color></size>";
            _coinTxt.text = "동전 " + run.Coins + "닢";
            _eyeTxt.text = "눈 " + run.Eyes.Count + " / " + HHDial.EyeMaxN + "   배수 ×" + run.EyeMultBase.ToString("0.0");
            _bellTxt.text = "종 " + run.BellGauge + " / " + run.GaugeNeed + (run.BellsTotal > 0 ? "  (울림 " + run.BellsTotal + ")" : "");
            _stopTxt.text = "릴 풀 " + run.Pool.Count + "장 · 장치 " + run.Devices.Count + "개";

            // ④ 판독기
            var last = run.Last;
            if (last != null && last.R != null && last.R.Bursts > 0)
            {
                var sb = new StringBuilder();
                sb.Append("줄 ").Append(last.R.Bursts).Append("개");
                if (last.R.LineMulAll > 1) sb.Append("  ·  동시 ×").Append(last.R.LineMulAll.ToString("0.0"));
                sb.Append("   기초 <b>").Append(Mathf.RoundToInt(last.R.TotalBase)).Append("W</b>");
                sb.Append(" × 배율 <color=#e3b341>").Append(last.R.M1.ToString("0.00")).Append("</color>");
                if (last.R.CoreM > 1) sb.Append(" × 코어 <color=#c9a6ff>×").Append(last.R.CoreM.ToString("0.0")).Append("</color>");
                if (last.EyePow > 1.001f) sb.Append(" × 눈 <color=#a5d6ff>×").Append(last.EyePow.ToString("0.00")).Append("</color>");
                sb.Append("  =  <b><color=#ffffff>").Append(last.Power).Append("W</color></b>");
                _readTxt.text = sb.ToString();
            }
            else if (last != null) _readTxt.text = "<color=#8b98a8>꽝 — 줄이 서지 않았다</color>";

            // ⑤ 로그
            var lg = new StringBuilder();
            int from = Mathf.Max(0, run.Log.Count - 12);
            for (int i = from; i < run.Log.Count; i++) lg.AppendLine(run.Log[i]);
            _logTxt.text = lg.ToString();

            _leverBtn.interactable = !run.Dead && !run.Finished && run.LeversLeft > 0;
            _departBtn.interactable = run.CanDepart;

            // ── 문 앞의 사람 / 인터폰 ──
            bool hasPax = run.Offers != null && run.Offers.Passenger != null;
            bool hasDeal = run.Offers != null && run.Offers.Deal != null && !run.Offers.DealTaken;
            if (hasPax)
            {
                var p = run.Offers.Passenger;
                _offerPanel.gameObject.SetActive(true);
                _offerTxt.text = "<color=#8fce6e><b>문 앞에 누군가 서 있다</b></color>\n"
                               + "<b>" + p.Name + "</b> <color=#8b98a8>(무게 " + p.W
                               + (p.IsQuest && p.Dest > 0 ? " · " + p.Dest + "층까지 · 인도 " + p.Pay + "닯" : " · 영구 탑승") + ")</color>\n"
                               + p.Fx + "\n<color=#6d7885>“" + p.Why + "”</color>";
                SetOfferLabels("태운다", "보낸다");
            }
            else if (hasDeal)
            {
                var d = run.Offers.Deal;
                string head = d.Kind == "grand" ? "<color=#ffd76e><b>종루가 통째로 울렸다</b></color>"
                            : d.Kind == "well" ? "<color=#c9a6ff><b>종이 맑게 겹쳤다</b></color>"
                            : d.Kind == "red" ? "<color=#c96a5f><b>붉은 종이 울렸다</b></color>"
                            : "<color=#e3b341><b>인터폰이 울린다</b></color>";
                _offerPanel.gameObject.SetActive(true);
                _offerTxt.text = head + "\n" + d.Text;
                SetOfferLabels("받는다", "끊는다");
            }
            else _offerPanel.gameObject.SetActive(false);

            if (_rosterPanel.gameObject.activeSelf) _rosterTxt.text = RosterText(run);

            if (_oddsPanel.gameObject.activeSelf) _oddsTxt.text = OddsText(run);
            if (_shopPanel.gameObject.activeSelf) _shopTxt.text = ShopText(run);
        }

        static string OddsText(HHRun run)
        {
            var prob = run.DrawProbabilities();
            var sb = new StringBuilder();
            sb.AppendLine("<color=#8b98a8>릴 풀 " + run.Pool.Count + "장 · 아이템 가중 반영 · 이 표가 실제 추첨값이다</color>\n");
            foreach (var d in HHSymbols.All)
            {
                if (d.Family != SymFamily.Flesh) continue;
                float p; prob.TryGetValue(d.Kind, out p);
                int n = run.Pool.FindAll(x => x.K == d.Kind).Count;
                int bars = Mathf.RoundToInt(p * 60f);
                sb.Append(d.Name.PadRight(4, '\u3000'));
                sb.Append(" <color=#9ecbff>").Append(d.Val).Append("W</color>  ");
                sb.Append(n).Append("장  ");
                sb.Append("<color=#e3b341>").Append(new string('|', Mathf.Max(0, bars))).Append("</color> ");
                sb.Append((p * 100f).ToString("0.0")).AppendLine("%");
            }
            sb.AppendLine();
            sb.AppendLine("<color=#8b98a8>장치는 릴에 없다 — 레버마다 " + (HHDial.DeviceBadgeRate * 100) + "% 로 뱃지가 붙는다</color>");
            foreach (var g in run.Devices)
                sb.AppendLine("  <color=#e3b341>" + HHSymbols.Get(g.K).Name + (g.Lv > 1 ? " L" + g.Lv : "") + "</color> — " + HHSymbols.Get(g.K).Desc);
            return sb.ToString();
        }

        static string ShopText(HHRun run)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<color=#8b98a8>동전 " + run.Coins + "닢  ·  당첨 레버 +1닢  ·  출발 때 남긴 레버 누진(1·2·3·5·8닢)</color>\n");
            sb.Append("<b>릴 풀</b>  ");
            var grp = new Dictionary<string, int>();
            foreach (var e in run.Pool) { string k = e.K + "@" + e.Lv; grp[k] = grp.ContainsKey(k) ? grp[k] + 1 : 1; }
            foreach (var kv in grp)
            {
                var parts = kv.Key.Split('@');
                var kind = (SymKind)System.Enum.Parse(typeof(SymKind), parts[0]);
                sb.Append(HHSymbols.Get(kind).Name).Append(parts[1] != "1" ? " L" + parts[1] : "").Append(" ×").Append(kv.Value).Append("   ");
            }
            sb.AppendLine("\n");
            sb.Append("<b>장치</b>  ");
            if (run.Devices.Count == 0) sb.Append("<color=#5d6875>없음</color>");
            foreach (var g in run.Devices) sb.Append("<color=#e3b341>").Append(HHSymbols.Get(g.K).Name).Append(g.Lv > 1 ? " L" + g.Lv : "").Append("</color>   ");
            sb.AppendLine("\n");
            sb.Append("<b>아이템</b>  ");
            if (run.ItemStacks.Count == 0 && run.Actives.Count == 0) sb.Append("<color=#5d6875>없음</color>");
            foreach (var kv in run.ItemStacks)
            { var it = HHItems.Get(kv.Key); if (it != null) sb.Append("<color=#c9a6ff>").Append(it.Name).Append(kv.Value > 1 ? " ×" + kv.Value : "").Append("</color>   "); }
            foreach (var a in run.Actives)
            { var it = HHItems.Get(a.Id); if (it != null) sb.Append("<color=#9ecbff>").Append(it.Name).Append(" ").Append(a.Chg).Append("/").Append(a.MaxChg).Append("</color>   "); }
            sb.AppendLine("\n");
            sb.AppendLine("<color=#8b98a8>아래 진열에서 산다. 문양은 릴에 들어가고, 장치는 뱃지로 붙는다.</color>");
            return sb.ToString();
        }
    }
}
