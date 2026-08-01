using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Ascend.CaptureHarness;

namespace Ascend.Prototype.Run.Tests
{
    /// <summary>
    /// 정해진 카메라로 정해진 프레임 수만큼 찍어 애니메이션 GIF 로 남긴다.
    ///
    /// 왜 정지 캡처로 부족한가: `MASTER_PRD.md` §13.5 가 증거 산출물로 "핵심 흐름 영상"을
    /// 요구한다. 5연쇄와 Critical→과수확→결과는 **시간이 지나야 성립하는 장면**이라
    /// 한 장으로는 "정말 5단계였는가"를 보일 수 없다. 캡처 15번은 8단계 중 5단계를
    /// 찍은 한 장이고, 그 한 장은 나머지 7단계가 있었다는 사실을 증명하지 못한다.
    ///
    /// 결정론: `Time.captureDeltaTime` 을 고정해 프레임 간격을 실제 프레임률에서 떼어낸다.
    /// 캡처 하네스가 같은 이유로 하는 일이며(그 README 의 표), 안 하면 기기가 느린 날
    /// 다른 장면이 찍힌다.
    /// </summary>
    public sealed class SequenceRecorder : MonoBehaviour
    {
        /// <summary>기본 녹화 폭. 증거용이라 화질보다 파일 크기와 메모리를 우선한다.</summary>
        public const int DefaultWidth = 480;

        /// <summary>기본 초당 프레임. GIF 지연 단위가 1/100초라 20fps(5cs)가 정확히 떨어진다.</summary>
        public const int DefaultFps = 20;

        private readonly List<byte[]> _frames = new List<byte[]>();
        private Camera _camera;
        private RenderTexture _target;
        private Texture2D _readback;
        private Texture2D _scaled;
        private bool _screenCapture;
        private int _width;
        private int _height;
        private int _maxFrames;
        private bool _recording;
        private float _previousCaptureDelta;

        /// <summary>지금까지 모은 프레임 수.</summary>
        public int FrameCount => _frames.Count;

        /// <summary>녹화 중인가.</summary>
        public bool IsRecording => _recording;

        /// <summary>
        /// 녹화를 시작한다. 이미 녹화 중이면 아무것도 하지 않는다.
        /// </summary>
        /// <param name="camera">찍을 카메라. null 이면 <see cref="Camera.main"/>.</param>
        /// <param name="width">가로 픽셀. 세로는 카메라 종횡비로 정한다.</param>
        /// <param name="fps">초당 프레임.</param>
        /// <param name="maxSeconds">
        /// 최대 녹화 시간. 프레임을 메모리에 모으므로 상한이 필요하다 —
        /// 480×270 RGB24 는 프레임당 약 389KB 이고 10초면 78MB 다.
        /// </param>
        public void Begin(Camera camera = null, int width = DefaultWidth,
            int fps = DefaultFps, float maxSeconds = 12f, bool screenCapture = false)
        {
            if (_recording) return;

            _screenCapture = screenCapture;
            _camera = camera != null ? camera : Camera.main;

            // 화면 캡처는 카메라를 쓰지 않는다 — 게임 뷰가 그린 것을 그대로 읽으므로
            // `Camera.main` 이 없어도 돈다. 종횡비만 화면에서 가져온다.
            if (_camera == null && !_screenCapture)
            {
                Debug.LogError("[상승] SequenceRecorder: 찍을 카메라가 없다. 녹화를 시작하지 않는다.", this);
                return;
            }

            _width = Mathf.Max(16, width);
            float aspect = _screenCapture
                ? (Screen.height > 0 ? Screen.width / (float)Screen.height : 16f / 9f)
                : (_camera.aspect > 0.01f ? _camera.aspect : 16f / 9f);
            // 세로는 짝수로 맞춘다. 홀수 높이는 일부 뷰어에서 마지막 줄이 잘린다.
            _height = Mathf.Max(16, Mathf.RoundToInt(_width / aspect) & ~1);

            _maxFrames = Mathf.Max(1, Mathf.RoundToInt(fps * Mathf.Max(0.1f, maxSeconds)));
            Fps = Mathf.Clamp(fps, 1, 50);

            if (!_screenCapture)
            {
                _target = new RenderTexture(_width, _height, 24,
                    RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
                {
                    name = "SequenceRecorderTarget",
                    antiAliasing = 1,
                };
                _target.Create();
                _readback = new Texture2D(_width, _height, TextureFormat.RGBA32, false);
            }

            _frames.Clear();
            _previousCaptureDelta = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / Fps;
            _recording = true;

            if (_screenCapture) StartCoroutine(ScreenGrabLoop());
        }

        /// <summary>녹화 프레임률. <see cref="Begin"/> 이 정한다.</summary>
        public int Fps { get; private set; } = DefaultFps;

        /// <summary>
        /// 화면을 그대로 찍는다(= 게임 뷰). **`ScreenSpaceOverlay` HUD 가 들어간다.**
        ///
        /// 카메라 렌더 경로로는 못 담는다는 것을 정지 캡처에서 한 번 찾아 고쳐 놓고
        /// 영상에서 똑같이 반복했다 — 「연쇄 N단계」를 보이려고 만든 HUD 가 정작
        /// 그것을 증명해야 할 영상에서 빠졌고, 「깊이 5」의 근거가 로그 텍스트뿐이었다.
        /// 독립 감사가 잡았다.
        ///
        /// 대가: 해상도가 게임 뷰에 종속되고 프레임당 비용이 크다. 그래서 기본값이
        /// 아니라 `Begin(..., screenCapture: true)` 로 켠다 — 증거 영상만 이 경로를 쓴다.
        /// </summary>
        public bool CapturesScreen => _screenCapture;

        private void LateUpdate()
        {
            if (!_recording || _screenCapture) return;
            if (_frames.Count >= _maxFrames) { StopRecording(); return; }
            Grab();
        }

        private System.Collections.IEnumerator ScreenGrabLoop()
        {
            // `CaptureScreenshotAsTexture` 는 프레임 끝에서만 유효하다. `LateUpdate` 에서
            // 부르면 이번 프레임의 UI 가 아직 안 그려져 있다.
            while (_recording && _frames.Count < _maxFrames)
            {
                yield return new WaitForEndOfFrame();
                if (!_recording) break;

                Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
                try
                {
                    // 가로 폭을 요청값에 맞춰 줄인다. 화면 그대로면 프레임당 2MB 를 넘어
                    // 12초 녹화가 메모리를 삼킨다.
                    int w = Mathf.Max(16, _width);
                    int h = Mathf.Max(16, Mathf.RoundToInt(w * shot.height / (float)shot.width) & ~1);
                    if (_scaled == null || _scaled.width != w || _scaled.height != h)
                    {
                        if (_scaled != null) Destroy(_scaled);
                        _scaled = new Texture2D(w, h, TextureFormat.RGBA32, false);
                    }
                    Downsample(shot, _scaled);
                    _frames.Add(GifEncoder.FromBottomUpRgba(_scaled.GetRawTextureData(), w, h));
                }
                finally
                {
                    Destroy(shot);
                }
            }
            if (_frames.Count >= _maxFrames) StopRecording();
        }

        /// <summary>최근접 축소. 증거 영상이라 화질보다 **재현성과 비용**이 앞선다.</summary>
        private static void Downsample(Texture2D source, Texture2D target)
        {
            Color32[] src = source.GetPixels32();
            Color32[] dst = new Color32[target.width * target.height];
            for (int y = 0; y < target.height; y++)
            {
                int sy = y * source.height / target.height;
                for (int x = 0; x < target.width; x++)
                {
                    int sx = x * source.width / target.width;
                    dst[y * target.width + x] = src[sy * source.width + sx];
                }
            }
            target.SetPixels32(dst);
            target.Apply(false);
        }

        private void Grab()
        {
            RenderTexture previousCameraTarget = _camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;

            _camera.targetTexture = _target;
            _camera.Render();
            _camera.targetTexture = previousCameraTarget;

            RenderTexture.active = _target;
            _readback.ReadPixels(new Rect(0, 0, _width, _height), 0, 0, false);
            _readback.Apply(false);
            RenderTexture.active = previousActive;

            _frames.Add(GifEncoder.FromBottomUpRgba(_readback.GetRawTextureData(), _width, _height));
        }

        /// <summary>프레임 수집만 멈춘다. 파일은 쓰지 않는다.</summary>
        public void StopRecording()
        {
            if (!_recording) return;
            _recording = false;
            Time.captureDeltaTime = _previousCaptureDelta;
        }

        /// <summary>
        /// 지금까지 모은 프레임을 GIF 로 쓰고 버퍼를 비운다.
        /// 쓴 경로를 돌려준다. 프레임이 없으면 null.
        /// </summary>
        public string WriteTo(string path)
        {
            StopRecording();
            if (_frames.Count == 0)
            {
                Debug.LogWarning($"[상승] SequenceRecorder: 프레임이 0장이라 {path} 를 쓰지 않는다.", this);
                return null;
            }

            try
            {
                GifEncoder.Write(path, _frames, _width, _height, Mathf.Max(1, 100 / Fps));
            }
            catch (Exception e)
            {
                Debug.LogError($"[상승] SequenceRecorder: GIF 쓰기 실패 — {e.GetType().Name}: {e.Message}", this);
                return null;
            }
            finally
            {
                _frames.Clear();
                ReleaseTargets();
            }

            var info = new FileInfo(path);
            Debug.Log($"[상승] 영상 기록 — {path} ({_width}×{_height}, {info.Length / 1024}KB)");
            return path;
        }

        private void OnDestroy()
        {
            StopRecording();
            ReleaseTargets();
        }

        private void ReleaseTargets()
        {
            if (_target != null)
            {
                if (RenderTexture.active == _target) RenderTexture.active = null;
                _target.Release();
                Destroy(_target);
                _target = null;
            }
            if (_readback != null)
            {
                Destroy(_readback);
                _readback = null;
            }
        }

        /// <summary>
        /// 조건이 참이 될 때까지 녹화하며 기다린다. 최대 시간을 넘기면 그대로 멈춘다 —
        /// 조건이 영영 오지 않는 런에서 자동화가 매달리지 않게 한다.
        /// </summary>
        public IEnumerator RecordUntil(Func<bool> done, float timeoutSeconds)
        {
            float elapsed = 0f;
            while (_recording && elapsed < timeoutSeconds)
            {
                if (done != null && done()) yield break;
                elapsed += Time.deltaTime;
                yield return null;
            }
        }
    }
}
