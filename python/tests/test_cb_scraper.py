"""
CB TPEX Scraper 單元測試

測試爬蟲功能：
- 資料結構正確性
- 日期解析
- 數值解析
- 標的代號推斷
"""

import pytest
from datetime import datetime

from scrapers.cb_tpex_scraper import (
    CBTpexScraper,
    CBBalanceData,
    cb_data_to_polars,
)


class TestCBBalanceData:
    """CBBalanceData 資料結構測試"""

    def test_dataclass_creation(self):
        """應能正確建立 CBBalanceData"""
        data = CBBalanceData(
            cb_ticker='23301',
            cb_name='台積電一',
            underlying_ticker='2330',
            outstanding_balance=35.0,
            conversion_price=850.0,
            maturity_date='2029-01-15',
            trade_date='2026-01-19'
        )
        assert data.cb_ticker == '23301'
        assert data.cb_name == '台積電一'
        assert data.underlying_ticker == '2330'
        assert data.outstanding_balance == 35.0
        assert data.conversion_price == 850.0


class TestCBTpexScraperHelpers:
    """CBTpexScraper 輔助方法測試"""

    @pytest.fixture
    def scraper(self) -> CBTpexScraper:
        """建立測試用 Scraper 實例"""
        return CBTpexScraper()

    # ===== 標的代號推斷測試 =====

    class TestExtractUnderlyingTicker:
        """標的代號推斷測試"""

        @pytest.fixture
        def scraper(self) -> CBTpexScraper:
            return CBTpexScraper()

        def test_extract_from_5digit_ticker(self, scraper: CBTpexScraper):
            """5 碼 CB 代號應取前 4 碼"""
            result = scraper._extract_underlying_ticker('23301', '台積電一')
            assert result == '2330'

        def test_extract_from_6digit_ticker(self, scraper: CBTpexScraper):
            """6 碼 CB 代號應取前 4 碼"""
            result = scraper._extract_underlying_ticker('233012', '台積電二')
            assert result == '2330'

        def test_extract_invalid_ticker(self, scraper: CBTpexScraper):
            """非數字代號應返回空字串"""
            result = scraper._extract_underlying_ticker('ABCD1', '測試')
            assert result == ''

        def test_extract_short_ticker(self, scraper: CBTpexScraper):
            """短於 4 碼應返回空字串"""
            result = scraper._extract_underlying_ticker('123', '測試')
            assert result == ''

    # ===== 餘額解析測試 =====

    class TestParseBalance:
        """餘額解析測試"""

        @pytest.fixture
        def scraper(self) -> CBTpexScraper:
            return CBTpexScraper()

        def test_parse_normal_balance(self, scraper: CBTpexScraper):
            """正常數字應正確解析"""
            result = scraper._parse_balance('350000')
            assert result == 35.0  # 350000 / 10000 = 35 億

        def test_parse_balance_with_comma(self, scraper: CBTpexScraper):
            """帶逗號的數字應正確解析"""
            result = scraper._parse_balance('1,500,000')
            assert result == 150.0

        def test_parse_balance_with_spaces(self, scraper: CBTpexScraper):
            """帶空格的數字應正確解析"""
            result = scraper._parse_balance('  350000  ')
            assert result == 35.0

        def test_parse_invalid_balance(self, scraper: CBTpexScraper):
            """無效數字應返回 0"""
            result = scraper._parse_balance('N/A')
            assert result == 0.0

        def test_parse_empty_balance(self, scraper: CBTpexScraper):
            """空字串應返回 0"""
            result = scraper._parse_balance('')
            assert result == 0.0

    # ===== 價格解析測試 =====

    class TestParsePrice:
        """價格解析測試"""

        @pytest.fixture
        def scraper(self) -> CBTpexScraper:
            return CBTpexScraper()

        def test_parse_normal_price(self, scraper: CBTpexScraper):
            """正常數字應正確解析"""
            result = scraper._parse_price('850.50')
            assert result == 850.50

        def test_parse_price_with_comma(self, scraper: CBTpexScraper):
            """帶逗號的數字應正確解析"""
            result = scraper._parse_price('1,200.00')
            assert result == 1200.0

        def test_parse_integer_price(self, scraper: CBTpexScraper):
            """整數價格應正確解析"""
            result = scraper._parse_price('850')
            assert result == 850.0

        def test_parse_invalid_price(self, scraper: CBTpexScraper):
            """無效數字應返回 0"""
            result = scraper._parse_price('--')
            assert result == 0.0

    # ===== 日期解析測試 =====

    class TestParseROCDate:
        """民國年日期解析測試"""

        @pytest.fixture
        def scraper(self) -> CBTpexScraper:
            return CBTpexScraper()

        def test_parse_roc_date(self, scraper: CBTpexScraper):
            """民國年日期應正確轉換"""
            result = scraper._parse_roc_date('113/12/31')
            assert result == '2024-12-31'

        def test_parse_roc_date_with_single_digit(self, scraper: CBTpexScraper):
            """單位數月日應補零"""
            result = scraper._parse_roc_date('114/1/5')
            assert result == '2025-01-05'

        def test_parse_invalid_date(self, scraper: CBTpexScraper):
            """無效日期應返回空字串"""
            result = scraper._parse_roc_date('invalid')
            assert result == ''

        def test_parse_empty_date(self, scraper: CBTpexScraper):
            """空字串應返回空字串"""
            result = scraper._parse_roc_date('')
            assert result == ''

        def test_parse_western_date(self, scraper: CBTpexScraper):
            """非民國格式應原樣返回"""
            result = scraper._parse_roc_date('2025-01-19')
            assert result == '2025-01-19'


class TestCBDataToPolars:
    """Polars 轉換測試"""

    def test_convert_to_dataframe(self):
        """應正確轉換為 DataFrame"""
        data = [
            CBBalanceData(
                cb_ticker='23301',
                cb_name='台積電一',
                underlying_ticker='2330',
                outstanding_balance=35.0,
                conversion_price=850.0,
                maturity_date='2029-01-15',
                trade_date='2026-01-19'
            ),
            CBBalanceData(
                cb_ticker='24541',
                cb_name='聯發科一',
                underlying_ticker='2454',
                outstanding_balance=25.0,
                conversion_price=1200.0,
                maturity_date='2029-03-01',
                trade_date='2026-01-19'
            ),
        ]

        df = cb_data_to_polars(data)

        assert len(df) == 2
        assert 'cb_ticker' in df.columns
        assert 'underlying_ticker' in df.columns
        assert 'outstanding_balance' in df.columns
        assert df['cb_ticker'].to_list() == ['23301', '24541']

    def test_convert_empty_list(self):
        """空列表應返回空 DataFrame"""
        df = cb_data_to_polars([])
        assert len(df) == 0


class TestScraperConfig:
    """Scraper 配置測試"""

    def test_default_timeout(self):
        """預設超時應為 60 秒"""
        scraper = CBTpexScraper()
        assert scraper.config.timeout == 60000

    def test_custom_timeout(self):
        """自訂超時應正確設定"""
        from scrapers.base_scraper import ScraperConfig
        config = ScraperConfig(timeout=30000)
        scraper = CBTpexScraper(config=config)
        assert scraper.config.timeout == 30000

    def test_base_url(self):
        """BASE_URL 應正確"""
        assert 'tpex.org.tw' in CBTpexScraper.BASE_URL

    def test_api_url(self):
        """API_URL 應正確"""
        assert 'tpex.org.tw' in CBTpexScraper.API_URL
