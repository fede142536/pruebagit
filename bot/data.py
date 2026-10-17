"""Obtención de velas (OHLC): API pública de Binance o datos sintéticos."""
import json
import math
import random
import urllib.request
from dataclasses import dataclass

BINANCE = "https://api.binance.com/api/v3"


@dataclass
class Candle:
    ts: int  # milisegundos
    open: float
    high: float
    low: float
    close: float


def fetch_candles(symbol="BTCUSDT", interval="1h", limit=1000):
    """Velas históricas reales (endpoint público, sin API key)."""
    url = f"{BINANCE}/klines?symbol={symbol}&interval={interval}&limit={limit}"
    with urllib.request.urlopen(url, timeout=15) as r:
        rows = json.load(r)
    return [Candle(int(x[0]), float(x[1]), float(x[2]), float(x[3]), float(x[4])) for x in rows]


def fetch_price(symbol="BTCUSDT"):
    with urllib.request.urlopen(f"{BINANCE}/ticker/price?symbol={symbol}", timeout=15) as r:
        return float(json.load(r)["price"])


def synthetic_candles(n=2000, start=50000.0, seed=1):
    """Datos falsos con tendencias y ruido, solo para probar el código."""
    rnd = random.Random(seed)
    price, out, drift = start, [], 0.0
    for i in range(n):
        if i % 200 == 0:
            drift = rnd.uniform(-0.0006, 0.0008)
        o = price
        price *= math.exp(drift + rnd.gauss(0, 0.006))
        hi = max(o, price) * (1 + abs(rnd.gauss(0, 0.002)))
        lo = min(o, price) * (1 - abs(rnd.gauss(0, 0.002)))
        out.append(Candle(i * 3_600_000, o, hi, lo, price))
    return out
