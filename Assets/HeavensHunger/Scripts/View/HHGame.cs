// HHGame.cs — 3D 씬의 게임 컨트롤러. 코어(HHRun)와 뷰(HHSlotView·HHHud·HHAudio)를 잇는다.
using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace HeavensHunger
{
    public class HHGame : MonoBehaviour
    {
        public HHSlotView Slot;
        public HHHud Hud;
        public HHCabinRig Rig;
        public string Seed = "";
        public float AnimSpeed = 1f;

        public HHRun Run { get; private set; }
        bool _busy;
        Camera _cam;

        void Start()
        {
            _cam = Camera.main;
            if (Rig == null) Rig = GetComponent<HHCabinRig>();
            if (GetComponent<HHAudio>() == null) gameObject.AddComponent<HHAudio>();
            if (Hud == null) Hud = gameObject.AddComponent<HHHud>();
            Hud.Game = this;
            Hud.Build();
            NewRun();
            AimAtMachine();
        }

        /// <summary>기계를 정면으로 보는 자리에 플레이어를 세운다. 모델에서 바깥방향을 재서 쓴다.</summary>
        public void AimAtMachine()
        {
            if (Slot == null || Slot.CabinRoot == null) return;
            var outward = HHSlotView.MachineOutward(Slot.CabinRoot);
            var glass = HHSlotView.FindDeep(Slot.CabinRoot, "TEST_H_Glass");
            var gr = glass != null ? glass.GetComponent<Renderer>() : null;
            if (gr == null) return;
            var c = gr.bounds.center;
            var player = GameObject.Find("Player");
            if (player != null)
            {
                var pos = c + outward * 2.95f;
                player.transform.position = new Vector3(pos.x, player.transform.position.y, pos.z);
                player.transform.rotation = Quaternion.LookRotation(-outward, Vector3.up);
            }
            if (_cam != null)
            {
                _cam.transform.localRotation = Quaternion.identity;
                _cam.fieldOfView = 52f;
            }
        }

        public void NewRun()
        {
            Run = new HHRun(string.IsNullOrEmpty(Seed) ? System.DateTime.Now.Ticks.ToString() : Seed);
            if (Slot != null) { Slot.EnsureBuilt(); Slot.ClearBoard(); }
            Hud.Refresh();
        }

        void LateUpdate()
        {
            if (Rig != null) Rig.Sync(Run);
        }

        void Update()
        {
            if (_busy) return;
#if ENABLE_INPUT_SYSTEM
            // 이 프로젝트는 Input System 패키지 전용이라 레거시 Input 을 쓰면 매 프레임 예외가 난다.
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.spaceKey.wasPressedThisFrame) DoLever();
            else if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) DoDepart();
            else if (kb.tabKey.wasPressedThisFrame) Hud.ToggleShop();
            else if (kb.qKey.wasPressedThisFrame) Hud.ToggleOdds();
            else if (kb.eKey.wasPressedThisFrame) Hud.ToggleLines();
            else if (kb.fKey.wasPressedThisFrame) Hud.ToggleRoster();
            else if (kb.yKey.wasPressedThisFrame) { AnswerOffer(true); Hud.Refresh(); }
            else if (kb.nKey.wasPressedThisFrame) { AnswerOffer(false); Hud.Refresh(); }
            else if (kb.rKey.wasPressedThisFrame) NewRun();
#else
            if (Input.GetKeyDown(KeyCode.Space)) DoLever();
            else if (Input.GetKeyDown(KeyCode.Return)) DoDepart();
            else if (Input.GetKeyDown(KeyCode.Tab)) Hud.ToggleShop();
            else if (Input.GetKeyDown(KeyCode.Q)) Hud.ToggleOdds();
            else if (Input.GetKeyDown(KeyCode.E)) Hud.ToggleLines();
            else if (Input.GetKeyDown(KeyCode.F)) Hud.ToggleRoster();
            else if (Input.GetKeyDown(KeyCode.R)) NewRun();
#endif
        }

        /// <summary>문 앞의 사람 → 인터폰 순으로 답한다.</summary>
        public void AnswerOffer(bool yes)
        {
            if (Run == null || Run.Offers == null) return;
            if (Run.Offers.Passenger != null)
            {
                if (yes) Run.BoardPassenger(); else Run.RefusePassenger();
                return;
            }
            if (Run.Offers.Deal != null && !Run.Offers.DealTaken)
            {
                if (yes) { if (!Run.AcceptDeal()) Run.LogLine("지금은 받을 수 없는 거래다"); }
                else Run.RefuseDeal();
            }
        }

        public void DoLever()
        {
            if (_busy || Run == null || Run.Dead || Run.Finished) return;
            if (Run.LeversLeft <= 0) return;
            StartCoroutine(LeverRoutine());
        }

        IEnumerator LeverRoutine()
        {
            _busy = true;
            if (HHAudio.I != null) HHAudio.I.Lever();
            if (Rig != null) yield return StartCoroutine(Rig.PullLever());
            else if (Slot != null) yield return StartCoroutine(Slot.PullLeverAnim());
            var rep = Run.PullLever();
            if (rep != null && Slot != null)
            {
                yield return StartCoroutine(Slot.SpinAnim(Run, AnimSpeed));
                Hud.Refresh();
                bool zig = false;
                if (rep.R != null) foreach (var e in rep.R.Events) if (e.Zig) zig = true;
                if (zig && Rig != null) Rig.FlashSiren(1.6f);
                if (rep.BellRing && Rig != null) Rig.FlashSiren(0.8f);
                yield return StartCoroutine(Slot.RevealCrescendo(Run, Hud, _cam));
                Slot.Render(Run);            // 최종 상태 확정
            }
            else if (Slot != null) Slot.Render(Run);
            Hud.Refresh();
            _busy = false;
        }

        public void DoDepart()
        {
            if (_busy || Run == null || !Run.CanDepart) return;
            if (HHAudio.I != null) HHAudio.I.Depart();
            Run.Depart();
            if (Rig != null) StartCoroutine(Rig.DoorCycle());
            if (Slot != null) Slot.ClearBoard();
            Hud.Refresh();
        }
    }
}
