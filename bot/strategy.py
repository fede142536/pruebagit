"""Estrategia: cruce de medias móviles con filtro de RSI."""


def sma(values, n):
    return sum(values[-n:]) / n if len(values) >= n else None


def rsi(values, n=14):
    if len(values) < n + 1:
        return None
    diffs = [values[i] - values[i - 1] for i in range(len(values) - n, len(values))]
    gain = sum(d for d in diffs if d > 0) / n
    loss = -sum(d for d in diffs if d < 0) / n
    if loss == 0:
        return 100.0
    return 100 - 100 / (1 + gain / loss)


class SmaCrossStrategy:
    """Devuelve 'buy', 'sell' o None según los cierres vistos hasta ahora."""

    def __init__(self, fast=10, slow=30, rsi_max=70):
        self.fast, self.slow, self.rsi_max = fast, slow, rsi_max

    def signal(self, closes):
        if len(closes) < self.slow + 2:
            return None
        f0, s0 = sma(closes[:-1], self.fast), sma(closes[:-1], self.slow)
        f1, s1 = sma(closes, self.fast), sma(closes, self.slow)
        if f0 <= s0 and f1 > s1 and (rsi(closes) or 0) < self.rsi_max:
            return "buy"
        if f0 >= s0 and f1 < s1:
            return "sell"
        return None
