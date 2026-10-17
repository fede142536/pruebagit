"""Broker simulado (dinero ficticio) con comisiones, slippage y gestión de riesgo."""
from dataclasses import dataclass, field


@dataclass
class RiskConfig:
    risk_per_trade: float = 0.01   # máx. % del capital que se pierde si salta el stop
    stop_loss: float = 0.02        # stop-loss: 2% bajo el precio de entrada
    take_profit: float = 0.04      # toma de ganancia: 4% sobre la entrada
    max_daily_loss: float = 0.03   # si se pierde 3% en el día, no se opera más ese día
    max_drawdown: float = 0.15     # si el capital cae 15% desde su máximo, el bot se apaga
    fee: float = 0.001             # comisión por operación (0.1%, típica de Binance spot)
    slippage: float = 0.0005       # peor precio de ejecución (0.05%)


@dataclass
class Trade:
    entry_ts: int
    exit_ts: int
    entry: float
    exit: float
    qty: float
    pnl: float
    reason: str


@dataclass
class PaperBroker:
    cash: float = 1000.0
    risk: RiskConfig = field(default_factory=RiskConfig)
    qty: float = 0.0
    entry: float = 0.0
    entry_ts: int = 0
    trades: list = field(default_factory=list)
    peak: float = 0.0
    halted: bool = False
    day: int = -1
    day_start_equity: float = 0.0

    def __post_init__(self):
        self.peak = self.day_start_equity = self.cash

    def equity(self, price):
        return self.cash + self.qty * price

    @property
    def in_position(self):
        return self.qty > 0

    def _new_day(self, ts, price):
        day = ts // 86_400_000
        if day != self.day:
            self.day, self.day_start_equity = day, self.equity(price)

    def can_open(self, ts, price):
        self._new_day(ts, price)
        eq = self.equity(price)
        if self.halted or self.in_position:
            return False
        return eq > self.day_start_equity * (1 - self.risk.max_daily_loss)

    def buy(self, ts, price):
        r = self.risk
        if not self.can_open(ts, price):
            return False
        px = price * (1 + r.slippage)
        # Tamaño tal que, si se activa el stop, la pérdida ≈ risk_per_trade del capital.
        notional = min(self.cash / (1 + r.fee), self.equity(price) * r.risk_per_trade / r.stop_loss)
        if notional <= 1:
            return False
        self.qty, self.entry, self.entry_ts = notional / px, px, ts
        self.cash -= notional * (1 + r.fee)
        return True

    def _close(self, ts, price, reason):
        r = self.risk
        px = price * (1 - r.slippage)
        proceeds = self.qty * px * (1 - r.fee)
        cost = self.qty * self.entry
        self.trades.append(Trade(self.entry_ts, ts, self.entry, px, self.qty, proceeds - cost * (1 + r.fee), reason))
        self.cash += proceeds
        self.qty = 0.0
        eq = self.cash
        self.peak = max(self.peak, eq)
        if eq < self.peak * (1 - r.max_drawdown):
            self.halted = True

    def sell(self, ts, price):
        if self.in_position:
            self._close(ts, price, "señal")

    def on_candle(self, c):
        """Revisa stop-loss / take-profit con el rango de la vela (stop primero: caso conservador)."""
        self._new_day(c.ts, c.close)
        if not self.in_position:
            return
        stop = self.entry * (1 - self.risk.stop_loss)
        target = self.entry * (1 + self.risk.take_profit)
        if c.low <= stop:
            self._close(c.ts, min(stop, c.open), "stop-loss")
        elif c.high >= target:
            self._close(c.ts, target, "take-profit")
