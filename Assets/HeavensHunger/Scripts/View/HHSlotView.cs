// HHSlotView.cs — 5×3 슬롯의 3D 배치 + 2D 문양 스프라이트 + 크레셴도 정산.
// 블렌더에서 내보낸 HH_Cell_00..14 앵커에 문양을 앉힌다.
// ⚠ 앵커에는 축변환 회전(-90° X)이 박혀 있다 → 월드 무회전 프레임을 하나 끼워 넣는다.
//   z 순서: 테두리 0.108 < 스프라이트 0.115 < 글자 0.14
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace HeavensHunger
{
    public class HHSlotView : MonoBehaviour
    {
        public Transform CabinRoot;
        public Transform[] CellAnchors = new Transform[HHDial.Cells];
        public Transform LeverPivot;

        readonly SpriteRenderer[] _sym = new SpriteRenderer[HHDial.Cells];
        readonly SpriteRenderer[] _rimR = new SpriteRenderer[HHDial.Cells];
        readonly TextMeshPro[] _valTxt = new TextMeshPro[HHDial.Cells];
        readonly TextMeshPro[] _badgeTxt = new TextMeshPro[HHDial.Cells];
        readonly GameObject[] _badge = new GameObject[HHDial.Cells];
        readonly Transform[] _frame = new Transform[HHDial.Cells];

        Material _unlitSrc, _lineMat;
        Vector3 _outward = Vector3.back;   // 기계가 캐빈 안쪽을 보는 방향 (유리 − 챔버배열로 산출)
        readonly List<LineRenderer> _payLines = new List<LineRenderer>();
        readonly List<TextMeshPro> _floaters = new List<TextMeshPro>();
        const float TILE = 0.40f;

        void Awake() { Build(); }
        public void EnsureBuilt() { if (_frame[0] == null) Build(); }

        public static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var r = FindDeep(root.GetChild(i), name);
                if (r != null) return r;
            }
            return null;
        }

        /// <summary>
        /// \uae30\uacc4\uc758 \ubc14\uae65\ubc29\ud5a5\uc744 \ubaa8\ub378\uc5d0\uc11c \uc9c1\uc811 \uc7ac\ub294\ub2e4 \u2014 \uc52c\uc5d0\uc11c \ud328\ub110\uc774 \uc5b4\ub290 \ucabd\uc73c\ub85c \ub3cc\uc544\uac00 \uc788\ub4e0 \ub9de\uac8c.
        /// \uc720\ub9ac(TEST_H_Glass)\uac00 \ucc54\ubc84\ubc30\uc5f4(TEST_H_ChamberArray)\ubcf4\ub2e4 \uc55e\uc5d0 \uc788\ub2e4.
        /// </summary>
        public Vector3 Outward { get { return _outward; } }

        public static Vector3 MachineOutward(Transform root)
        {
            var glass = FindDeep(root, "TEST_H_Glass");
            var arr = FindDeep(root, "TEST_H_ChamberArray");
            if (glass != null && arr != null)
            {
                var gr = glass.GetComponent<Renderer>();
                var ar = arr.GetComponent<Renderer>();
                if (gr != null && ar != null)
                {
                    var v = gr.bounds.center - ar.bounds.center; v.y = 0f;
                    if (v.sqrMagnitude > 1e-6f) return v.normalized;
                }
            }
            return Vector3.back;
        }

        void ComputeOutward()
        {
            var root = CabinRoot != null ? CabinRoot : transform;
            var glass = FindDeep(root, "TEST_H_Glass");
            var arr = FindDeep(root, "TEST_H_ChamberArray");
            if (glass != null && arr != null)
            {
                var gr = glass.GetComponent<Renderer>();
                var ar = arr.GetComponent<Renderer>();
                if (gr != null && ar != null)
                {
                    var v = gr.bounds.center - ar.bounds.center;
                    v.y = 0f;
                    if (v.sqrMagnitude > 1e-6f) { _outward = v.normalized; return; }
                }
            }
            _outward = Vector3.back;
        }

        void ResolveAnchors()
        {
            var root = CabinRoot != null ? CabinRoot : transform;
            for (int i = 0; i < HHDial.Cells; i++)
                if (CellAnchors[i] == null) CellAnchors[i] = FindDeep(root, "HH_Cell_" + i.ToString("00"));
            if (LeverPivot == null) LeverPivot = FindDeep(root, "HH_LeverPivot");
        }

        void Build()
        {
            ResolveAnchors();
            ComputeOutward();
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlit == null) unlit = Shader.Find("Unlit/Color");
            _unlitSrc = new Material(unlit) { name = "HH_Unlit" };
            _lineMat = new Material(unlit) { name = "HH_PaylineMat" };
            _lineMat.color = HHUiKit.Gold;

            var spriteShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (spriteShader == null) spriteShader = Shader.Find("Sprites/Default");

            for (int i = 0; i < HHDial.Cells; i++)
            {
                var a = CellAnchors[i];
                if (a == null) continue;

                var frame = new GameObject("HH_CellFrame_" + i.ToString("00"));
                frame.transform.SetParent(a, false);
                frame.transform.rotation = Quaternion.LookRotation(_outward, Vector3.up);
                frame.transform.position = a.position;
                var F = frame.transform;
                _frame[i] = F;

                // 당첨 테두리
                // \ub2f9\ucca8 \ud14c\ub450\ub9ac \u2014 \uaf49 \ucc2c \uc0ac\uac01\ud615\uc774 \uc544\ub2c8\ub77c \ud14c\ub450\ub9ac \uc2a4\ud504\ub77c\uc774\ud2b8
                var rim = new GameObject("HH_Rim_" + i.ToString("00"));
                rim.transform.SetParent(F, false);
                rim.transform.localPosition = new Vector3(0, 0, 0.128f);
                rim.transform.localScale = Vector3.one * (TILE + 0.055f);
                var rsr = rim.AddComponent<SpriteRenderer>();
                if (spriteShader != null) rsr.sharedMaterial = new Material(spriteShader);
                rsr.sprite = HHSymbolArt.Ring();
                rsr.color = HHUiKit.Gold;
                rsr.enabled = false;
                _rimR[i] = rsr;

                // 2D 문양 스프라이트
                var sg = new GameObject("HH_Sym_" + i.ToString("00"));
                sg.transform.SetParent(F, false);
                sg.transform.localPosition = new Vector3(0, 0.045f, 0.118f);
                sg.transform.localScale = Vector3.one * (TILE * 0.80f);
                var sr = sg.AddComponent<SpriteRenderer>();
                if (spriteShader != null) sr.sharedMaterial = new Material(spriteShader);
                sr.enabled = false;
                _sym[i] = sr;

                _valTxt[i] = MakeText(F, "HH_Val_" + i, 26f, new Vector3(0, -0.152f, 0.14f));
                var bg = new GameObject("HH_Badge_" + i);
                bg.transform.SetParent(F, false);
                bg.transform.localPosition = new Vector3(0f, 0.185f, 0.145f);
                bg.transform.localRotation = Quaternion.identity;
                _badgeTxt[i] = MakeText(bg.transform, "T", 13f, Vector3.zero);
                _badge[i] = bg;
                bg.SetActive(false);
            }
            ClearBoard();
        }

        TextMeshPro MakeText(Transform parent, string name, float size, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(0, 180f, 0);   // 무회전이면 뒷면이 보인다
            go.transform.localScale = Vector3.one * 0.05f;
            var t = go.AddComponent<TextMeshPro>();
            t.font = HHUiKit.LoadFont();
            t.fontSize = size;
            t.alignment = TextAlignmentOptions.Center;
            t.enableWordWrapping = false;
            t.color = Color.white;
            t.GetComponent<RectTransform>().sizeDelta = new Vector2(9f, 3.2f);
            var mr = t.GetComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return t;
        }

        public void ClearBoard()
        {
            for (int i = 0; i < HHDial.Cells; i++)
            {
                if (_sym[i] != null) _sym[i].enabled = false;
                if (_rimR[i] != null) _rimR[i].enabled = false;
                if (_valTxt[i] != null) _valTxt[i].text = "";
                if (_badge[i] != null) _badge[i].SetActive(false);
            }
            ClearPaylines();
        }

        /// <summary>판을 즉시 그린다. showWins=false 면 당첨 표시는 빼고 문양만 (크레셴도용).</summary>
        public void Render(HHRun run, bool showWins = true)
        {
            EnsureBuilt();
            if (run == null) return;
            var badgeAt = new Dictionary<int, BadgeSlot>();
            foreach (var b in run.LastBadges) badgeAt[b.Cell] = b;
            var bellAt = new HashSet<int>(run.LastBellCells);

            for (int i = 0; i < HHDial.Cells; i++)
            {
                if (_sym[i] == null) continue;
                var c = run.Board[i];
                if (!c.Filled) { _sym[i].enabled = false; _valTxt[i].text = ""; _badge[i].SetActive(false); _rimR[i].enabled = false; continue; }

                if (c.IsEye)
                {
                    _sym[i].enabled = false;
                    _valTxt[i].text = "<color=#c96a5f>막힘</color>";
                    _badge[i].SetActive(false);
                    _rimR[i].enabled = false;
                    continue;
                }

                var d = HHSymbols.Get(c.K);
                var col = HHUiKit.SymColor(c.K);
                _sym[i].enabled = true;
                _sym[i].sprite = HHSymbolArt.Get(c.K);
                _sym[i].color = col;
                _valTxt[i].text = d.Family == SymFamily.Flesh
                    ? Mathf.RoundToInt(HHResolver.SymVal(c)) + "W" + (c.Lv > 1 ? " <color=#e3b341>L" + c.Lv + "</color>" : "")
                    : "<color=#e3b341>" + d.Name + "</color>";
                _valTxt[i].color = Color.white;

                BadgeSlot bs;
                bool hasDev = badgeAt.TryGetValue(i, out bs);
                bool hasBell = bellAt.Contains(i);
                if (hasDev || hasBell)
                {
                    _badge[i].SetActive(true);
                    _badgeTxt[i].text = hasDev
                        ? "<color=#e3b341>" + HHSymbols.Get(bs.Dev.K).Name + "</color>"
                        : "<color=#ffd76e>종</color>";
                }
                else _badge[i].SetActive(false);
                _rimR[i].enabled = false;
            }

            ClearPaylines();
            if (showWins) MarkWins(run);
        }

        void MarkWins(HHRun run)
        {
            if (run.Last == null || run.Last.R == null) return;
            int n = 0;
            foreach (var ev in run.Last.R.Events) { MarkOne(ev, n++); }
        }

        void MarkOne(LineHit ev, int idx)
        {
            foreach (var c in ev.Cells)
            {
                if (_rimR[c] != null) _rimR[c].enabled = true;
                if (_sym[c] != null) _sym[c].transform.localScale = Vector3.one * (TILE * 0.92f);
            }
            var lr = GetLine(idx);
            lr.gameObject.SetActive(true);
            lr.positionCount = ev.Cells.Length;
            for (int i = 0; i < ev.Cells.Length; i++)
            {
                var a = CellAnchors[ev.Cells[i]];
                lr.SetPosition(i, a != null ? a.position + _outward * 0.145f : Vector3.zero);
            }
            var c2 = ev.Zig ? HHUiKit.Gold : HHUiKit.Amber;
            lr.startColor = c2; lr.endColor = c2;
            lr.widthMultiplier = ev.Zig ? 0.05f : 0.032f;
        }

        void ClearPaylines()
        {
            foreach (var lr in _payLines) if (lr != null) lr.gameObject.SetActive(false);
            for (int i = 0; i < HHDial.Cells; i++)
                if (_sym[i] != null) _sym[i].transform.localScale = Vector3.one * (TILE * 0.80f);
        }

        LineRenderer GetLine(int i)
        {
            while (_payLines.Count <= i)
            {
                var go = new GameObject("HH_Payline_" + _payLines.Count);
                go.transform.SetParent(transform, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.material = _lineMat;
                lr.numCapVertices = 4; lr.numCornerVertices = 4;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
                _payLines.Add(lr);
            }
            return _payLines[i];
        }

        // ── 레버 연출 ──
        public IEnumerator PullLeverAnim()
        {
            if (HHAudio.I != null) HHAudio.I.Lever();
            if (LeverPivot == null) yield break;
            var q0 = LeverPivot.localRotation;
            float t = 0;
            while (t < 1f) { t += Time.deltaTime * 6f; LeverPivot.localRotation = q0 * Quaternion.Euler(Mathf.Lerp(0, -38f, t), 0, 0); yield return null; }
            t = 0;
            while (t < 1f) { t += Time.deltaTime * 3.2f; LeverPivot.localRotation = q0 * Quaternion.Euler(Mathf.Lerp(-38f, 0, t), 0, 0); yield return null; }
            LeverPivot.localRotation = q0;
        }

        /// <summary>릴이 열마다 순서대로 멈춘다. 결과는 이미 확정돼 있고 눈속임만 한다.</summary>
        public IEnumerator SpinAnim(HHRun run, float speed)
        {
            EnsureBuilt();
            ClearPaylines();
            var pool = run.Pool;
            var rnd = new System.Random();
            var stopped = new bool[HHResolver.C];
            float per = 0.14f / Mathf.Max(0.1f, speed);

            for (int col = 0; col < HHResolver.C; col++)
            {
                float t = 0;
                while (t < per)
                {
                    for (int c2 = col; c2 < HHResolver.C; c2++)
                        for (int r = 0; r < HHResolver.R; r++)
                        {
                            int i = r * HHResolver.C + c2;
                            if (_sym[i] == null || run.Board[i].IsEye) continue;
                            var e = pool[rnd.Next(pool.Count)];
                            _sym[i].enabled = true;
                            _sym[i].sprite = HHSymbolArt.Get(e.K);
                            _sym[i].color = HHUiKit.SymColor(e.K) * 0.75f;
                            _valTxt[i].text = "";
                        }
                    t += Time.deltaTime;
                    yield return null;
                }
                stopped[col] = true;
                // 이 열만 확정 표시
                for (int r = 0; r < HHResolver.R; r++)
                {
                    int i = r * HHResolver.C + col;
                    ShowCell(run, i);
                }
                if (HHAudio.I != null) HHAudio.I.ReelStop(col);
            }
            Render(run, false);
        }

        void ShowCell(HHRun run, int i)
        {
            var c = run.Board[i];
            if (_sym[i] == null) return;
            if (!c.Filled || c.IsEye)
            {
                _sym[i].enabled = false;
                _valTxt[i].text = c.IsEye ? "<color=#c96a5f>막힘</color>" : "";
                return;
            }
            var d = HHSymbols.Get(c.K);
            _sym[i].enabled = true;
            _sym[i].sprite = HHSymbolArt.Get(c.K);
            _sym[i].color = HHUiKit.SymColor(c.K);
            _valTxt[i].text = d.Family == SymFamily.Flesh
                ? Mathf.RoundToInt(HHResolver.SymVal(c)) + "W"
                : "<color=#e3b341>" + d.Name + "</color>";
        }

        /// <summary>
        /// 크레셴도 정산 — 작은 줄 → 큰 줄 → 완성형(잭팟).
        /// 완성형 직전엔 침묵을 넣고, 터질 때 화면이 흔들린다.
        /// </summary>
        public IEnumerator RevealCrescendo(HHRun run, HHHud hud, Camera cam)
        {
            if (run.Last == null || run.Last.R == null) yield break;
            var evs = new List<LineHit>(run.Last.R.Events);
            evs.Sort((a, b) => { int za = a.Zig ? 1 : 0, zb = b.Zig ? 1 : 0; if (za != zb) return za - zb; return a.Value.CompareTo(b.Value); });

            if (evs.Count == 0)
            {
                if (HHAudio.I != null) HHAudio.I.Dud();
                yield break;
            }

            float acc = 0;
            for (int i = 0; i < evs.Count; i++)
            {
                var ev = evs[i];
                if (ev.Zig && (i == 0 || !evs[i - 1].Zig)) yield return new WaitForSeconds(0.38f);  // 완성형 직전 침묵

                MarkOne(ev, i);
                acc += ev.Value;
                Floater(ev, acc);
                if (HHAudio.I != null) { if (ev.Zig) HHAudio.I.Jackpot(); else HHAudio.I.Pop(i); }
                if (hud != null) hud.SetReadout("줄 " + (i + 1) + "개 — <b>" + Mathf.RoundToInt(acc) + "W</b> …");
                if (ev.Zig) { if (hud != null) hud.ShowBanner(ev.Name + "! — 벽이 운다", HHUiKit.Gold, 46); yield return Shake(cam, 0.36f, 0.055f); }
                yield return new WaitForSeconds(ev.Zig ? 0.30f : Mathf.Max(0.14f, 0.28f - i * 0.02f));
            }
            if (run.Last.BellAdd > 0 && HHAudio.I != null) HHAudio.I.Bell();
            if (run.Last.CoinsGained > 0 && HHAudio.I != null) HHAudio.I.Coin();
            bool hasZig = evs.Exists(e => e.Zig);
            if (!hasZig && evs.Count >= 3 && hud != null) hud.ShowBanner("크게 섰다!", HHUiKit.Amber, 34);
        }

        void Floater(LineHit ev, float acc)
        {
            var anchor = CellAnchors[ev.Cells[ev.Cells.Length / 2]];
            if (anchor == null) return;
            var t = GetFloater();
            t.gameObject.SetActive(true);
            t.transform.position = anchor.position + Vector3.up * 0.12f + _outward * 0.22f;
            t.transform.rotation = Quaternion.LookRotation(-_outward, Vector3.up);
            t.color = ev.Zig ? HHUiKit.Gold : HHUiKit.Amber;
            t.fontSize = ev.Zig ? 40f : 28f;
            t.text = ev.Name + "  +" + Mathf.RoundToInt(ev.Value) + "W" + (!ev.Zig && ev.Len >= 4 ? "  " + ev.Len + "연속!" : "");
            StartCoroutine(FadeFloater(t));
        }

        TextMeshPro GetFloater()
        {
            foreach (var f in _floaters) if (f != null && !f.gameObject.activeSelf) return f;
            var go = new GameObject("HH_Floater_" + _floaters.Count);
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one * 0.05f;
            var t = go.AddComponent<TextMeshPro>();
            t.font = HHUiKit.LoadFont();
            t.alignment = TextAlignmentOptions.Center;
            t.enableWordWrapping = false;
            t.GetComponent<RectTransform>().sizeDelta = new Vector2(24f, 4f);
            var mr = t.GetComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            _floaters.Add(t);
            return t;
        }

        IEnumerator FadeFloater(TextMeshPro t)
        {
            var p0 = t.transform.position;
            float e = 0;
            while (e < 1f)
            {
                e += Time.deltaTime * 1.25f;
                t.transform.position = p0 + new Vector3(0, e * 0.20f, 0);
                var c = t.color; c.a = Mathf.Clamp01(1.6f - e * 1.6f); t.color = c;
                yield return null;
            }
            t.gameObject.SetActive(false);
        }

        public IEnumerator Shake(Camera cam, float dur, float amp)
        {
            if (cam == null) yield break;
            var p0 = cam.transform.localPosition;
            float t = 0;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = 1f - t / dur;
                cam.transform.localPosition = p0 + new Vector3(
                    (Random.value * 2 - 1) * amp * k,
                    (Random.value * 2 - 1) * amp * k, 0);
                yield return null;
            }
            cam.transform.localPosition = p0;
        }
    }
}
