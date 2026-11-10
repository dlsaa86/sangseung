import os, yfinance as yf
os.makedirs("artifacts/vix", exist_ok=True)
for t in ["^VIX", "TQQQ", "QQQ", "SPY", "^IRX", "^NDX"]:
    d = yf.download(t, start="1990-01-01", auto_adjust=False, progress=False, actions=False)
    if hasattr(d.columns, "levels"): d.columns = d.columns.get_level_values(0)
    d.to_csv(f"artifacts/vix/{t.replace('^','_')}.csv")
    print(t, len(d), d.index.min(), d.index.max())
