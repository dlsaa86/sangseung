// HHContent.cs — 승객 23 · 설비(부품) 151 · 인터폰 거래 24 · 연쇄.
// 원본 sangseung_proto.html 의 ROSTER / PARTS / DEALS 를 그대로 JSON 으로 떠서 읽는다.
// 손으로 옮겨 적지 않는 이유: 151개를 옮기면 오타가 반드시 난다. 정본은 Data/*.json 이다.
using System;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace HeavensHunger
{
    /// <summary>연쇄 한 줄: 사건(on) → 효과(fx) × 값(v).</summary>
    [Serializable]
    public sealed class ChainDef
    {
        public string On;    // line / eyeGone / eyeBorn / bell / blank / jack / full / deliv / pat / fuse / famN
        public string Fx;    // purge / plant / out / pow / bellg / spin / notch / tick / luck / redo
        public float V = 1f;
    }

    /// <summary>부품·승객이 주는 숫자 뭉치. 원본 mods 파이프의 키를 그대로 갖는다.</summary>
    [Serializable]
    public sealed class ModBag
    {
        public float wcMul = 1f;          // 무게세 배수
        public float reqMul = 1f;         // 문턱 배수
        public float lvH, lvV, lvD;       // 가로/세로/대각 줄값 보정
        public float[] sv = new float[7]; // 문양별 값 보정 (어금니..폐)
        public float svLo, svHi;          // 저/고배율 일괄 보정
        public float svHiX = 1f;          // 고배율 배수
        public float svPerW;              // 무게 1당 심볼값
        public float luck, luckLow, notchLuck;
        public float eyeW;                // 눈 출현 가중
        public float eyeMult;             // 눈 배수 가산 (기본 비활성 — HHDial.EyeMultFromMods)
        public float outAdd, outPerW;
        public float outMulX = 1f;
        public float spinCapD;            // 레버 수 증감
        public float jumpCap;             // 상승 한계 가산
        public float jumpCapMul = 1f;
        public float badgeAdd;            // 벨 뱃지 확률 가산
        public float gaugeNeedD;          // 종 문턱 증감
        public float brand;               // 낙인 횟수/정차
        public float purgeOnArrive, plantOnArrive, arriveTick;
        public float onDeliverTick, onBlankTick, onJackTick, onEyeGoneOut;
        public float interest, buyTick, tickThreshOut, confirmTickX;
        public float pick, seal;
        public float selfW;               // 부품 자체 무게
        // 출발 복리: 층당 배수 = 1 + v × src(런 상태)
        public string confirmMulSrc;
        public float confirmMulV;

        public void AddFrom(JObject m, ModBag famAcc)
        {
            if (m == null) return;
            foreach (var p in m.Properties())
            {
                switch (p.Name)
                {
                    case "wcMul": wcMul *= F(p.Value); break;
                    case "reqMul": reqMul *= F(p.Value); break;
                    case "svHiX": svHiX *= F(p.Value); break;
                    case "outMulX": outMulX *= F(p.Value); break;
                    case "jumpCapMul": jumpCapMul *= F(p.Value); break;

                    case "lv":
                        {
                            var o = p.Value as JObject; if (o == null) break;
                            lvH += F(o["h"]); lvV += F(o["v"]); lvD += F(o["d"]);
                            break;
                        }
                    case "sv":
                        {
                            if (p.Value is JArray a) { for (int i = 0; i < a.Count && i < sv.Length; i++) sv[i] += F(a[i]); }
                            break;
                        }
                    case "confirmMul":
                        {
                            var o = p.Value as JObject; if (o == null) break;
                            confirmMulSrc = (string)o["src"];
                            confirmMulV += F(o["v"]);
                            break;
                        }
                    case "famPer":
                        {
                            // "같은 계열 부품 N개당" — 계열 수는 호출부에서 곱한다
                            if (famAcc != null && p.Value is JObject fo) famAcc.AddFrom(fo, null);
                            break;
                        }
                    case "svLo": svLo += F(p.Value); break;
                    case "svHi": svHi += F(p.Value); break;
                    case "svPerW": svPerW += F(p.Value); break;
                    case "luck": luck += F(p.Value); break;
                    case "luckLow": luckLow += F(p.Value); break;
                    case "notchLuck": notchLuck += F(p.Value); break;
                    case "eyeW": eyeW += F(p.Value); break;
                    case "eyeMult": eyeMult += F(p.Value); break;
                    case "outAdd": outAdd += F(p.Value); break;
                    case "outPerW": outPerW += F(p.Value); break;
                    case "spinCapD": spinCapD += F(p.Value); break;
                    case "jumpCap": jumpCap += F(p.Value); break;
                    case "badgeAdd": badgeAdd += F(p.Value); break;
                    case "gaugeNeedD": gaugeNeedD += F(p.Value); break;
                    case "purgeOnArrive": purgeOnArrive += F(p.Value); break;
                    case "plantOnArrive": plantOnArrive += F(p.Value); break;
                    case "arriveTick": arriveTick += F(p.Value); break;
                    case "onDeliverTick": onDeliverTick += F(p.Value); break;
                    case "onBlankTick": onBlankTick += F(p.Value); break;
                    case "onJackTick": onJackTick += F(p.Value); break;
                    case "onEyeGoneOut": onEyeGoneOut += F(p.Value); break;
                    case "interest": interest += F(p.Value); break;
                    case "buyTick": buyTick += F(p.Value); break;
                    case "tickThreshOut": tickThreshOut += F(p.Value); break;
                    case "confirmTickX": confirmTickX += F(p.Value); break;
                    case "pick": pick += F(p.Value); break;
                    case "seal": seal += F(p.Value); break;
                    default: break;   // 아직 안 쓰는 키는 조용히 흘린다(데이터는 JSON 에 남아 있다)
                }
            }
        }

        static float F(JToken t) { return t == null ? 0f : (float)Convert.ToDouble(t); }
    }

    public sealed class PassengerDef
    {
        public string Id, Name, Kind, Fx, Why;
        public int W, Dest, Pay, Gen;
        public bool Prov;
        public JObject M;
        public ChainDef Ch;
        public string FuseFamily, FuseFx;
        public JObject FuseM;
        /// <summary>퀘스트형(q) = 목적지가 있고 인도하면 보상. 능력형(a) = 영구 탑승.</summary>
        public bool IsQuest { get { return Kind == "q"; } }
    }

    public sealed class PartDef
    {
        public string Id, Name, Family, Fx, Cond;
        public int Cost, W;
        public bool Prov;
        public JObject M;
        public ChainDef Ch;
        public string ActEf;
        public int ActN;
    }

    public sealed class DealDef
    {
        public string Id, Text, Kind;   // Kind: normal / well / grand / red / desc
    }

    public static class HHContent
    {
        public static readonly List<PassengerDef> Roster = new List<PassengerDef>();
        public static readonly List<PartDef> Parts = new List<PartDef>();
        public static readonly List<DealDef> Deals = new List<DealDef>();
        static bool _loaded;

        public static bool Loaded { get { return _loaded; } }

        public static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            try
            {
                LoadRoster(Read("hh_roster"));
                LoadParts(Read("hh_parts"));
                LoadDeals(Read("hh_deals"));
                Debug.Log("[HHContent] 승객 " + Roster.Count + " · 설비 " + Parts.Count + " · 거래 " + Deals.Count + " 적재");
            }
            catch (Exception e) { Debug.LogError("[HHContent] 적재 실패: " + e); }
        }

        static string Read(string name)
        {
            var ta = Resources.Load<TextAsset>(name);
            if (ta != null) return ta.text;
#if UNITY_EDITOR
            var p = "Assets/HeavensHunger/Data/" + name + ".json";
            var a = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(p);
            if (a != null) return a.text;
            if (System.IO.File.Exists(p)) return System.IO.File.ReadAllText(p);
#endif
            throw new Exception("데이터 없음: " + name);
        }

        static ChainDef ParseChain(JToken t)
        {
            var o = t as JObject; if (o == null) return null;
            return new ChainDef
            {
                On = (string)o["on"],
                Fx = (string)o["fx"],
                V = o["v"] != null ? (float)o["v"] : 1f
            };
        }

        static void LoadRoster(string json)
        {
            Roster.Clear();
            foreach (var t in JArray.Parse(json))
            {
                var o = (JObject)t;
                var fuse = o["fuse"] as JObject;
                Roster.Add(new PassengerDef
                {
                    Id = (string)o["id"],
                    Name = (string)o["nm"],
                    Kind = (string)o["kind"],
                    W = o["w"] != null ? (int)o["w"] : 0,
                    Dest = o["dest"] != null ? (int)o["dest"] : 0,
                    Pay = o["pay"] != null ? (int)o["pay"] : 0,
                    Gen = o["gen"] != null ? (int)o["gen"] : 1,
                    Prov = o["prov"] != null,
                    Fx = (string)o["fx"],
                    Why = (string)o["why"],
                    M = o["m"] as JObject,
                    Ch = ParseChain(o["ch"]),
                    FuseFamily = fuse != null ? (string)fuse["f"] : null,
                    FuseFx = fuse != null ? (string)fuse["fx"] : null,
                    FuseM = fuse != null ? fuse["m"] as JObject : null,
                });
            }
        }

        static void LoadParts(string json)
        {
            Parts.Clear();
            foreach (var t in JArray.Parse(json))
            {
                var o = (JObject)t;
                var act = o["act"] as JObject;
                Parts.Add(new PartDef
                {
                    Id = (string)o["id"],
                    Name = (string)o["nm"],
                    Family = (string)o["f"],
                    Cost = o["cost"] != null ? (int)o["cost"] : 1,
                    W = o["w"] != null ? (int)o["w"] : 0,
                    Prov = o["prov"] != null,
                    Fx = (string)o["fx"],
                    Cond = (string)o["c"],
                    M = o["m"] as JObject,
                    Ch = ParseChain(o["ch"]),
                    ActEf = act != null ? (string)act["ef"] : null,
                    ActN = act != null && act["n"] != null ? (int)act["n"] : 0,
                });
            }
        }

        static void LoadDeals(string json)
        {
            Deals.Clear();
            var root = JObject.Parse(json);
            AddDeals(root["DEALS"], "normal");
            AddDeals(root["WELL_DEALS"], "well");
            AddDeals(root["GRAND_DEALS"], "grand");
            AddDeals(root["RED_DEALS"], "red");
            AddDeals(root["DESC_DEALS"], "desc");
        }

        static void AddDeals(JToken arr, string kind)
        {
            if (arr == null) return;
            foreach (var t in arr)
            {
                var o = (JObject)t;
                string tx = o["tx"] != null ? (string)o["tx"] : "";
                if (tx != null && tx.StartsWith("__FN__")) tx = HHDeals.DynamicText((string)o["id"]);
                Deals.Add(new DealDef { Id = (string)o["id"], Text = tx, Kind = kind });
            }
        }

        public static PartDef Part(string id) { EnsureLoaded(); return Parts.Find(p => p.Id == id); }
        public static PassengerDef Passenger(string id) { EnsureLoaded(); return Roster.Find(p => p.Id == id); }
        public static DealDef Deal(string id) { EnsureLoaded(); return Deals.Find(d => d.Id == id); }
    }
}
