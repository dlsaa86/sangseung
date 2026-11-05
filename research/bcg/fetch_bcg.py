"""Fetch point-in-time inputs for BCG-matrix backtest.
- S&P 500 historical membership (fja05680/sp500)
- SEC companyfacts revenue facts (with filed dates) + SIC codes
- yfinance daily adjusted closes
Writes compact files to artifacts/bcg/.
"""
import glob, json, os, subprocess, time, gzip, io, sys
import pandas as pd, requests

OUT = "artifacts/bcg"; os.makedirs(OUT, exist_ok=True)
UA = {"User-Agent": "metronome-research research@metronome.local", "Accept-Encoding": "gzip, deflate"}
log = open(f"{OUT}/fetch_log.txt", "w")
def L(*a):
    s = " ".join(str(x) for x in a); print(s); log.write(s + "\n"); log.flush()

# 1. membership
subprocess.run(["git", "clone", "--depth", "1", "https://github.com/fja05680/sp500", "/tmp/sp500"], check=True)
cands = sorted(glob.glob("/tmp/sp500/*Historical Components*.csv"))
L("membership files", cands)
mem = pd.read_csv(cands[-1])
mem.to_csv(f"{OUT}/sp500_membership.csv.gz", index=False, compression="gzip")
mem["date"] = pd.to_datetime(mem["date"])
mem = mem[mem["date"] >= "2009-01-01"]
tickers = sorted({t.strip() for s in mem["tickers"] for t in str(s).split(",") if t.strip()})
L("tickers since 2009", len(tickers))

# 2. ticker -> CIK
ct = requests.get("https://www.sec.gov/files/company_tickers.json", headers=UA, timeout=60).json()
t2c = {v["ticker"].upper().replace(".", "-"): int(v["cik_str"]) for v in ct.values()}
REV_TAGS = ["Revenues", "RevenueFromContractWithCustomerExcludingAssessedTax",
            "RevenueFromContractWithCustomerIncludingAssessedTax", "SalesRevenueNet",
            "SalesRevenueGoodsNet", "SalesRevenueServicesNet", "RevenuesNetOfInterestExpense",
            "InterestAndDividendIncomeOperating", "TotalRevenuesAndOtherIncome"]
rows, meta = [], []
for i, t in enumerate(tickers):
    yt = t.replace(".", "-")
    cik = t2c.get(yt)
    if cik is None:
        meta.append({"ticker": yt, "cik": None, "sic": None, "name": None}); continue
    try:
        time.sleep(0.12)
        sub = requests.get(f"https://data.sec.gov/submissions/CIK{cik:010d}.json", headers=UA, timeout=60).json()
        time.sleep(0.12)
        cf = requests.get(f"https://data.sec.gov/api/xbrl/companyfacts/CIK{cik:010d}.json", headers=UA, timeout=120).json()
    except Exception as e:
        L("ERR", yt, e); meta.append({"ticker": yt, "cik": cik, "sic": None, "name": None}); continue
    meta.append({"ticker": yt, "cik": cik, "sic": sub.get("sic"), "name": sub.get("name")})
    g = cf.get("facts", {}).get("us-gaap", {})
    for tag in REV_TAGS:
        for f in g.get(tag, {}).get("units", {}).get("USD", []):
            if "start" not in f: continue
            rows.append((yt, tag, f["start"], f["end"], f["val"], f["filed"], f.get("form", "")))
    if i % 50 == 0: L(i, yt, len(rows))
pd.DataFrame(meta).to_csv(f"{OUT}/meta.csv", index=False)
pd.DataFrame(rows, columns=["ticker", "tag", "start", "end", "val", "filed", "form"]).to_csv(
    f"{OUT}/revenue_facts.csv.gz", index=False, compression="gzip")
L("revenue rows", len(rows))

# 3. prices
import yfinance as yf
ylist = sorted({m["ticker"] for m in meta} | {"SPY", "RSP"})
frames = []
for k in range(0, len(ylist), 100):
    chunk = ylist[k:k + 100]
    d = yf.download(chunk, start="2008-06-01", auto_adjust=True, progress=False, threads=True)["Close"]
    frames.append(d)
px = pd.concat(frames, axis=1)
px = px.loc[:, ~px.columns.duplicated()].dropna(how="all", axis=1)
px.round(4).to_csv(f"{OUT}/prices_adj.csv.gz", compression="gzip")
L("price cols", px.shape)
