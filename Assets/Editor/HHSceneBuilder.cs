// HHSceneBuilder.cs — 해븐즈헝거 씬을 **이전 씬(Prototype_Elevator)에서 복제해서** 짓는다.
//
// ⚠ 이전 판(2026-08-25 오전)은 빈 씬에서 새로 지었고, 그래서 텍스처·무드·조명·레버·계기판·
//   파티클·사운드·문/승객 정거장·1인칭 플레이어를 전부 버렸다. 그건 잘못이었다.
//   지금은 이전 씬을 통째로 물려받고 **3×3 패널만 5×3으로 갈아끼운다.**
//
// 정렬 근거(실측): 이전 씬 CabinAD47 은 rot Y=180. 내 FBX 를 그 자식으로 localRotation=identity 로
//   넣으면 월드에서 정확히 겹친다 — SM_Cab_Wall_Back z: FBX −2.661 → 회전 후 +2.661 (이전 씬과 일치).
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using HeavensHunger;

public static class HHSceneBuilder
{
    const string FBX = "Assets/HeavensHunger/Art/ELV_Cabin_5x3.fbx";
    const string SRC_SCENE = "Assets/Prototype_Elevator/Scenes/Prototype_Elevator.unity";
    const string SCENE = "Assets/HeavensHunger/Scenes/HeavensHunger.unity";

    [MenuItem("HeavensHunger/씬 다시 짓기 (이전 씬 + 5x3 패널)", priority = 30)]
    public static void BuildScene()
    {
        RemapMaterials();

        // 1) 이전 씬을 열어 그대로 물려받는다
        var scene = EditorSceneManager.OpenScene(SRC_SCENE, OpenSceneMode.Single);

        // 2) 3×3 패널 → 5×3 패널 교체
        var cabin = GameObject.Find("CabinAD47");
        if (cabin == null) { Debug.LogError("[HH] CabinAD47 없음 — 이전 씬이 바뀌었나?"); return; }

        // 2-a) 이전 판 잔재 제거 (다시 지을 때 중복 방지)
        var old5 = FindChildByName(cabin.transform, "HH_Panel_5x3");
        if (old5 != null) Object.DestroyImmediate(old5.gameObject);

        // 2-b) FBX 가 대체하는 옛 메시를 끈다. 씬에서 손으로 붙인 것(콜라이더·조명·앵커)은 남긴다.
        var keep = new HashSet<string> {
            "ShellCollision", "FX_MachineAnchor", "ChamberFillLight", "LT_CabBulb", "MachineScreen"
        };
        int turnedOff = 0;
        var toDisable = new List<GameObject>();
        foreach (Transform c in cabin.transform)
        {
            if (keep.Contains(c.name)) continue;
            if (c.name == "HH_Panel_5x3") continue;
            toDisable.Add(c.gameObject);
        }
        foreach (var g in toDisable) { g.SetActive(false); turnedOff++; }

        // 2-c) 5×3 캐빈을 CabinAD47 자식으로 (localRotation identity → 월드에서 정확히 겹친다)
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FBX);
        if (prefab == null) { Debug.LogError("[HH] FBX 없음: " + FBX); return; }
        var panel = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        panel.name = "HH_Panel_5x3";
        panel.transform.SetParent(cabin.transform, false);
        panel.transform.localPosition = Vector3.zero;
        panel.transform.localRotation = Quaternion.identity;
        panel.transform.localScale = Vector3.one;

        // 2-d) 블렌더 임시물 / 옛 계기 텍스처 정리
        foreach (var t in panel.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "TEST_H_Cores") { t.gameObject.SetActive(false); continue; }   // 자홍색 임시 발광구
            if (t.name == "SM_Gauge_Screen") { t.gameObject.SetActive(false); continue; } // 옛 영어 계기 텍스처
            var mr = t.GetComponent<MeshRenderer>();
            if (mr == null) continue;
            foreach (var m in mr.sharedMaterials)
                if (m == null || m.shader == null || m.shader.name == "Hidden/InternalErrorShader") { mr.enabled = false; break; }
        }

        // 3) 옛 3×3 코어를 돌리던 드라이버만 끈다 (연출·오디오·파티클은 살린다)
        int drivers = DisableOldCoreDrivers();

        // 4) 게임 오브젝트
        var gameGo = GameObject.Find("HH_Game");
        if (gameGo != null) Object.DestroyImmediate(gameGo);
        gameGo = new GameObject("HH_Game");
        var slot = gameGo.AddComponent<HHSlotView>();
        slot.CabinRoot = panel.transform;
        var hud = gameGo.AddComponent<HHHud>();
        var game = gameGo.AddComponent<HHGame>();
        game.Slot = slot;
        game.Hud = hud;
        hud.Game = game;

        // 5) 물리 계기 재배선 — 이전 씬의 진짜 오브젝트를 그대로 쓴다
        var rig = gameGo.AddComponent<HHCabinRig>();
        rig.Bind(panel.transform, cabin.transform);

        // 6) 카메라: 1인칭 플레이어를 기계 앞에 세운다
        PlacePlayer(panel.transform);

        // 7) 환경값은 이전 씬 것을 그대로 둔다 (포스트/안개/앰비언트/반사) — 손대지 않는다
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(SCENE));
        EditorSceneManager.SaveScene(scene, SCENE);

        Debug.Log("[HH] 씬 = 이전 씬 + 5x3 패널\n"
                + "  옛 캐빈 메시 " + turnedOff + "개 비활성 · 옛 코어 드라이버 " + drivers + "개 비활성\n"
                + "  살린 것: 포스트볼륨 · 안개 · 조명 11 · ReferenceRoom(레버베이스·상승칼럼·전력계·창고·경고등) · 문리그 · 승객정거장 · 1인칭 플레이어 · AudioDirector · AmbientParticleDirector\n"
                + "  Space=레버 · Enter=출발 · Y/N=응답 · Tab=상점 · F=명부 · Q=확률표 · E=줄표 · R=새 판");
    }

    /// <summary>옛 3×3 코어(RunSession)를 돌리던 컴포넌트만 끈다. 소리·파티클·소품은 건드리지 않는다.</summary>
    static int DisableOldCoreDrivers()
    {
        string[] kill = {
            "RunSessionBehaviour", "SpinBoardView", "SpinPresenter", "RouletteInteractionBridge",
            "RiskEventBridge", "OverharvestApproachBridge", "CollapseSequence", "PassengerReactionView",
            "RiskStateView", "AccidentRecorder", "TelemetryRecorderBehaviour", "MemoryTrendProbe",
            "RenderBudgetProbe", "CellGlowView", "MachineScreenView", "PowerGaugeView",
            "AscentColumnView", "InstrumentPanelView", "FloorNumberDisplayView", "FloorIndicatorView",
            "PurifyMarkerView", "BuildFigureView", "GameHudView", "DebugPanelView", "RoundSandbox",
            "OverharvestStageView", "PaperTapePrinterView", "ContractPanelGrayboxView",
            "ElevatorGrayboxView", "PassengerEntryAnimator", "TubeController", "MachineImpactView",
            "SoulReelView", "CustomsLockView", "PassengerStationSet", "MachineFocusController"
        };
        var set = new HashSet<string>(kill);
        int n = 0;
        foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (mb == null) continue;
            if (!set.Contains(mb.GetType().Name)) continue;
            mb.enabled = false; n++;
        }
        // 옛 HUD 캔버스는 화면을 가리므로 끈다 (내 HUD 가 대신한다)
        foreach (var nm in new[] { "GameHUD", "PrototypeUI", "Canvas", "GrayboxWorld", "TubesRoot", "GB_AscendControls", "MachineScreen" })
        {
            var g = GameObject.Find(nm);
            if (g != null) g.SetActive(false);
        }
        return n;
    }

    static void PlacePlayer(Transform panel)
    {
        var player = GameObject.Find("Player");
        var cell = HHSlotView.FindDeep(panel, "HH_Cell_07");
        var glass = HHSlotView.FindDeep(panel, "TEST_H_Glass");
        Vector3 outward = HHSlotView.MachineOutward(panel);
        Vector3 boardCenter = glass != null && glass.GetComponent<Renderer>() != null
            ? glass.GetComponent<Renderer>().bounds.center
            : (cell != null ? cell.position : Vector3.zero);

        // \ub9b4\uc774 \ud654\uba74\uc5d0 \ub2e4 \ub4e4\uc5b4\uc624\uace0 \ubc29\ub3c4 \ubcf4\uc774\ub294 \uac70\ub9ac
        Vector3 camPos = boardCenter + outward * 3.15f + Vector3.up * 0.10f;
        if (player != null)
        {
            player.transform.position = new Vector3(camPos.x, 0f, camPos.z);
            player.transform.rotation = Quaternion.LookRotation(-outward, Vector3.up);
            var head = HHSlotView.FindDeep(player.transform, "Head");
            if (head != null) head.localRotation = Quaternion.identity;
            var rig = HHSlotView.FindDeep(player.transform, "CameraRig");
            if (rig != null) rig.localRotation = Quaternion.identity;
        }
        var cam = Camera.main;
        if (cam != null)
        {
            cam.transform.localRotation = Quaternion.identity;
            cam.fieldOfView = 52f;
            cam.nearClipPlane = 0.05f;
            var ad = cam.GetComponent<UniversalAdditionalCameraData>();
            if (ad == null) ad = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            ad.renderPostProcessing = true;
            ad.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        }
    }

    static Transform FindChildByName(Transform root, string n)
    {
        foreach (Transform c in root) if (c.name == n) return c;
        return null;
    }

    /// <summary>
    /// ⚠ 첫 임포트 때 FBX 가 **같은 이름의 빈 머티리얼**을 새로 깔고 그걸 물어버렸다.
    /// 진짜는 Assets/Prototype_Elevator/Materials/ 에 있다(TEX_Iron_Rust 등).
    /// </summary>
    [MenuItem("HeavensHunger/머티리얼 재연결 (원본 캐빈 머티리얼로)", priority = 31)]
    public static void RemapMaterials()
    {
        var mi = AssetImporter.GetAtPath(FBX) as ModelImporter;
        if (mi == null) { Debug.LogError("[HH] FBX importer 없음"); return; }
        mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        mi.materialSearch = ModelImporterMaterialSearch.Everywhere;

        var names = new HashSet<string>();
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(FBX))
        {
            var r = o as GameObject; if (r == null) continue;
            foreach (var rend in r.GetComponentsInChildren<Renderer>(true))
                foreach (var m in rend.sharedMaterials) if (m != null) names.Add(m.name);
        }
        int remapped = 0; var missed = new List<string>();
        foreach (var n in names)
        {
            Material best = null;
            foreach (var guid in AssetDatabase.FindAssets("t:Material " + n))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                if (p.StartsWith("Assets/HeavensHunger/Art/Materials")) continue;
                if (p.Contains("/Baked/")) continue;
                var m = AssetDatabase.LoadAssetAtPath<Material>(p);
                if (m == null || m.name != n) continue;
                bool hasTex = m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null;
                if (best == null || hasTex) best = m;
            }
            if (best != null) { mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), n), best); remapped++; }
            else missed.Add(n);
        }
        mi.SaveAndReimport();
        if (AssetDatabase.IsValidFolder("Assets/HeavensHunger/Art/Materials"))
            AssetDatabase.DeleteAsset("Assets/HeavensHunger/Art/Materials");
        AssetDatabase.ImportAsset(FBX, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        AssetDatabase.Refresh();
        Debug.Log("[HH] 머티리얼 재연결 " + remapped + "/" + names.Count
                  + (missed.Count > 0 ? "  — 못 찾음: " + string.Join(", ", missed) : ""));
    }
}
