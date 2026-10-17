"""Backtest: reproduce velas históricas con la misma lógica que usaría el bot en vivo."""
from .paper import PaperBroker, RiskConfig
from .strategy import SmaCrossStrategy


def run_backtest(candles, strategy=None, capital=1000.0, risk=None):
    strategy = strategy or SmaCrossStrategy()
    broker = PaperBroker(cash=capital, risk=risk or RiskConfig())
    closes, curve = [], []
    for c in candles:
        closes.append(c.close)
        broker.on_candle(c)            # primero stop/target dentro de la vela
        sig = strategy.signal(closes)  # luego la señal al cierre
        if sig == "buy":
            broker.buy(c.ts, c.close)
        elif sig == "sell":
            broker.sell(c.ts, c.close)
        curve.append(broker.equity(c.close))
    if broker.in_position:
        broker.sell(candles[-1].ts, candles[-1].close)
    return broker, curve


def summary(broker, curve, capital):
    wins = [t for t in broker.trades if t.pnl > 0]
    peak, max_dd = curve[0], 0.0
    for v in curve:
        peak = max(peak, v)
        max_dd = max(max_dd, (peak - v) / peak)
    final = broker.cash
    n = len(broker.trades)
    return {
        "capital_inicial": capital,
        "capital_final": round(final, 2),
        "retorno_%": round((final / capital - 1) * 100, 2),
        "operaciones": n,
        "aciertos_%": round(len(wins) / n * 100, 1) if n else 0,
        "peor_caida_%": round(max_dd * 100, 2),
        "peor_operacion": round(min((t.pnl for t in broker.trades), default=0), 2),
        "bot_apagado_por_riesgo": broker.halted,
    }
