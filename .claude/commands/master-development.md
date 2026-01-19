# Alpha Squeeze - 主開發流程指引

## 專案目標
建立一個**戰術級量化決策支援平台**，針對台股 (TWSE/TPEx) 環境，透過盤後籌碼大數據與衍生性商品市場訊號，找出具備「高壓縮、高爆發」軋空潛力的標的。

## 技術架構要求

### 異構雙核設計
- **運算核心**: Python 3.11+ (Polars, gRPC Server)
- **業務核心**: .NET 8 (ASP.NET Core, Dapper)
- **前端**: React 18 + TypeScript 5
- **資料庫**: MSSQL (SQL Server)
- **通訊協議**: gRPC (二進制傳輸)

## 開發階段

### Phase 1: 資料層建立
執行順序：
1. 建立 MSSQL 資料庫與 Schema
2. 實作 Dapper Repository 層
3. 建立資料存取單元測試

### Phase 2: Python 量化引擎
執行順序：
1. 實作 Squeeze Score 演算法
2. 建立 gRPC Server
3. 實作 FinMind API 整合
4. 實作 Playwright 權證爬蟲
5. 建立 Python 單元測試

### Phase 3: .NET API 層
執行順序：
1. 建立 ASP.NET Core Web API 專案結構
2. 實作 gRPC Client 與 Python 通訊
3. 實作 REST API Endpoints
4. 實作 LINE Notify 整合
5. 建立 API 整合測試

### Phase 4: React 前端
執行順序：
1. 建立 React + TypeScript 專案
2. 實作 Dashboard 頁面
3. 實作 IV/HV 走勢圖表
4. 實作即時軋空清單
5. 建立前端測試

### Phase 5: 整合與部署
執行順序：
1. 端對端整合測試
2. 效能優化
3. Docker 容器化
4. CI/CD 配置

## 品質要求

### 程式碼品質
- [ ] 遵循 SOLID 原則
- [ ] 適當的錯誤處理與日誌記錄
- [ ] 無硬編碼敏感資訊
- [ ] 程式碼複雜度控制 (McCabe < 10)

### 測試覆蓋率
- 單元測試覆蓋率 > 80%
- 關鍵路徑 100% 覆蓋
- 整合測試涵蓋所有 API Endpoints

### 效能指標
- gRPC 回應時間 < 100ms (單一標的)
- Batch 分析 100 支股票 < 5s
- API 回應時間 < 200ms

## Squeeze Score 演算法規格

```
S = (0.35 × F_B) + (0.25 × F_G) + (0.20 × F_M) + (0.20 × F_V)
```

| 維度 | 參數 | 權重 | 得分邏輯 |
|------|------|------|----------|
| 法人空頭 (F_B) | 借券賣出餘額變化 | 35% | 負值(回補)越大，分數越高 |
| Gamma效應 (F_G) | IV - HV 乖離 | 25% | IV < HV 時，分數越高 |
| 散戶燃料 (F_M) | 券資比 | 20% | 比率越高，分數越高 |
| 價量動能 (F_V) | 價量組合 | 20% | 帶量突破，分數越高 |

## 驗收標準

### 功能驗收
- [ ] 每日 18:30 自動擷取 FinMind 籌碼數據
- [ ] 成功爬取權證 IV 數據
- [ ] 正確計算 Squeeze Score (與手動計算一致)
- [ ] gRPC 通訊正常 (.NET ↔ Python)
- [ ] LINE Notify 20:00 推播軋空清單
- [ ] Dashboard 正確顯示 IV/HV 走勢圖

### 異常處理驗收
- [ ] FinMind API 失敗時的降級處理
- [ ] Playwright 爬蟲失敗時的重試機制
- [ ] gRPC 斷線時的 .NET 降級顯示
- [ ] 資料缺漏時的警告日誌

## 開發指令

依序執行以下指令完成開發：

```bash
# Phase 1: 資料層
/ralph-loop:ralph-loop --max-iterations 30 請根據 .claude/commands/phase1-data-layer.md 執行資料層開發

# Phase 2: Python 引擎
/ralph-loop:ralph-loop --max-iterations 50 請根據 .claude/commands/phase2-python-engine.md 執行量化引擎開發

# Phase 3: .NET API
/ralph-loop:ralph-loop --max-iterations 40 請根據 .claude/commands/phase3-dotnet-api.md 執行 API 層開發

# Phase 4: React 前端
/ralph-loop:ralph-loop --max-iterations 40 請根據 .claude/commands/phase4-react-frontend.md 執行前端開發

# Phase 5: 整合測試
/ralph-loop:ralph-loop --max-iterations 20 請根據 .claude/commands/phase5-integration.md 執行整合測試
```
