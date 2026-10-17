# Prueba App – Bot de paper trading de cripto

Prototipo educativo: analiza precios de cripto y simula compras/ventas con **dinero ficticio**.
No usa cuentas, claves de API ni dinero real. Solo Python 3.10+ (sin dependencias).

## Uso

```bash
python3 backtest.py                    # prueba con datos históricos reales de Binance (BTCUSDT, 1h)
python3 backtest.py --synthetic        # datos falsos, sin internet (solo para probar el código)
python3 paper_live.py                  # paper trading en vivo con precios reales
python3 -m unittest discover -s tests  # tests
```

## Cómo funciona

- **Estrategia** (`bot/strategy.py`): compra cuando la media móvil rápida (10) cruza hacia arriba la lenta (30) y el RSI no está sobrecomprado; vende en el cruce inverso.
- **Riesgo** (`bot/paper.py`, `RiskConfig`):
  - stop-loss del 2 % y take-profit del 4 % por operación;
  - tamaño de posición para perder ~1 % del capital si salta el stop;
  - pausa diaria si se pierde el 3 % en el día;
  - apagado total si el capital cae 15 % desde su máximo;
  - comisión (0,1 %) y slippage (0,05 %) simulados.

## Advertencias

- Una estrategia simple como esta **no tiene ganancia garantizada**; probablemente no supere a comprar y mantener. Es una base para aprender y medir riesgo.
- El backtest no es una promesa de resultados futuros. Antes de pensar en dinero real: varias semanas de `paper_live.py`.
- Si algún día se conecta a un exchange real, usar claves **sin permiso de retiro**.
