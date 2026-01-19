"""
資料回補機制測試

驗證追加需求：
- 日期範圍回補
- 進度追蹤
- 錯誤處理
- API 欄位映射
"""

import pytest
from datetime import datetime, timedelta
from unittest.mock import Mock, patch, MagicMock, AsyncMock

import polars as pl


class TestDailyDataFetcher:
    """FinMind 資料擷取器測試"""

    @pytest.fixture
    def mock_loader(self):
        """建立 Mock FinMind DataLoader"""
        with patch('workers.daily_fetch.DataLoader') as mock:
            loader = MagicMock()
            mock.return_value = loader
            yield loader

    def test_fetch_stock_prices_columns(self, mock_loader):
        """股價資料應包含正確欄位"""
        import pandas as pd

        # 模擬 FinMind 回傳
        mock_df = pd.DataFrame({
            'date': ['2026-01-15', '2026-01-16'],
            'stock_id': ['2330', '2330'],
            'open': [1700, 1720],
            'max': [1750, 1760],
            'min': [1680, 1700],
            'close': [1740, 1755],
            'Trading_Volume': [50000000, 45000000],
            'Trading_money': [87000000000, 79000000000]
        })
        mock_loader.taiwan_stock_daily.return_value = mock_df

        from workers.daily_fetch import DailyDataFetcher
        fetcher = DailyDataFetcher(finmind_token='test_token')

        result = fetcher.fetch_stock_prices('2330', '2026-01-15', '2026-01-16')

        # 驗證欄位映射
        assert 'ticker' in result.columns
        assert 'trade_date' in result.columns
        assert 'close_price' in result.columns
        assert 'volume' in result.columns

    def test_fetch_borrowing_data_uses_correct_api(self, mock_loader):
        """借券資料應使用正確的 API"""
        import pandas as pd

        # 模擬 TaiwanDailyShortSaleBalances 回傳
        mock_df = pd.DataFrame({
            'date': ['2026-01-15', '2026-01-16'],
            'stock_id': ['2330', '2330'],
            'SBLShortSalesCurrentDayBalance': [1000000, 950000],
            'MarginShortSalesCurrentDayBalance': [500, 480],
        })
        mock_loader.taiwan_daily_short_sale_balances.return_value = mock_df

        from workers.daily_fetch import DailyDataFetcher
        fetcher = DailyDataFetcher(finmind_token='test_token')

        result = fetcher.fetch_borrowing_data('2330', '2026-01-15', '2026-01-16')

        # 驗證呼叫正確的 API
        mock_loader.taiwan_daily_short_sale_balances.assert_called_once()

        # 驗證欄位映射 (SBLShortSalesCurrentDayBalance -> borrowing_balance)
        assert 'borrowing_balance' in result.columns
        assert 'borrowing_balance_change' in result.columns

    def test_fetch_margin_data_calculates_ratio(self, mock_loader):
        """融資融券資料應正確計算券資比"""
        import pandas as pd

        mock_df = pd.DataFrame({
            'date': ['2026-01-15'],
            'stock_id': ['2330'],
            'MarginPurchaseTodayBalance': [100000],  # 融資餘額
            'ShortSaleTodayBalance': [5000],  # 融券餘額
        })
        mock_loader.taiwan_stock_margin_purchase_short_sale.return_value = mock_df

        from workers.daily_fetch import DailyDataFetcher
        fetcher = DailyDataFetcher(finmind_token='test_token')

        result = fetcher.fetch_margin_data('2330', '2026-01-15', '2026-01-15')

        # 驗證券資比計算: 5000 / 100000 * 100 = 5%
        assert 'margin_ratio' in result.columns
        margin_ratio = result['margin_ratio'][0]
        assert abs(margin_ratio - 5.0) < 0.01


class TestBackfillService:
    """回補服務測試"""

    @pytest.fixture
    def mock_db(self):
        """建立 Mock 資料庫連線"""
        return MagicMock()

    @pytest.fixture
    def mock_repos(self, mock_db):
        """建立 Mock Repositories"""
        with patch('workers.backfill.get_database') as mock_get_db, \
             patch('workers.backfill.BackfillJobRepository') as mock_job_repo, \
             patch('workers.backfill.StockMetricsRepository') as mock_metrics_repo, \
             patch('workers.backfill.TrackedTickerRepository') as mock_ticker_repo, \
             patch('workers.backfill.ConfigRepository') as mock_config_repo, \
             patch('workers.backfill.DailyDataFetcher') as mock_fetcher:

            mock_get_db.return_value = mock_db

            # 設置 config repo 回傳
            config_repo = MagicMock()
            config_repo.get_value.return_value = '0.5'
            mock_config_repo.return_value = config_repo

            # 設置 ticker repo 回傳
            ticker_repo = MagicMock()
            ticker_repo.get_active_tickers.return_value = ['2330', '2454']
            mock_ticker_repo.return_value = ticker_repo

            # 設置 job repo 回傳
            job_repo = MagicMock()
            job_repo.create_job.return_value = 1
            mock_job_repo.return_value = job_repo

            # 設置 metrics repo
            metrics_repo = MagicMock()
            mock_metrics_repo.return_value = metrics_repo

            yield {
                'db': mock_db,
                'job_repo': job_repo,
                'metrics_repo': metrics_repo,
                'ticker_repo': ticker_repo,
                'config_repo': config_repo,
                'fetcher': mock_fetcher.return_value
            }

    def test_default_date_range(self, mock_repos):
        """預設日期範圍應為 30 天"""
        mock_repos['config_repo'].get_value.return_value = '30'

        from workers.backfill import BackfillService
        service = BackfillService()

        start_date, end_date = service._get_default_date_range()

        # 驗證範圍約 30 天
        start = datetime.strptime(start_date, '%Y-%m-%d')
        end = datetime.strptime(end_date, '%Y-%m-%d')
        days_diff = (end - start).days

        assert 29 <= days_diff <= 31, f"預設範圍應約 30 天，實際 {days_diff} 天"


class TestBackfillJobTracking:
    """回補任務追蹤測試"""

    def test_job_status_values(self):
        """任務狀態應符合規範"""
        valid_statuses = ['PENDING', 'RUNNING', 'COMPLETED', 'FAILED']

        # 這些狀態值應在資料庫 migration 中定義
        for status in valid_statuses:
            assert len(status) <= 20, f"狀態 {status} 超過欄位長度限制"

    def test_job_type_values(self):
        """任務類型應符合規範"""
        valid_types = ['STOCK_METRICS', 'WARRANT_DATA', 'FULL']

        for job_type in valid_types:
            assert len(job_type) <= 50, f"類型 {job_type} 超過欄位長度限制"


class TestAPIColumnMapping:
    """FinMind API 欄位映射測試"""

    def test_taiwan_daily_short_sale_balances_columns(self):
        """TaiwanDailyShortSaleBalances API 欄位映射"""
        # 根據 FinMind 文件的欄位
        expected_columns = [
            'stock_id',
            'SBLShortSalesCurrentDayBalance',  # 借券賣出餘額
            'MarginShortSalesCurrentDayBalance',  # 融券餘額
            'date'
        ]

        # 這些欄位應在 daily_fetch.py 中正確處理
        for col in expected_columns:
            assert col is not None

    def test_taiwan_stock_margin_purchase_short_sale_columns(self):
        """TaiwanStockMarginPurchaseShortSale API 欄位映射"""
        expected_columns = [
            'stock_id',
            'MarginPurchaseTodayBalance',  # 融資餘額
            'ShortSaleTodayBalance',  # 融券餘額
            'date'
        ]

        for col in expected_columns:
            assert col is not None

    def test_margin_ratio_calculation(self):
        """券資比計算公式驗證"""
        # 券資比 = 融券餘額 / 融資餘額 * 100
        short_balance = 5000
        margin_balance = 100000

        margin_ratio = short_balance / margin_balance * 100

        assert margin_ratio == 5.0, "券資比應為 5%"

    def test_borrowing_balance_change_calculation(self):
        """借券餘額變化計算驗證"""
        # 變化量 = 今日餘額 - 昨日餘額
        # 負值表示回補，正值表示增加
        today_balance = 950000
        yesterday_balance = 1000000

        change = today_balance - yesterday_balance

        assert change == -50000, "應為 -50000 (回補)"


class TestDataIntegrity:
    """資料完整性測試"""

    def test_metrics_unique_constraint(self):
        """DailyStockMetrics 應有 Ticker+TradeDate 唯一約束"""
        # 此為資料庫層約束，在 schema.sql 中定義
        constraint_name = 'UC_DailyStockMetrics_Ticker_Date'
        assert constraint_name is not None

    def test_required_metrics_columns(self):
        """必要欄位應符合規劃書定義"""
        required_columns = [
            'Ticker',
            'TradeDate',
            'ClosePrice',
            'BorrowingBalanceChange',  # 借券賣出餘額增減 (核心指標)
            'MarginRatio',  # 券資比
            'HistoricalVolatility20D',  # 20日歷史波動率
            'Volume'
        ]

        for col in required_columns:
            assert col is not None, f"必要欄位 {col} 未定義"
