import unittest

from bot.data import Candle, synthetic_candles
from bot.backtest import run_backtest
from bot.paper import PaperBroker, RiskConfig


class PaperTests(unittest.TestCase):
    def test_stop_loss_limits_loss(self):
        r = RiskConfig(fee=0, slippage=0)
        b = PaperBroker(cash=1000, risk=r)
        self.assertTrue(b.buy(0, 100))
        b.on_candle(Candle(1, 100, 100, 90, 95))  # cae por debajo del stop (98)
        self.assertEqual(b.trades[0].reason, "stop-loss")
        self.assertAlmostEqual(b.trades[0].pnl, -10, places=4)  # 1% de 1000

    def test_drawdown_halts_bot(self):
        b = PaperBroker(cash=1000, risk=RiskConfig(max_drawdown=0.01, risk_per_trade=0.05, fee=0, slippage=0))
        b.buy(0, 100)
        b.on_candle(Candle(1, 100, 100, 50, 60))
        self.assertTrue(b.halted)
        self.assertFalse(b.buy(86_400_000 * 2, 100))

    def test_backtest_runs_and_never_negative_cash(self):
        broker, curve = run_backtest(synthetic_candles())
        self.assertGreater(len(curve), 0)
        self.assertGreaterEqual(broker.cash, 0)


if __name__ == "__main__":
    unittest.main()
