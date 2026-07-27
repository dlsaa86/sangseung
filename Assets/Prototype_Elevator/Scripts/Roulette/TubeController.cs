using System.Collections.Generic;
using UnityEngine;

namespace Ascend.Prototype
{
    /// <summary>
    /// Controls a single tube: holds a seeded ball stream, scrolls it deterministically,
    /// handles brake delay, and selects the harvest-window ball on stop.
    /// T-02 Graybox — visuals are runtime-created spheres with URP/Unlit debugColor tint.
    /// </summary>
    public class TubeController : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private PrototypeConfig _config;

        [Header("Scene References")]
        [Tooltip("Parent transform for ball sphere GameObjects. Uses this transform if left null.")]
        [SerializeField] private Transform _ballContainer;

        [Tooltip("Optional harvest-window marker transform; its local Y is used as the harvest Y.")]
        [SerializeField] private Transform _harvestMarker;

        // ── Stream data ──
        private IReadOnlyList<BallDefinition> _stream;
        private float _scrollOffset;   // cumulative downward scroll in world units
        private bool  _isScrolling;
        private bool  _isBraking;
        private float _brakeTimer;
        private BallDefinition _stoppedBall;

        // ── Snap-to-harvest-line settle ──
        private bool  _isSnapping;
        private float _snapStartOffset;
        private float _snapTargetOffset;
        private float _snapDuration;
        private float _snapElapsed;

        // ── Visual pool ──
        private readonly List<GameObject>  _ballVisuals   = new List<GameObject>();
        private readonly List<Renderer>    _ballRenderers = new List<Renderer>();
        private Shader _ballSphereShader;

        // ── Public state ──

        /// <summary>True after FinalizeStop has been called and a StoppedBall has been confirmed.</summary>
        public bool IsStopped  => !_isScrolling && !_isBraking && !_isSnapping && _stoppedBall != null;

        /// <summary>True while the brake delay is counting down before the actual stop.</summary>
        public bool IsBraking  => _isBraking;

        /// <summary>True while the tube is easing the chosen ball onto the harvest line.</summary>
        public bool IsSnapping => _isSnapping;

        /// <summary>The ball confirmed at the harvest window; null until IsStopped.</summary>
        public BallDefinition StoppedBall => _stoppedBall;

        // ── Public API ──

        /// <summary>
        /// Assigns a ball stream and (re)creates the visual sphere pool.
        /// Call before StartScroll().
        /// </summary>
        public void SetStream(IReadOnlyList<BallDefinition> stream)
        {
            _stream = stream;
            EnsureVisualPool();
        }

        /// <summary>
        /// Starts scrolling from the beginning of the stream.
        /// </summary>
        public void StartScroll()
        {
            _scrollOffset = 0f;
            _isScrolling  = true;
            _isBraking    = false;
            _isSnapping   = false;
            _stoppedBall  = null;
            UpdateVisuals();
        }

        /// <summary>
        /// Requests a stop. After brakeDelay seconds the tube finalizes its stop.
        /// Ignored if already stopping or stopped.
        /// </summary>
        public void RequestStop()
        {
            if (_isBraking || _isSnapping || IsStopped) return;

            float delay = _config != null ? _config.brakeDelay : 0f;
            _isScrolling = false;

            // brakeDelay <= 0 means "the ball nearest the line right now is the one we take" —
            // begin the settle on the exact frame the input arrived.
            if (delay <= 0f)
            {
                _isBraking = false;
                BeginSnap();
                return;
            }

            // Movement continues inside the braking branch during the delay,
            // then the settle begins from wherever the reel ended up.
            _isBraking  = true;
            _brakeTimer = delay;
            Debug.Log($"[상승] {name}: Brake requested — delay {_brakeTimer:F2}s");
        }

        /// <summary>
        /// Resets the tube to a clean state (scroll stopped, no stopped ball, visuals cleared).
        /// </summary>
        public void ResetTube()
        {
            _isScrolling = false;
            _isBraking   = false;
            _isSnapping  = false;
            _brakeTimer  = 0f;
            _snapElapsed = 0f;
            _stoppedBall = null;
            _scrollOffset = 0f;
            HideAllVisuals();
        }

        // ── Unity lifecycle ──

        private void Update()
        {
            if (_stream == null || _stream.Count == 0) return;
            if (_config == null) return;

            float dt = Time.deltaTime;

            if (_isScrolling)
            {
                _scrollOffset += _config.ballMoveSpeed * dt;
            }
            else if (_isBraking)
            {
                // Continue scrolling during brake delay
                _scrollOffset += _config.ballMoveSpeed * dt;
                _brakeTimer   -= dt;
                if (_brakeTimer <= 0f)
                    BeginSnap();
            }
            else if (_isSnapping)
            {
                _snapElapsed += dt;
                float t = Mathf.Clamp01(_snapElapsed / _snapDuration);

                // Ease-out cubic: quick departure, gentle arrival — reads as the reel
                // clicking into place rather than drifting to a halt.
                float inv = 1f - t;
                float eased = 1f - inv * inv * inv;

                _scrollOffset = Mathf.Lerp(_snapStartOffset, _snapTargetOffset, eased);

                if (t >= 1f)
                {
                    _scrollOffset = _snapTargetOffset;   // land exactly, no float drift
                    _isSnapping   = false;
                    FinalizeStop();
                }
            }

            UpdateVisuals();
        }

        // ── Visual helpers ──

        private Transform BallParent => _ballContainer != null ? _ballContainer : transform;

        /// <summary>
        /// Grows the sphere pool to the required size, reusing what already exists.
        /// SetStream() runs on every spin, so rebuilding from scratch each time would churn
        /// 21 GameObjects per spin and leak the instantiated materials along with them.
        /// </summary>
        private void EnsureVisualPool()
        {
            if (_config == null) return;

            // The pool has to span the whole tube: slots occupy topY-frac down to
            // topY-frac-(count-1)*spacing, so cover tubeHeight plus one slot of headroom
            // for frac, otherwise a gap opens at the bottom on every wrap.
            float spacing  = Mathf.Max(0.0001f, _config.ballSpacing);
            int   required = Mathf.CeilToInt(_config.tubeHeight / spacing) + 1;
            int   count    = Mathf.Max(_config.visibleBallsPerTube, required);

            if (_ballSphereShader == null)
                _ballSphereShader = Shader.Find("Universal Render Pipeline/Unlit");

            for (int i = _ballVisuals.Count; i < count; i++)
            {
                GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.name = $"{name}_Ball_{i}";
                sphere.transform.SetParent(BallParent, false);
                sphere.transform.localScale = Vector3.one * 0.45f;

                // Remove collider — pure visual
                Collider col = sphere.GetComponent<Collider>();
                if (col != null) Destroy(col);

                // Create URP Unlit material with debugColor
                Renderer rend = sphere.GetComponent<Renderer>();
                if (rend != null && _ballSphereShader != null)
                {
                    var mat = new Material(_ballSphereShader);
                    mat.SetColor("_BaseColor", Color.white); // updated in UpdateVisuals
                    rend.material = mat;
                }

                _ballVisuals.Add(sphere);
                _ballRenderers.Add(rend);
            }
        }

        private void OnDestroy()
        {
            // rend.material instantiates a copy that Unity will not collect on its own.
            foreach (var rend in _ballRenderers)
            {
                if (rend != null && rend.material != null) Destroy(rend.material);
            }
        }

        private void HideAllVisuals()
        {
            foreach (var go in _ballVisuals)
            {
                if (go != null) go.SetActive(false);
            }
        }

        // ── Scroll math (shared by visuals and stop logic) ──
        //
        // Balls fall top → bottom. Ball k enters at topY when _scrollOffset == k * spacing,
        // so its local Y is  topY - (_scrollOffset - k * spacing).
        //
        // We render a fixed pool of `count` slots. Slot 0 is the newest ball at the top;
        // slot i trails it by i * spacing. Writing topIndex = floor(offset / spacing) and
        // frac = offset - topIndex * spacing, slot i sits at:
        //
        //     y_i = topY - frac - i * spacing
        //
        // As frac sweeps 0 → spacing every slot glides down by exactly one spacing. When it
        // wraps, topIndex increments and slot i inherits the ball slot i-1 was showing, so
        // the snap-up and the index shift cancel out — motion reads as continuous and each
        // ball keeps its colour for its whole descent.
        //
        // The previous code wrapped POSITION with period tubeHeight but advanced the COLOUR
        // index with period ballSpacing. Those periods disagree, so every colour in the tube
        // was replaced ballMoveSpeed/ballSpacing times per second — that was the flicker.

        private float Spacing => Mathf.Max(0.0001f, _config.ballSpacing);

        private float TopY => _config.tubeHeight * 0.5f;

        /// <summary>Index of the newest ball to have entered at the top of the tube.</summary>
        private int TopIndex => Mathf.FloorToInt(_scrollOffset / Spacing);

        /// <summary>Sub-spacing progress of the scroll, in [0, spacing).</summary>
        private float Frac => _scrollOffset - TopIndex * Spacing;

        /// <summary>Local Y of visual slot <paramref name="slot"/>.</summary>
        private float SlotY(int slot) => TopY - Frac - slot * Spacing;

        /// <summary>
        /// Y of the harvest line, expressed in the same space the ball visuals live in.
        /// The marker hangs off the tube while the balls hang off BallParent; converting
        /// through world space keeps alignment correct even if BallParent is ever offset.
        /// </summary>
        private float HarvestY
        {
            get
            {
                if (_harvestMarker == null) return _config.harvestWindowOffset;
                return BallParent.InverseTransformPoint(_harvestMarker.position).y;
            }
        }

        /// <summary>Stream entry currently occupying visual slot <paramref name="slot"/>.</summary>
        private int StreamIndexForSlot(int slot)
        {
            int n = _stream.Count;
            return ((TopIndex - slot) % n + n) % n;
        }

        private void UpdateVisuals()
        {
            if (_stream == null || _stream.Count == 0) return;
            if (_config == null) return;

            int count = _ballVisuals.Count;

            for (int i = 0; i < count; i++)
            {
                if (_ballVisuals[i] == null) continue;

                _ballVisuals[i].transform.localPosition = new Vector3(0f, SlotY(i), 0f);
                _ballVisuals[i].SetActive(true);

                BallDefinition ball = _stream[StreamIndexForSlot(i)];
                if (ball != null && _ballRenderers[i] != null && _ballRenderers[i].material != null)
                {
                    _ballRenderers[i].material.SetColor("_BaseColor", ball.debugColor);
                }
            }
        }

        // ── Stop logic ──

        /// <summary>
        /// Chooses the ball closest to the harvest line right now and starts easing the reel
        /// until that ball sits dead centre on the line.
        /// </summary>
        private void BeginSnap()
        {
            _isScrolling = false;
            _isBraking   = false;
            _brakeTimer  = 0f;

            if (_stream == null || _stream.Count == 0 || _config == null || _ballVisuals.Count == 0)
            {
                FinalizeStop();
                return;
            }

            // Ball m sits at  TopY - (_scrollOffset - m * Spacing), so the offset that puts it
            // exactly on the line is  anchor + m * Spacing  where  anchor = TopY - HarvestY.
            // Rounding picks whichever ball is nearest at this instant, which may nudge the
            // reel back by up to half a spacing — that settle-back is the intended feel.
            float anchor = TopY - HarvestY;
            int   m      = Mathf.RoundToInt((_scrollOffset - anchor) / Spacing);

            _snapStartOffset  = _scrollOffset;
            _snapTargetOffset = anchor + m * Spacing;
            _snapDuration     = _config.snapDuration;
            _snapElapsed      = 0f;

            if (_snapDuration <= 0f)
            {
                _scrollOffset = _snapTargetOffset;
                _isSnapping   = false;
                FinalizeStop();
                return;
            }

            _isSnapping = true;
        }

        private void FinalizeStop()
        {
            _isScrolling = false;
            _isBraking   = false;
            _brakeTimer  = 0f;

            if (_stream == null || _stream.Count == 0 || _config == null)
            {
                Debug.LogWarning($"[상승] {name}: FinalizeStop — stream or config missing.");
                return;
            }

            if (_ballVisuals.Count == 0)
            {
                Debug.LogWarning($"[상승] {name}: FinalizeStop — visual pool empty; call SetStream() first.");
                return;
            }

            float harvestY = HarvestY;

            // Pick the slot nearest the harvest window using the SAME math the visuals use,
            // so the ball the player sees in the window is the ball that gets scored.
            int   count    = _ballVisuals.Count;
            float bestDist = float.MaxValue;
            int   bestSlot = 0;

            for (int i = 0; i < count; i++)
            {
                float dist = Mathf.Abs(SlotY(i) - harvestY);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestSlot = i;
                }
            }

            int streamIdx = StreamIndexForSlot(bestSlot);
            _stoppedBall  = _stream[streamIdx];

            Debug.Log($"[상승] {name}: STOPPED — StoppedBall = {(_stoppedBall != null ? _stoppedBall.id : "null")} (slot {bestSlot}, streamIdx {streamIdx}, dist {bestDist:F3})");
        }
    }
}
