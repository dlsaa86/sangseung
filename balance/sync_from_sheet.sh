#!/bin/sh
# 상승 밸런스 — Google Sheets → balance/*.csv 동기화
# 주의: 시트가 비공개면 curl이 로그인 페이지를 받는다. 그 경우 Aside에게 맡기거나 수동 다운로드.
ID="18H0Uy1VSZiw6PAwGO7o-52wsTyHiAg_XYhV9vQ-6ZZw"
DIR="$(cd "$(dirname "$0")" && pwd)"
sync_tab() { # $1=탭명 $2=파일명
  OUT="$DIR/$2"
  curl -sL "https://docs.google.com/spreadsheets/d/$ID/gviz/tq?tqx=out:csv&sheet=$1" -o "$OUT.tmp" || { echo "✗ $1 다운로드 실패"; return 1; }
  if head -c 200 "$OUT.tmp" | grep -qi "<html\|accounts.google"; then
    echo "✗ $1 — 시트가 비공개입니다. Aside에게 '밸런스 시트 동기화'를 요청하거나 수동 다운로드하세요."
    rm -f "$OUT.tmp"; return 1
  fi
  mv "$OUT.tmp" "$OUT"; echo "✓ $2"
}
sync_tab "1_Symbols"      "01_symbols.csv"
sync_tab "2_Stages"       "02_stages.csv"
sync_tab "3_SnowPatterns" "03_snow_patterns.csv"
sync_tab "4_Config"       "04_config.csv"
sync_tab "6_Passengers"   "05_passengers.csv"
sync_tab "7_Equipment"    "06_equipment.csv"
sync_tab "8_Deals"        "07_deals.csv"
sync_tab "9_Chains"       "08_chains.csv"
sync_tab "10_Progression" "10_progression.csv"
echo "완료 — git diff로 변경 확인 후 커밋하세요."
