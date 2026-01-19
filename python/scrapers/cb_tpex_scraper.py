"""
櫃買中心 CB 餘額爬蟲

資料來源: https://www.tpex.org.tw/web/bond/publish/convertible_bond/cb_download.php

抓取項目:
- CB 代號/名稱
- 標的股票代號
- 剩餘餘額
- 轉換價格
"""

import asyncio
import logging
from dataclasses import dataclass
from datetime import datetime
from typing import Optional

import polars as pl

from .base_scraper import BaseScraper, ScrapeResult, ScraperConfig

logger = logging.getLogger(__name__)


@dataclass
class CBBalanceData:
    """CB 餘額資料結構"""
    cb_ticker: str                      # CB 代號
    cb_name: str                        # CB 名稱
    underlying_ticker: str              # 標的代號
    outstanding_balance: float          # 剩餘餘額 (億)
    conversion_price: float             # 轉換價格
    maturity_date: str                  # 到期日
    trade_date: str                     # 資料日期


class CBTpexScraper(BaseScraper[list[CBBalanceData]]):
    """
    櫃買中心可轉債資料爬蟲

    爬取流程:
    1. 前往櫃買中心 CB 資訊頁面
    2. 選擇「可轉換公司債」類別
    3. 解析表格數據
    4. 轉換為結構化資料
    """

    BASE_URL = "https://www.tpex.org.tw/web/bond/publish/convertible_bond/cb_download.php"
    # 備用 API endpoint (直接取得 CSV/JSON)
    API_URL = "https://www.tpex.org.tw/web/bond/tradeinfo/cb/cbDaily.php"

    def __init__(self, config: Optional[ScraperConfig] = None):
        super().__init__(config or ScraperConfig(timeout=60000))

    async def scrape(self, page, **kwargs) -> list[CBBalanceData]:
        """
        執行爬取邏輯

        Args:
            page: Playwright 頁面實例
            **kwargs: 可選參數
                - trade_date: 指定日期 (YYYY-MM-DD)

        Returns:
            CB 餘額資料列表
        """
        trade_date = kwargs.get('trade_date', datetime.now().strftime('%Y-%m-%d'))

        logger.info(f"Scraping CB balance data for {trade_date}")

        try:
            # 嘗試使用 API 方式取得資料（更穩定）
            return await self._scrape_via_api(page, trade_date)
        except Exception as e:
            logger.warning(f"API scrape failed: {e}, falling back to web scraping")
            return await self._scrape_via_web(page, trade_date)

    async def _scrape_via_api(self, page, trade_date: str) -> list[CBBalanceData]:
        """透過 API 取得 CB 資料"""
        # 轉換日期格式為民國年 (例如: 115/01/19)
        dt = datetime.strptime(trade_date, '%Y-%m-%d')
        roc_year = dt.year - 1911
        roc_date = f"{roc_year}/{dt.month:02d}/{dt.day:02d}"

        api_url = f"{self.API_URL}?l=zh-tw&d={roc_date}&o=json"
        logger.info(f"Fetching CB data from API: {api_url}")

        response = await page.goto(api_url, wait_until='load')
        content = await response.text()

        # 解析 JSON 回應
        import json
        try:
            data = json.loads(content)
        except json.JSONDecodeError:
            raise ValueError("Invalid JSON response from API")

        if 'aaData' not in data:
            raise ValueError("No data in API response")

        results: list[CBBalanceData] = []
        for row in data['aaData']:
            try:
                # API 回傳格式: [代號, 名稱, ..., 剩餘張數, 轉換價, ...]
                cb_ticker = str(row[0]).strip()
                cb_name = str(row[1]).strip()
                underlying_ticker = self._extract_underlying_ticker(cb_ticker, cb_name)

                # 剩餘餘額 (轉換為億)
                balance_str = str(row[4]).replace(',', '').strip()
                outstanding_balance = float(balance_str) / 10000 if balance_str else 0

                # 轉換價格
                conv_price_str = str(row[5]).replace(',', '').strip()
                conversion_price = float(conv_price_str) if conv_price_str else 0

                # 到期日
                maturity_date = self._parse_roc_date(str(row[3]))

                results.append(CBBalanceData(
                    cb_ticker=cb_ticker,
                    cb_name=cb_name,
                    underlying_ticker=underlying_ticker,
                    outstanding_balance=outstanding_balance,
                    conversion_price=conversion_price,
                    maturity_date=maturity_date,
                    trade_date=trade_date
                ))
            except (IndexError, ValueError) as e:
                logger.warning(f"Failed to parse row: {e}")
                continue

        logger.info(f"Successfully scraped {len(results)} CB records via API")
        return results

    async def _scrape_via_web(self, page, trade_date: str) -> list[CBBalanceData]:
        """透過網頁爬取 CB 資料"""
        # 導航至目標頁面
        await page.goto(self.BASE_URL, wait_until='networkidle')

        # 等待表格載入
        await page.wait_for_selector('table', timeout=self.config.timeout)

        # 取得所有表格列
        rows = await page.query_selector_all('table tbody tr')

        results: list[CBBalanceData] = []

        for row in rows:
            try:
                cells = await row.query_selector_all('td')
                if len(cells) < 6:
                    continue

                cb_ticker = await cells[0].inner_text()
                cb_name = await cells[1].inner_text()

                # 解析標的代號 (從 CB 代號或名稱推斷)
                underlying_ticker = self._extract_underlying_ticker(cb_ticker.strip(), cb_name.strip())

                # 解析餘額 (單位: 億)
                balance_text = await cells[4].inner_text()
                outstanding_balance = self._parse_balance(balance_text)

                # 解析轉換價
                conversion_text = await cells[5].inner_text()
                conversion_price = self._parse_price(conversion_text)

                # 解析到期日
                maturity_text = await cells[3].inner_text() if len(cells) > 3 else ""
                maturity_date = self._parse_roc_date(maturity_text)

                results.append(CBBalanceData(
                    cb_ticker=cb_ticker.strip(),
                    cb_name=cb_name.strip(),
                    underlying_ticker=underlying_ticker,
                    outstanding_balance=outstanding_balance,
                    conversion_price=conversion_price,
                    maturity_date=maturity_date,
                    trade_date=trade_date
                ))

            except Exception as e:
                logger.warning(f"Failed to parse row: {e}")
                continue

        logger.info(f"Successfully scraped {len(results)} CB records via web")
        return results

    def _extract_underlying_ticker(self, cb_ticker: str, cb_name: str) -> str:
        """從 CB 代號或名稱推斷標的股票代號"""
        # CB 代號通常是 標的代號 + 序號，如 23301, 23302
        # 標的代號為前 4 碼
        if len(cb_ticker) >= 4 and cb_ticker[:4].isdigit():
            return cb_ticker[:4]
        return ""

    def _parse_balance(self, text: str) -> float:
        """解析餘額文字為數值 (億)"""
        try:
            cleaned = text.replace(',', '').replace(' ', '').strip()
            return float(cleaned) / 10000  # 轉換為億
        except ValueError:
            return 0.0

    def _parse_price(self, text: str) -> float:
        """解析價格文字"""
        try:
            cleaned = text.replace(',', '').replace(' ', '').strip()
            return float(cleaned)
        except ValueError:
            return 0.0

    def _parse_roc_date(self, text: str) -> str:
        """解析民國年日期文字"""
        import re
        try:
            # 處理民國年格式 (113/12/31 -> 2024-12-31)
            text = text.strip()
            if not text:
                return ""
            if '/' in text:
                parts = text.split('/')
                if len(parts) == 3:
                    year = int(parts[0]) + 1911
                    return f"{year}-{parts[1].zfill(2)}-{parts[2].zfill(2)}"
            # 檢查是否為西元日期格式 (YYYY-MM-DD)
            if re.match(r'^\d{4}-\d{2}-\d{2}$', text):
                return text
            # 非有效日期格式
            return ""
        except (ValueError, IndexError):
            return ""


async def scrape_cb_balances(trade_date: Optional[str] = None) -> ScrapeResult[list[CBBalanceData]]:
    """
    便捷函數：執行 CB 餘額爬取

    Args:
        trade_date: 指定日期 (預設: 今日)

    Returns:
        ScrapeResult 包含 CB 餘額資料
    """
    async with CBTpexScraper() as scraper:
        return await scraper.execute(trade_date=trade_date)


def cb_data_to_polars(data: list[CBBalanceData]) -> pl.DataFrame:
    """將 CB 資料轉換為 Polars DataFrame"""
    return pl.DataFrame([
        {
            'cb_ticker': d.cb_ticker,
            'cb_name': d.cb_name,
            'underlying_ticker': d.underlying_ticker,
            'outstanding_balance': d.outstanding_balance,
            'conversion_price': d.conversion_price,
            'maturity_date': d.maturity_date,
            'trade_date': d.trade_date
        }
        for d in data
    ])


# 同步介面供 CLI 使用
def run_cb_scraper(trade_date: Optional[str] = None) -> list[CBBalanceData]:
    """
    同步執行 CB 爬蟲

    Args:
        trade_date: 指定日期

    Returns:
        CB 餘額資料列表
    """
    result = asyncio.run(scrape_cb_balances(trade_date))
    if result.success:
        return result.data or []
    else:
        raise RuntimeError(f"CB scraping failed: {result.error}")


if __name__ == "__main__":
    import sys

    logging.basicConfig(level=logging.INFO)

    date_arg = sys.argv[1] if len(sys.argv) > 1 else None

    try:
        data = run_cb_scraper(date_arg)
        print(f"\nFound {len(data)} CB records:")
        for cb in data[:10]:
            print(f"  {cb.cb_ticker}: {cb.cb_name} | "
                  f"標的: {cb.underlying_ticker} | "
                  f"轉換價: {cb.conversion_price} | "
                  f"餘額: {cb.outstanding_balance:.2f}億")
    except Exception as e:
        print(f"Error: {e}")
        sys.exit(1)
