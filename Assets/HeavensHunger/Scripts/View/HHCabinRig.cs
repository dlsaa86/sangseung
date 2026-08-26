// HHCabinRig.cs — 이전 씬에 이미 있던 **물리 계기들**을 coreB 런에 다시 물린다.
// 새로 만들지 않는다. 캐빈 패널의 진짜 오브젝트(레버 손잡이·스핀 게이지 5칸·사이렌·계기 유리·문)를
// 이름으로 찾아 값만 흘려 넣는다.
using System.Collections;
using UnityEngine;
using TMPro;

namespace HeavensHunger
{
    public class HHCabinRig : MonoBehaviour
    {
        public Transform Panel;          // HH_Panel_5x3
        public Transform CabinRoot;      // CabinAD47

        Transform _leverHandle, _leverHandle2;
        Quaternion _leverRest, _leverRest2;
        Ascend.Prototype.View.LeverStateMachine _leverSM;
        readonly Renderer[] _spinPips = new Renderer[5];
        Renderer _sirenBulb;
        Transform _doorL, _doorR;
        Vector3 _doorLClosed, _doorRClosed;
        Transform _powerBar;             // 계기 유리 안에 넣는 물리 막대
        Renderer _powerBarR;
        TextMeshPro _gaugeText;
        Light _chamberFill;
        Renderer _harnessFill;

        Vector3 _outward = Vector3.back;
        Color _pipOff = new Color(0.10f, 0.10f, 0.11f);
        Color _pipOn = new Color(1.00f, 0.72f, 0.28f);
        float _sirenT;

        public void Bind(Transform panel, Transform cabinRoot)
        {
            Panel = panel; CabinRoot = cabinRoot;
            Resolve();
        }

        void Awake() { if (Panel != null) Resolve(); }

        // 설계자 지적(2026-08-25) 후 구조: 원본 캐빈(BK 구움이 맞는 메시)이 켜져 있고
        // 복제 캐빈은 레버 계열만 남았다. 이름이 양쪽에 있으므로 활성 인스턴스를 우선한다.
        Transform F(string n) { return FindActivePreferred(CabinRoot != null ? CabinRoot : Panel, n); }

        static Transform FindActivePreferred(Transform root, string n)
        {
            if (root == null) return null;
            Transform fallback = null;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != n) continue;
                if (t.gameObject.activeInHierarchy) return t;
                if (fallback == null) fallback = t;
            }
            return fallback;
        }

        void Resolve()
        {
            if (Panel == null) return;
            _outward = HHSlotView.MachineOutward(Panel);

            _leverHandle = F("SM_Lever_Handle.003");
            _leverHandle2 = F("SM_Lever_Handle.001");
            if (_leverHandle != null) _leverRest = _leverHandle.localRotation;
            if (_leverHandle2 != null) _leverRest2 = _leverHandle2.localRotation;

            // 실행 레버의 원 소유자(LeverStateMachine)를 그대로 쓴다 — 저항→가속→오버슈트→걸림→스프링 복귀.
            // 예전 빌드에서 튜닝된 감각이며, 상세 근거는 LeverStateMachine.cs 머리 주석에 있다.
            // 보조 손잡이(.001)는 rest 축이 감겨 있어(Z≈270°) X 회전을 먹이면 옆으로 비틀린다 — 더는 움직이지 않는다.
            if (_leverHandle != null)
            {
                _leverSM = _leverHandle.GetComponent<Ascend.Prototype.View.LeverStateMachine>();
                if (_leverSM == null) _leverSM = _leverHandle.gameObject.AddComponent<Ascend.Prototype.View.LeverStateMachine>();
                // 축: +X 회전 = 손잡이가 기계 반대쪽(플레이어 쪽)으로 내려온다.
                // 이전 트윈의 −X 방향은 기계 안으로 파고들었다 — 설계자 지적으로 반전.
                // 설계자 지시(2026-08-25): 중간에서 멈추지 말고 끝까지 — 55°의 2배.
                _leverSM.Configure(_leverHandle, Vector3.right, 110f);
            }

            for (int i = 0; i < 5; i++)
            {
                var t = F("SM_SpinGauge_Cell_" + i);
                if (t != null) _spinPips[i] = t.GetComponent<Renderer>();
            }
            var sb = F("SM_Siren_Bulb");
            if (sb != null) _sirenBulb = sb.GetComponent<Renderer>();

            _doorL = F("SM_Door_L"); _doorR = F("SM_Door_R");
            if (_doorL != null) _doorLClosed = _doorL.localPosition;
            if (_doorR != null) _doorRClosed = _doorR.localPosition;

            var hf = F("SM_Harness_Fill");
            if (hf != null) _harnessFill = hf.GetComponent<Renderer>();

            if (CabinRoot != null)
            {
                var cf = HHSlotView.FindDeep(CabinRoot, "ChamberFillLight");
                if (cf != null) _chamberFill = cf.GetComponent<Light>();
            }

            BuildPowerBar();
            BuildGaugeText();
            // 인스턴스 머티리얼로 분리해 원본 에셋을 더럽히지 않는다
            foreach (var r in _spinPips) if (r != null) r.material = new Material(r.sharedMaterial);
            if (_sirenBulb != null) _sirenBulb.material = new Material(_sirenBulb.sharedMaterial);
        }

        /// <summary>계기 유리 안쪽에 물리 막대를 하나 만든다 — 전력이 얼마나 찼는지 3D 로 읽힌다.</summary>
        void BuildPowerBar()
        {
            var housing = F("SM_Gauge_Housing");
            if (housing == null) return;
            var hr = housing.GetComponent<Renderer>();
            if (hr == null) return;
            var b = hr.bounds;   // 월드 기준 (패널 회전 반영됨)

            var old = HHSlotView.FindDeep(transform, "HH_PowerBar");
            if (old != null) DestroyImmediate(old.gameObject);

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "HH_PowerBar";
            DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            // 계기 안쪽에 살짝 앞으로
            var front = b.center + _outward * (b.size.z * 0.55f + 0.012f);
            go.transform.position = new Vector3(b.min.x, b.center.y, front.z);
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = new Vector3(0.001f, b.size.y * 0.42f, 0.02f);
            _powerBar = go.transform;
            _powerBarR = go.GetComponent<Renderer>();
            var sh = Shader.Find("Universal Render Pipeline/Unlit");
            if (sh != null)
            {
                var m = new Material(sh);
                m.color = new Color(0.55f, 0.80f, 1.00f);
                _powerBarR.sharedMaterial = m;
            }
            _powerBarFullWidth = b.size.x * 0.94f;
            _powerBarLeftX = b.min.x + b.size.x * 0.03f;
            _powerBarZ = front.z;
            _powerBarY = b.center.y;
        }
        float _powerBarFullWidth, _powerBarLeftX, _powerBarZ, _powerBarY;

        /// <summary>계기 위 3D 숫자 — "지금 몇 W 찼는지"를 화면 UI 말고 기계에서도 읽게.</summary>
        void BuildGaugeText()
        {
            var housing = F("SM_Gauge_Housing");
            if (housing == null) return;
            var hr = housing.GetComponent<Renderer>();
            if (hr == null) return;
            var b = hr.bounds;

            var old = HHSlotView.FindDeep(transform, "HH_GaugeText");
            if (old != null) DestroyImmediate(old.gameObject);

            var go = new GameObject("HH_GaugeText");
            go.transform.SetParent(transform, false);
            go.transform.position = b.center + Vector3.up * (b.size.y * 0.02f) + _outward * (b.size.z * 0.70f + 0.015f);
            // 글자는 forward 가 카메라 시선과 같을 때 읽힌다 (실측 규칙)
            go.transform.rotation = Quaternion.LookRotation(-_outward, Vector3.up);
            go.transform.localScale = Vector3.one * 0.05f;
            var t = go.AddComponent<TextMeshPro>();
            t.font = HHUiKit.LoadFont();
            t.fontSize = 22f;
            t.alignment = TextAlignmentOptions.Center;
            t.enableWordWrapping = false;
            t.color = new Color(1f, 0.82f, 0.42f);
            t.GetComponent<RectTransform>().sizeDelta = new Vector2(44f, 6f);
            var mr = t.GetComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            _gaugeText = t;
        }

        // ── 매 프레임 값 흘리기 ──
        public void Sync(HHRun run)
        {
            if (run == null) return;

            // 스핀 게이지 5칸 = 남은 레버
            int left = run.LeversLeft;
            for (int i = 0; i < 5; i++)
            {
                var r = _spinPips[i];
                if (r == null) continue;
                bool on = i < left;
                var m = r.material;
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", on ? _pipOn : _pipOff);
                if (m.HasProperty("_EmissionColor"))
                {
                    m.EnableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", on ? _pipOn * 2.2f : Color.black);
                }
            }

            // 물리 전력 막대
            float ratio = run.EffReq > 0 ? Mathf.Clamp01(run.Power / (float)run.EffReq) : 0f;
            if (_powerBar != null)
            {
                float w = Mathf.Max(0.001f, _powerBarFullWidth * ratio);
                var s = _powerBar.localScale; s.x = w; _powerBar.localScale = s;
                _powerBar.position = new Vector3(_powerBarLeftX + w * 0.5f, _powerBarY, _powerBarZ);
                if (_powerBarR != null)
                {
                    var c = run.CanDepart ? new Color(0.56f, 0.81f, 0.43f) : new Color(0.55f, 0.80f, 1.00f);
                    _powerBarR.sharedMaterial.color = c;
                }
            }
            if (_gaugeText != null)
                _gaugeText.text = run.Power + " / " + run.EffReq + " W";

            // 챔버 조명 — 전력이 찰수록 밝아진다
            if (_chamberFill != null) _chamberFill.intensity = Mathf.Lerp(1.8f, 4.2f, ratio);

            // 하네스(안전벨트) 발광 — 문턱을 넘기면 켜진다
            if (_harnessFill != null)
            {
                var m = _harnessFill.material;
                if (m.HasProperty("_EmissionColor"))
                {
                    m.EnableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", run.CanDepart ? new Color(0.30f, 0.55f, 0.28f) : Color.black);
                }
            }

            // 사이렌 — 켜져 있으면 맥동
            if (_sirenBulb != null)
            {
                float k = _sirenT > 0f ? (0.5f + 0.5f * Mathf.Sin(Time.time * 18f)) : 0f;
                if (_sirenT > 0f) _sirenT -= Time.deltaTime;
                var m = _sirenBulb.material;
                if (m.HasProperty("_EmissionColor"))
                {
                    m.EnableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", new Color(1f, 0.25f, 0.15f) * (3.5f * k));
                }
            }
        }

        public void FlashSiren(float seconds) { _sirenT = Mathf.Max(_sirenT, seconds); }

        /// <summary>레버 손잡이를 실제로 내린다 — 이전 씬에 있던 그 손잡이, 이전 빌드의 그 감각이다.</summary>
        public IEnumerator PullLever()
        {
            if (_leverHandle == null) yield break;

            if (_leverSM != null)
            {
                // 직전 사이클이 복귀 중이면 잠깐 기다린다 — Pull() 은 Idle/Ready 에서만 받는다.
                float pre = 1.5f;
                while (pre > 0f && !_leverSM.AcceptsInput) { pre -= Time.deltaTime; yield return null; }

                _leverSM.Pull();

                // 걸림 + 장치 반응 지연(Processing 진입)까지 기다렸다가 돌아간다 — 릴은 그 순간부터 돈다.
                // 복귀(Processing→Completed→Resetting→Idle)는 상태머신이 스스로 진행한다.
                float guard = 3f;
                while (guard > 0f
                       && _leverSM.Current != Ascend.Prototype.View.LeverStateMachine.State.Processing
                       && _leverSM.Current != Ascend.Prototype.View.LeverStateMachine.State.Completed
                       && _leverSM.Current != Ascend.Prototype.View.LeverStateMachine.State.Idle)
                { guard -= Time.deltaTime; yield return null; }
                yield break;
            }

            // (예비) 상태머신을 못 붙인 경우에만 남는 단순 트윈. 보조 손잡이는 건드리지 않는다.
            float t = 0;
            while (t < 1f)
            {
                t += Time.deltaTime * 5.5f;
                float a = Mathf.Lerp(0, 42f, Mathf.SmoothStep(0, 1, t));
                _leverHandle.localRotation = _leverRest * Quaternion.Euler(a, 0, 0);
                yield return null;
            }
            yield return new WaitForSeconds(0.06f);
            t = 0;
            while (t < 1f)
            {
                t += Time.deltaTime * 2.6f;
                float a = Mathf.Lerp(42f, 0, Mathf.SmoothStep(0, 1, t));
                _leverHandle.localRotation = _leverRest * Quaternion.Euler(a, 0, 0);
                yield return null;
            }
            _leverHandle.localRotation = _leverRest;
        }

        /// <summary>출발할 때 문이 열렸다 닫힌다.</summary>
        public IEnumerator DoorCycle()
        {
            if (_doorL == null || _doorR == null) yield break;
            const float travel = 0.92f;
            float t = 0;
            while (t < 1f)
            {
                t += Time.deltaTime * 1.6f;
                float k = Mathf.SmoothStep(0, 1, t) * travel;
                _doorL.localPosition = _doorLClosed + new Vector3(0, 0, k);
                _doorR.localPosition = _doorRClosed + new Vector3(0, 0, -k);
                yield return null;
            }
            yield return new WaitForSeconds(0.35f);
            t = 0;
            while (t < 1f)
            {
                t += Time.deltaTime * 1.6f;
                float k = (1f - Mathf.SmoothStep(0, 1, t)) * travel;
                _doorL.localPosition = _doorLClosed + new Vector3(0, 0, k);
                _doorR.localPosition = _doorRClosed + new Vector3(0, 0, -k);
                yield return null;
            }
            _doorL.localPosition = _doorLClosed;
            _doorR.localPosition = _doorRClosed;
        }
    }
}
