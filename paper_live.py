"""Paper trading en vivo: precios reales, dinero ficticio. Ctrl+C para detener.
Uso: python paper_live.py [--symbol BTCUSDT] [--interval 1h]"""
import argparse
import time

from bot.data import fetch_candles, fetch_price
from bot.paper import PaperBroker
from bot.strategy import SmaCrossStrategy

p = argparse.ArgumentParser()
p.add_argument("--symbol", default="BTCUSDT")
p.add_argument("--interval", default="1h")
p.add_argument("--capital", type=float, default=1000.0)
p.add_argument("--poll", type=int, default=60, help="segundos entre consultas")
a = p.parse_args()

broker, strat, last_ts = PaperBroker(cash=a.capital), SmaCrossStrategy(), None
print(f"Paper trading {a.symbol} {a.interval} con ${a.capital:,.0f} ficticios. Ctrl+C para salir.")
while True:
    try:
        candles = fetch_candles(a.symbol, a.interval, 100)
        price = fetch_price(a.symbol)
        closed = candles[:-1]  # la última vela aún no cerró
        if broker.in_position:  # stop/target contra el precio actual
            from bot.data import Candle
            broker.on_candle(Candle(candles[-1].ts, price, price, price, price))
        if closed[-1].ts != last_ts:
            last_ts = closed[-1].ts
            sig = strat.signal([c.close for c in closed])
            if sig == "buy" and broker.buy(closed[-1].ts, price):
                print(f"COMPRA ficticia a {price:,.2f}")
            elif sig == "sell" and broker.in_position:
                broker.sell(closed[-1].ts, price)
                print(f"VENTA ficticia a {price:,.2f}")
        print(f"{time.strftime('%H:%M:%S')} precio {price:,.2f} | capital ${broker.equity(price):,.2f} "
              f"| operaciones {len(broker.trades)}" + (" | APAGADO por riesgo" if broker.halted else ""))
    except KeyboardInterrupt:
        break
    except Exception as e:  # red caída, API limitada, etc.
        print("error:", e)
    time.sleep(a.poll)
