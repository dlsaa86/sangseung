using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ascend.CaptureHarness
{
    /// <summary>
    /// One deterministic screenshot: an optional camera pose, an optional key sequence to
    /// drive the game into a state, and how many frames to settle before reading pixels.
    /// </summary>
    [Serializable]
    public class CaptureShot
    {
        [Tooltip("Used as the PNG filename. Keep stable across runs — comparison is filename-matched.")]
        public string Name = "shot";

        [Tooltip("When false the camera is left wherever the scene/gameplay put it.")]
        public bool OverridePose = true;

        public Vector3 Position;
        public Vector3 EulerAngles;
        [Range(1f, 179f)] public float FieldOfView = 60f;

        [Tooltip("InputSystem key names pressed one after another before this shot (e.g. Space, Digit1). " +
                 "Applied only if the Input System is present.")]
        public string[] KeysBefore = Array.Empty<string>();

        [Tooltip("Frames to advance after posing/input before reading pixels. " +
                 "Raise this when TAA or animated effects need to converge.")]
        [Min(0)] public int SettleFrames = 4;
    }

    /// <summary>
    /// A reproducible capture run. Every field here is part of the comparison contract:
    /// two runs are only comparable if they used the same CaptureSet.
    /// </summary>
    [CreateAssetMenu(menuName = "Capture Harness/Capture Set", fileName = "CaptureSet")]
    public class CaptureSet : ScriptableObject
    {
        [Header("Scene")]
        [Tooltip("Scene asset opened before entering Play Mode. Leave empty to use the currently open scene.")]
        public string ScenePath = "Assets/Prototype_Elevator/Scenes/Prototype_Elevator.unity";

        [Header("Determinism")]
        [Tooltip("Fixed frame duration during capture. Decouples the run from real framerate.")]
        [Min(1)] public int CaptureFps = 60;

        [Tooltip("Seeds UnityEngine.Random before the first frame.")]
        public int RandomSeed = 12345;

        [Tooltip("Frames advanced after scene load before the first shot. " +
                 "Covers Start(), first-frame allocations and post-processing warmup.")]
        [Min(0)] public int WarmupFrames = 30;

        [Header("Output")]
        [Min(16)] public int Width = 960;
        [Min(16)] public int Height = 540;

        [Tooltip("MSAA sample count for the capture target. 1 = off.")]
        public int MsaaSamples = 1;

        [Header("Shots")]
        public List<CaptureShot> Shots = new List<CaptureShot>
        {
            new CaptureShot { Name = "01_initial", OverridePose = false }
        };
    }
}
