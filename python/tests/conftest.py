"""
Pytest 設定檔

提供共用的 fixtures 和設定。
"""

import sys
from pathlib import Path

import pytest

# 確保 Python 路徑正確
python_dir = Path(__file__).parent.parent
if str(python_dir) not in sys.path:
    sys.path.insert(0, str(python_dir))


@pytest.fixture
def sample_stock_data():
    """範例股票資料"""
    return {
        "ticker": "2330",
        "close_price": 600.0,
        "volume": 50000000,
        "borrow_change": -100000,
        "margin_ratio": 12.5,
        "iv": 0.25,
        "hv": 0.30,
    }


@pytest.fixture
def bullish_stock_data():
    """BULLISH 情境資料"""
    return {
        "ticker": "2330",
        "borrow_change": -500000,
        "margin_ratio": 18.0,
        "iv": 0.22,
        "hv": 0.32,
        "close_price": 610.0,
        "prev_price": 590.0,
        "volume": 80000000,
        "avg_volume": 30000000,
    }


@pytest.fixture
def bearish_stock_data():
    """BEARISH 情境資料"""
    return {
        "ticker": "2330",
        "borrow_change": 800000,
        "margin_ratio": 2.0,
        "iv": 0.40,
        "hv": 0.25,
        "close_price": 540.0,
        "prev_price": 580.0,
        "volume": 10000000,
        "avg_volume": 30000000,
    }


@pytest.fixture
def neutral_stock_data():
    """NEUTRAL 情境資料"""
    return {
        "ticker": "2330",
        "borrow_change": 0,
        "margin_ratio": 8.0,
        "iv": 0.25,
        "hv": 0.25,
        "close_price": 580.0,
        "prev_price": 580.0,
        "volume": 30000000,
        "avg_volume": 30000000,
    }
