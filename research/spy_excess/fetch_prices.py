"""Fetch daily price history for the SPY-excess evidence study (run in CI, not in production)."""
import json
from pathlib import Path

import pandas as pd
import yfinance as yf

TICKERS = [
    'SPY', 'VOO', 'QQQ', 'QQQM', 'VXUS', 'VEU', 'EFA', 'EWY', '148020.KS', '069500.KS', '^KS200',
    'GLD', 'GLDM', 'IBIT', 'BTC-USD', 'IBB', 'AVGO', 'GOOGL', 'LLY', 'MSFT', 'NVDA', 'XLG', 'MGK',
    'KRW=X', 'AAPL', 'AMZN', 'META', 'TSLA', 'BRK-B', '360750.KS', '379800.KS', 'RSP', 'MTUM', 'QUAL',
    'SCHD', 'BIL',
]
OUT = Path('artifacts/prices')
OUT.mkdir(parents=True, exist_ok=True)
summary = {}
for t in TICKERS:
    df = yf.Ticker(t).history(period='max', auto_adjust=False, actions=True)
    if df.empty:
        summary[t] = None
        continue
    df.index = df.index.tz_localize(None).normalize()
    df = df[['Open', 'Close', 'Adj Close', 'Dividends']]
    df.to_csv(OUT/f"{t.replace('^', '_')}.csv")
    summary[t] = [str(df.index[0].date()), str(df.index[-1].date()), len(df)]
(OUT/'summary.json').write_text(json.dumps(summary, indent=1))
print(json.dumps(summary, indent=1))
