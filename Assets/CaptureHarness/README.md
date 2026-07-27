# Capture Harness

Deterministic Play Mode screenshot capture. Its only job is to make two runs
**comparable**: same resolution, same clock, same seed, same camera poses. Without
that, "is this build better than the last one?" is unanswerable — a one-frame
lighting difference flips the verdict.

## Why each knob exists

| Knob | Without it |
|---|---|
| `Time.captureDeltaTime` | Frame timing follows real framerate, so animated state lands differently every run. |
| `RandomSeed` | Any future `Random` call desynchronises the runs. |
| Fixed-size `RenderTexture` | `ScreenCapture` follows the Game View size, which changes when you resize a window. |
| `WarmupFrames` | `Start()`, first-frame allocations and TAA convergence pollute the first shots. |
| Named shots | Comparison is filename-matched. Renaming a shot breaks the history for that angle. |
| Explicit render request | `WaitForEndOfFrame` only resumes when the Game View repaints, so an unfocused editor would hang the run forever. |
| `Application.runInBackground` | The player loop stalls when the editor loses focus, so an unattended run would never progress. |

Verified: two consecutive runs of the same set produce byte-identical PNGs.

## Usage

1. Create a set: **Assets → Create → Capture Harness → Capture Set**
2. Fill in `ScenePath`, resolution, and one entry per `Shot`.
3. Select the asset, then **Tools → Capture Harness → Run Selected Capture Set**.

The editor opens the scene, enters Play Mode, captures, and exits Play Mode by itself.

## Output

```
Captures/
  last-run.txt                        # runId of the newest run
  20260727-213045_MyCaptureSet/
    00_initial.png
    01_after_spin.png
    manifest.json
```

`manifest.json` records the conditions and every Error/Exception/Assert logged during
the run. **A run with `errorCount > 0` is not a valid comparison baseline** — fix the
errors before judging the visuals.

## Driving the game into a state

`KeysBefore` queues Input System key presses before a shot (the prototype polls
`Keyboard.current`, so simulated events are seen normally). Key names are
`UnityEngine.InputSystem.Key` values:

```
KeysBefore: ["Space"]          → press Space, then capture
KeysBefore: ["Space", "Digit1"]
```

## UI capture

Screen Space **Overlay** canvases draw straight to the backbuffer and bypass any camera
`targetTexture` — left alone, UI would be silently missing from every shot. The session
therefore switches every Overlay canvas to Screen Space **Camera** for the duration of the
run (pinned just past the near clip plane so it still renders on top) and restores it
afterwards. The scene asset is never modified.

Side effect: UI is now inside the 3D render, so anything closer to the camera than
`nearClipPlane + 0.01` would occlude it. Nothing does today; if that changes, the UI will
start getting covered rather than silently disappearing.

## Known limits

- The main camera renders into the capture RenderTexture for the duration of the run,
  so the **Game View is blank while capturing**. This is expected.
- Shots are captured in order in a single Play Mode session; state accumulates from
  shot to shot. Order is part of the contract.
- `OverridePose = false` captures wherever gameplay left the camera — convenient, but
  only deterministic if the gameplay leading up to it is.
