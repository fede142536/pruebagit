"""Uso: python backtest.py [--symbol BTCUSDT] [--interval 1h] [--synthetic]"""
import argparse

from bot.backtest import run_backtest, summary
from bot.data import fetch_candles, synthetic_candles

p = argparse.ArgumentParser()
p.add_argument("--symbol", default="BTCUSDT")
p.add_argument("--interval", default="1h")
p.add_argument("--capital", type=float, default=1000.0)
p.add_argument("--synthetic", action="store_true", help="datos falsos (sin internet)")
a = p.parse_args()

candles = synthetic_candles() if a.synthetic else fetch_candles(a.symbol, a.interval)
broker, curve = run_backtest(candles, capital=a.capital)
buy_hold = candles[-1].close / candles[0].close - 1
print(f"Velas analizadas: {len(candles)}  |  Comprar y mantener: {buy_hold * 100:.2f}%")
for k, v in summary(broker, curve, a.capital).items():
    print(f"{k:>24}: {v}")
