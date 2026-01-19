# Phase 5: 整合測試與部署指引

## 目標
執行端對端整合測試，確保所有元件正確協作，並建立 Docker 容器化部署。

## 前置條件
- 已完成 Phase 1-4 所有開發
- Docker Desktop 已安裝

## 開發任務

### Task 5.1: 建立端對端測試案例

```typescript
// tests/e2e/squeeze-flow.spec.ts
import { test, expect } from '@playwright/test';

test.describe('Alpha Squeeze E2E Tests', () => {

  test.beforeEach(async ({ page }) => {
    await page.goto('http://localhost:5173');
  });

  test('Dashboard loads with squeeze candidates', async ({ page }) => {
    // 等待載入完成
    await expect(page.locator('h1')).toContainText('Alpha Squeeze');

    // 確認有軋空候選清單
    const candidates = page.locator('[data-testid="squeeze-card"]');
    await expect(candidates.first()).toBeVisible();

    // 確認有排名顯示
    await expect(page.locator('text=#1')).toBeVisible();
  });

  test('Clicking candidate shows IV/HV chart', async ({ page }) => {
    // 點擊第一個候選標的
    const firstCard = page.locator('[data-testid="squeeze-card"]').first();
    await firstCard.click();

    // 確認圖表出現
    const chart = page.locator('[data-testid="ivhv-chart"]');
    await expect(chart).toBeVisible();
  });

  test('Factor breakdown displays correctly', async ({ page }) => {
    // 等待因子分解圖
    await expect(page.locator('text=法人回補')).toBeVisible();
    await expect(page.locator('text=Gamma壓縮')).toBeVisible();
    await expect(page.locator('text=空單擁擠')).toBeVisible();
    await expect(page.locator('text=價量動能')).toBeVisible();
  });

  test('API returns valid data structure', async ({ request }) => {
    const response = await request.get('http://localhost:5000/api/squeeze/top-candidates');
    expect(response.ok()).toBeTruthy();

    const data = await response.json();
    expect(data).toHaveProperty('candidates');
    expect(data).toHaveProperty('analysisDate');
    expect(Array.isArray(data.candidates)).toBeTruthy();

    if (data.candidates.length > 0) {
      const candidate = data.candidates[0];
      expect(candidate).toHaveProperty('ticker');
      expect(candidate).toHaveProperty('score');
      expect(candidate).toHaveProperty('trend');
      expect(candidate.score).toBeGreaterThanOrEqual(0);
      expect(candidate.score).toBeLessThanOrEqual(100);
    }
  });

  test('gRPC degradation works when Python engine is down', async ({ page, request }) => {
    // 假設 Python 引擎已關閉
    // API 應該返回降級響應

    const response = await request.get('http://localhost:5000/api/squeeze/2330');

    if (response.ok()) {
      const data = await response.json();
      // 可能是正常響應或降級響應
      expect(data).toHaveProperty('ticker');
      expect(data).toHaveProperty('trend');
    }
  });

});
```

### Task 5.2: 建立整合測試腳本

```python
# tests/integration/test_full_pipeline.py
"""
完整流程整合測試

測試從資料擷取到訊號計算的完整流程
"""

import pytest
import asyncio
from datetime import datetime, timedelta

# 假設已有這些模組
from python.scrapers.finmind_client import FinMindClient
from python.engine.squeeze_calculator import SqueezeCalculator
from python.workers.daily_fetch import DailyDataFetcher


class TestFullPipeline:
    """完整流程測試"""

    @pytest.fixture
    def calculator(self):
        return SqueezeCalculator()

    @pytest.fixture
    def test_ticker(self):
        return "2330"  # 台積電

    @pytest.mark.asyncio
    async def test_finmind_data_fetch(self, test_ticker):
        """測試 FinMind 資料擷取"""
        client = FinMindClient()
        end_date = datetime.now().strftime("%Y-%m-%d")
        start_date = (datetime.now() - timedelta(days=5)).strftime("%Y-%m-%d")

        # 這個測試需要實際 API 連線
        # 在 CI 環境可能需要 mock
        try:
            df = client.get_daily_metrics(test_ticker, start_date, end_date)
            assert len(df) > 0
            assert "ticker" in df.columns
            assert "close_price" in df.columns
        except Exception as e:
            pytest.skip(f"FinMind API not available: {e}")

    def test_squeeze_score_calculation_consistency(self, calculator):
        """測試評分計算一致性"""
        # 相同輸入應產生相同輸出
        params = {
            "ticker": "2330",
            "borrow_change": -500000,
            "borrow_percentile": 0.8,
            "margin_ratio": 15.0,
            "iv": 0.22,
            "hv": 0.30,
            "close_price": 600,
            "prev_close": 580,
            "volume": 50000000,
            "avg_volume_20d": 30000000,
        }

        result1 = calculator.calculate_squeeze_score(**params)
        result2 = calculator.calculate_squeeze_score(**params)

        assert result1["score"] == result2["score"]
        assert result1["trend"] == result2["trend"]

    def test_squeeze_score_weighted_sum(self, calculator):
        """驗證加權總分正確"""
        params = {
            "ticker": "TEST",
            "borrow_change": 0,
            "borrow_percentile": 0.5,
            "margin_ratio": 10.0,
            "iv": 0.25,
            "hv": 0.25,
            "close_price": 100,
            "prev_close": 100,
            "volume": 1000,
            "avg_volume_20d": 1000,
        }

        result = calculator.calculate_squeeze_score(**params)
        factors = result["factors"]

        # 手動計算加權總分
        expected_score = round(
            0.35 * factors["borrow_score"] +
            0.25 * factors["gamma_score"] +
            0.20 * factors["margin_score"] +
            0.20 * factors["momentum_score"]
        )

        assert result["score"] == expected_score

    def test_trend_classification(self, calculator):
        """測試趨勢分類邏輯"""
        # 高分 = BULLISH
        high_score_result = calculator.calculate_squeeze_score(
            ticker="TEST",
            borrow_change=-1000000,
            borrow_percentile=0.95,
            margin_ratio=25.0,
            iv=0.18,
            hv=0.32,
            close_price=110,
            prev_close=100,
            volume=100000,
            avg_volume_20d=30000,
        )
        assert high_score_result["trend"] == "BULLISH"
        assert high_score_result["score"] >= 70

        # 低分 = BEARISH
        low_score_result = calculator.calculate_squeeze_score(
            ticker="TEST",
            borrow_change=1000000,
            borrow_percentile=0.05,
            margin_ratio=1.0,
            iv=0.45,
            hv=0.20,
            close_price=90,
            prev_close=100,
            volume=10000,
            avg_volume_20d=50000,
        )
        assert low_score_result["trend"] == "BEARISH"
        assert low_score_result["score"] <= 40


class TestDatabaseIntegration:
    """資料庫整合測試"""

    @pytest.mark.asyncio
    async def test_metrics_upsert_and_retrieve(self):
        """測試資料寫入與讀取"""
        # 需要測試資料庫連線
        # 在實際環境中使用測試資料庫
        pass

    @pytest.mark.asyncio
    async def test_squeeze_signal_storage(self):
        """測試軋空訊號儲存"""
        pass


class TestGrpcCommunication:
    """gRPC 通訊測試"""

    @pytest.mark.asyncio
    async def test_grpc_server_responds(self):
        """測試 gRPC Server 回應"""
        # 需要啟動的 gRPC server
        pass

    @pytest.mark.asyncio
    async def test_grpc_batch_request(self):
        """測試批量請求"""
        pass
```

### Task 5.3: 建立 Docker 配置

```dockerfile
# docker/Dockerfile.python
FROM python:3.11-slim

WORKDIR /app

# 安裝系統依賴
RUN apt-get update && apt-get install -y \
    build-essential \
    && rm -rf /var/lib/apt/lists/*

# 安裝 Python 依賴
COPY python/requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

# 安裝 Playwright browsers
RUN playwright install chromium
RUN playwright install-deps chromium

# 複製程式碼
COPY python/ ./python/
COPY proto/ ./proto/

# 生成 gRPC 程式碼
RUN python -m grpc_tools.protoc \
    -I./proto \
    --python_out=./python/engine/protos \
    --grpc_python_out=./python/engine/protos \
    ./proto/squeeze.proto

EXPOSE 50051

CMD ["python", "-m", "python.engine.server"]
```

```dockerfile
# docker/Dockerfile.api
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/AlphaSqueeze.Api/AlphaSqueeze.Api.csproj", "AlphaSqueeze.Api/"]
COPY ["src/AlphaSqueeze.Core/AlphaSqueeze.Core.csproj", "AlphaSqueeze.Core/"]
COPY ["src/AlphaSqueeze.Data/AlphaSqueeze.Data.csproj", "AlphaSqueeze.Data/"]
COPY ["src/AlphaSqueeze.Shared/AlphaSqueeze.Shared.csproj", "AlphaSqueeze.Shared/"]

RUN dotnet restore "AlphaSqueeze.Api/AlphaSqueeze.Api.csproj"

COPY src/ .
COPY proto/ ./proto/

RUN dotnet publish "AlphaSqueeze.Api/AlphaSqueeze.Api.csproj" \
    -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 80
ENTRYPOINT ["dotnet", "AlphaSqueeze.Api.dll"]
```

```dockerfile
# docker/Dockerfile.web
FROM node:18-alpine AS build
WORKDIR /app

COPY web/package*.json ./
RUN npm ci

COPY web/ .
RUN npm run build

FROM nginx:alpine
COPY --from=build /app/dist /usr/share/nginx/html
COPY docker/nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
```

### Task 5.4: 建立 Docker Compose

```yaml
# docker-compose.yml
version: '3.8'

services:
  mssql:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      - ACCEPT_EULA=Y
      - SA_PASSWORD=${MSSQL_SA_PASSWORD:-YourStrong@Passw0rd}
      - MSSQL_PID=Express
    ports:
      - "1433:1433"
    volumes:
      - mssql_data:/var/opt/mssql
      - ./database:/docker-entrypoint-initdb.d
    healthcheck:
      test: /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "$$SA_PASSWORD" -Q "SELECT 1"
      interval: 10s
      timeout: 5s
      retries: 5

  python-engine:
    build:
      context: .
      dockerfile: docker/Dockerfile.python
    ports:
      - "50051:50051"
    environment:
      - FINMIND_TOKEN=${FINMIND_TOKEN}
      - DB_CONNECTION_STRING=Server=mssql;Database=AlphaSqueeze;User Id=sa;Password=${MSSQL_SA_PASSWORD:-YourStrong@Passw0rd};TrustServerCertificate=True;
    depends_on:
      mssql:
        condition: service_healthy
    restart: unless-stopped

  api:
    build:
      context: .
      dockerfile: docker/Dockerfile.api
    ports:
      - "5000:80"
    environment:
      - ConnectionStrings__DefaultConnection=Server=mssql;Database=AlphaSqueeze;User Id=sa;Password=${MSSQL_SA_PASSWORD:-YourStrong@Passw0rd};TrustServerCertificate=True;
      - GrpcSettings__EngineUrl=http://python-engine:50051
      - LineNotify__AccessToken=${LINE_NOTIFY_TOKEN}
    depends_on:
      - python-engine
      - mssql
    restart: unless-stopped

  web:
    build:
      context: .
      dockerfile: docker/Dockerfile.web
    ports:
      - "80:80"
    environment:
      - VITE_API_URL=http://localhost:5000/api
    depends_on:
      - api
    restart: unless-stopped

volumes:
  mssql_data:
```

### Task 5.5: 建立環境變數範本

```bash
# .env.example
# MSSQL
MSSQL_SA_PASSWORD=YourStrong@Passw0rd

# FinMind API
FINMIND_TOKEN=your_finmind_api_token

# LINE Notify
LINE_NOTIFY_TOKEN=your_line_notify_token

# API Settings
API_URL=http://localhost:5000
```

### Task 5.6: 建立 CI/CD 配置 (GitHub Actions)

```yaml
# .github/workflows/ci.yml
name: CI/CD Pipeline

on:
  push:
    branches: [main, develop]
  pull_request:
    branches: [main]

jobs:
  test-python:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Set up Python
        uses: actions/setup-python@v5
        with:
          python-version: '3.11'

      - name: Install dependencies
        run: |
          cd python
          pip install -r requirements.txt
          pip install pytest pytest-cov pytest-asyncio

      - name: Run tests
        run: |
          cd python
          pytest tests/ -v --cov=engine --cov=scrapers --cov-report=xml

      - name: Upload coverage
        uses: codecov/codecov-action@v3
        with:
          files: python/coverage.xml

  test-dotnet:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Restore dependencies
        run: dotnet restore

      - name: Build
        run: dotnet build --no-restore

      - name: Test
        run: dotnet test --no-build --verbosity normal --collect:"XPlat Code Coverage"

  test-frontend:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup Node.js
        uses: actions/setup-node@v4
        with:
          node-version: '18'

      - name: Install dependencies
        run: |
          cd web
          npm ci

      - name: Run tests
        run: |
          cd web
          npm run test

      - name: Build
        run: |
          cd web
          npm run build

  e2e-tests:
    needs: [test-python, test-dotnet, test-frontend]
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Build and start services
        run: docker-compose up -d --build

      - name: Wait for services
        run: |
          sleep 30
          curl --retry 10 --retry-delay 5 http://localhost:5000/health

      - name: Run E2E tests
        run: |
          cd web
          npx playwright install
          npx playwright test

      - name: Stop services
        run: docker-compose down

  deploy:
    needs: e2e-tests
    runs-on: ubuntu-latest
    if: github.ref == 'refs/heads/main'
    steps:
      - uses: actions/checkout@v4

      - name: Deploy to production
        run: echo "Deploy step would go here"
```

### Task 5.7: 執行完整測試流程

```bash
# 1. 啟動所有服務
docker-compose up -d

# 2. 等待服務就緒
sleep 30

# 3. 執行 Python 測試
docker-compose exec python-engine pytest tests/ -v

# 4. 執行 .NET 測試
dotnet test

# 5. 執行前端測試
cd web && npm test

# 6. 執行 E2E 測試
npx playwright test

# 7. 檢查日誌
docker-compose logs --tail=100
```

## 驗收標準

### 功能驗收
- [ ] 所有服務可透過 Docker Compose 啟動
- [ ] 端對端流程正常運作
- [ ] gRPC 通訊穩定
- [ ] LINE Notify 推播成功
- [ ] 降級模式正確觸發

### 測試驗收
- [ ] Python 測試覆蓋率 > 80%
- [ ] .NET 測試覆蓋率 > 80%
- [ ] E2E 測試全部通過
- [ ] 效能測試符合指標

### 部署驗收
- [ ] Docker images 可正常建置
- [ ] Docker Compose 啟動無錯誤
- [ ] CI/CD Pipeline 執行成功
- [ ] 環境變數正確配置

## 效能測試指標

| 指標 | 目標 | 測試方法 |
|------|------|----------|
| gRPC 單一請求 | < 100ms | `grpcurl` 測試 |
| API 回應時間 | < 200ms | `curl` 測試 |
| 批量分析 100 標的 | < 5s | 整合測試 |
| 前端首次載入 | < 3s | Lighthouse |

## 完成後輸出
1. 通過的測試報告 (所有層級)
2. Docker images 與容器運行日誌
3. CI/CD Pipeline 執行成功截圖
4. 效能測試報告
5. 完整的部署文件
