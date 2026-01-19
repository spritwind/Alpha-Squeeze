# Phase 4: React 前端開發指引

## 目標
建立 React + TypeScript 前端 Dashboard，視覺化軋空訊號與 IV/HV 走勢圖。

## 前置條件
- Node.js 18+ 已安裝
- 已完成 Phase 3 .NET API

## 開發任務

### Task 4.1: 建立 React 專案

```bash
cd web
npm create vite@latest . -- --template react-ts

# 安裝依賴
npm install
npm install axios react-query @tanstack/react-query
npm install recharts  # 圖表庫
npm install @radix-ui/react-select @radix-ui/react-tabs  # UI 元件
npm install tailwindcss postcss autoprefixer
npm install date-fns  # 日期處理
npm install clsx tailwind-merge  # 樣式工具

# 開發依賴
npm install -D @types/node
npx tailwindcss init -p
```

### Task 4.2: 配置 Tailwind CSS

```javascript
// tailwind.config.js
/** @type {import('tailwindcss').Config} */
export default {
  content: [
    "./index.html",
    "./src/**/*.{js,ts,jsx,tsx}",
  ],
  theme: {
    extend: {
      colors: {
        bullish: '#ef4444',  // 紅色 (台股慣例)
        bearish: '#22c55e',  // 綠色
        neutral: '#6b7280',
      },
    },
  },
  plugins: [],
}
```

### Task 4.3: 建立專案結構

```
web/src/
├── components/
│   ├── ui/                  # 基礎 UI 元件
│   │   ├── Button.tsx
│   │   ├── Card.tsx
│   │   └── Badge.tsx
│   ├── charts/              # 圖表元件
│   │   ├── IVHVChart.tsx
│   │   └── ScoreGauge.tsx
│   ├── squeeze/             # 軋空相關元件
│   │   ├── SqueezeCard.tsx
│   │   ├── SqueezeList.tsx
│   │   └── FactorBreakdown.tsx
│   └── layout/
│       ├── Header.tsx
│       └── Sidebar.tsx
├── hooks/
│   ├── useSqueezeSignals.ts
│   └── useStockMetrics.ts
├── services/
│   └── api.ts
├── types/
│   └── index.ts
├── pages/
│   ├── Dashboard.tsx
│   └── StockDetail.tsx
├── App.tsx
└── main.tsx
```

### Task 4.4: 建立 API 服務層

```typescript
// src/services/api.ts
import axios from 'axios';
import type { SqueezeSignal, TopCandidates, StockMetric } from '../types';

const api = axios.create({
  baseURL: import.meta.env.VITE_API_URL || 'http://localhost:5000/api',
  timeout: 10000,
});

export const squeezeApi = {
  getTopCandidates: async (limit = 10, minScore = 60): Promise<TopCandidates> => {
    const { data } = await api.get('/squeeze/top-candidates', {
      params: { limit, minScore },
    });
    return data;
  },

  getSignal: async (ticker: string): Promise<SqueezeSignal> => {
    const { data } = await api.get(`/squeeze/${ticker}`);
    return data;
  },
};

export const metricsApi = {
  getByDate: async (date?: string): Promise<StockMetric[]> => {
    const { data } = await api.get('/metrics', { params: { date } });
    return data;
  },

  getHistory: async (ticker: string, days = 30): Promise<StockMetric[]> => {
    const { data } = await api.get(`/metrics/${ticker}/history`, {
      params: { days },
    });
    return data;
  },
};
```

### Task 4.5: 建立 TypeScript 型別

```typescript
// src/types/index.ts
export interface FactorScores {
  borrowScore: number;
  gammaScore: number;
  marginScore: number;
  momentumScore: number;
}

export interface SqueezeSignal {
  ticker: string;
  score: number;
  trend: 'BULLISH' | 'NEUTRAL' | 'BEARISH' | 'DEGRADED';
  comment: string;
  factors: FactorScores | null;
}

export interface TopCandidates {
  candidates: SqueezeSignal[];
  analysisDate: string;
  generatedAt: string;
}

export interface StockMetric {
  ticker: string;
  tradeDate: string;
  closePrice: number | null;
  borrowingBalanceChange: number | null;
  marginRatio: number | null;
  historicalVolatility20D: number | null;
  volume: number | null;
}
```

### Task 4.6: 建立 React Query Hooks

```typescript
// src/hooks/useSqueezeSignals.ts
import { useQuery } from '@tanstack/react-query';
import { squeezeApi } from '../services/api';

export function useTopCandidates(limit = 10, minScore = 60) {
  return useQuery({
    queryKey: ['topCandidates', limit, minScore],
    queryFn: () => squeezeApi.getTopCandidates(limit, minScore),
    refetchInterval: 60000, // 每分鐘更新
    staleTime: 30000,
  });
}

export function useSqueezeSignal(ticker: string) {
  return useQuery({
    queryKey: ['squeezeSignal', ticker],
    queryFn: () => squeezeApi.getSignal(ticker),
    enabled: !!ticker,
  });
}
```

```typescript
// src/hooks/useStockMetrics.ts
import { useQuery } from '@tanstack/react-query';
import { metricsApi } from '../services/api';

export function useStockHistory(ticker: string, days = 30) {
  return useQuery({
    queryKey: ['stockHistory', ticker, days],
    queryFn: () => metricsApi.getHistory(ticker, days),
    enabled: !!ticker,
  });
}
```

### Task 4.7: 建立 IV/HV 走勢圖表

```tsx
// src/components/charts/IVHVChart.tsx
import { LineChart, Line, XAxis, YAxis, Tooltip, Legend, ResponsiveContainer } from 'recharts';
import { format, parseISO } from 'date-fns';
import type { StockMetric } from '../../types';

interface IVHVChartProps {
  metrics: StockMetric[];
  ivData?: { date: string; iv: number }[];
}

export function IVHVChart({ metrics, ivData = [] }: IVHVChartProps) {
  // 合併 HV 與 IV 資料
  const chartData = metrics.map((m) => {
    const iv = ivData.find((i) => i.date === m.tradeDate)?.iv;
    return {
      date: m.tradeDate,
      hv: m.historicalVolatility20D ? m.historicalVolatility20D * 100 : null,
      iv: iv ? iv * 100 : null,
    };
  });

  return (
    <div className="w-full h-80">
      <ResponsiveContainer>
        <LineChart data={chartData} margin={{ top: 5, right: 30, left: 20, bottom: 5 }}>
          <XAxis
            dataKey="date"
            tickFormatter={(date) => format(parseISO(date), 'MM/dd')}
          />
          <YAxis
            tickFormatter={(value) => `${value.toFixed(0)}%`}
            domain={['auto', 'auto']}
          />
          <Tooltip
            formatter={(value: number) => [`${value.toFixed(2)}%`]}
            labelFormatter={(date) => format(parseISO(date as string), 'yyyy/MM/dd')}
          />
          <Legend />
          <Line
            type="monotone"
            dataKey="hv"
            name="HV (20日歷史波動率)"
            stroke="#3b82f6"
            strokeWidth={2}
            dot={false}
          />
          <Line
            type="monotone"
            dataKey="iv"
            name="IV (隱含波動率)"
            stroke="#f97316"
            strokeWidth={2}
            dot={false}
          />
        </LineChart>
      </ResponsiveContainer>
    </div>
  );
}
```

### Task 4.8: 建立軋空評分卡片

```tsx
// src/components/squeeze/SqueezeCard.tsx
import { clsx } from 'clsx';
import type { SqueezeSignal } from '../../types';
import { FactorBreakdown } from './FactorBreakdown';

interface SqueezeCardProps {
  signal: SqueezeSignal;
  rank: number;
  onClick?: () => void;
}

export function SqueezeCard({ signal, rank, onClick }: SqueezeCardProps) {
  const trendColors = {
    BULLISH: 'border-bullish bg-red-50',
    BEARISH: 'border-bearish bg-green-50',
    NEUTRAL: 'border-neutral bg-gray-50',
    DEGRADED: 'border-yellow-500 bg-yellow-50',
  };

  const scoreColor = signal.score >= 70
    ? 'text-bullish'
    : signal.score <= 40
      ? 'text-bearish'
      : 'text-neutral';

  return (
    <div
      className={clsx(
        'border-l-4 rounded-lg p-4 shadow-sm hover:shadow-md transition-shadow cursor-pointer',
        trendColors[signal.trend]
      )}
      onClick={onClick}
    >
      <div className="flex items-center justify-between mb-2">
        <div className="flex items-center gap-3">
          <span className="text-2xl font-bold text-gray-400">#{rank}</span>
          <div>
            <h3 className="text-lg font-semibold">{signal.ticker}</h3>
            <span className={clsx('text-sm font-medium', scoreColor)}>
              {signal.trend === 'DEGRADED' ? '降級模式' : signal.trend}
            </span>
          </div>
        </div>
        <div className="text-right">
          <div className={clsx('text-3xl font-bold', scoreColor)}>
            {signal.score}
          </div>
          <div className="text-xs text-gray-500">Squeeze Score</div>
        </div>
      </div>

      <p className="text-sm text-gray-600 mb-3">{signal.comment}</p>

      {signal.factors && <FactorBreakdown factors={signal.factors} />}
    </div>
  );
}
```

### Task 4.9: 建立因子分解圖

```tsx
// src/components/squeeze/FactorBreakdown.tsx
import type { FactorScores } from '../../types';

interface FactorBreakdownProps {
  factors: FactorScores;
}

const factorLabels = {
  borrowScore: '法人回補',
  gammaScore: 'Gamma壓縮',
  marginScore: '空單擁擠',
  momentumScore: '價量動能',
};

const factorWeights = {
  borrowScore: 0.35,
  gammaScore: 0.25,
  marginScore: 0.20,
  momentumScore: 0.20,
};

export function FactorBreakdown({ factors }: FactorBreakdownProps) {
  const entries = Object.entries(factors) as [keyof FactorScores, number][];

  return (
    <div className="space-y-2">
      {entries.map(([key, value]) => {
        const percentage = (value / 100) * 100;
        const weightedScore = value * factorWeights[key];

        return (
          <div key={key} className="flex items-center gap-2">
            <span className="text-xs text-gray-500 w-20">
              {factorLabels[key]}
            </span>
            <div className="flex-1 bg-gray-200 rounded-full h-2">
              <div
                className="bg-blue-500 h-2 rounded-full transition-all"
                style={{ width: `${percentage}%` }}
              />
            </div>
            <span className="text-xs font-medium w-12 text-right">
              {value.toFixed(0)}
            </span>
            <span className="text-xs text-gray-400 w-12 text-right">
              (+{weightedScore.toFixed(1)})
            </span>
          </div>
        );
      })}
    </div>
  );
}
```

### Task 4.10: 建立 Dashboard 頁面

```tsx
// src/pages/Dashboard.tsx
import { useState } from 'react';
import { useTopCandidates } from '../hooks/useSqueezeSignals';
import { useStockHistory } from '../hooks/useStockMetrics';
import { SqueezeCard } from '../components/squeeze/SqueezeCard';
import { IVHVChart } from '../components/charts/IVHVChart';

export function Dashboard() {
  const [selectedTicker, setSelectedTicker] = useState<string | null>(null);
  const { data: candidates, isLoading, error } = useTopCandidates(10, 60);
  const { data: history } = useStockHistory(selectedTicker || '', 30);

  if (isLoading) {
    return (
      <div className="flex items-center justify-center h-64">
        <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-500" />
      </div>
    );
  }

  if (error) {
    return (
      <div className="bg-red-50 text-red-600 p-4 rounded-lg">
        載入失敗：{(error as Error).message}
      </div>
    );
  }

  return (
    <div className="max-w-7xl mx-auto p-6">
      <header className="mb-8">
        <h1 className="text-3xl font-bold text-gray-900">Alpha Squeeze</h1>
        <p className="text-gray-500">
          戰術級量化決策支援平台 | 分析日期: {candidates?.analysisDate}
        </p>
      </header>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* 軋空清單 */}
        <div className="lg:col-span-1 space-y-4">
          <h2 className="text-xl font-semibold mb-4">🚀 今日軋空潛力排行</h2>
          {candidates?.candidates.map((signal, index) => (
            <SqueezeCard
              key={signal.ticker}
              signal={signal}
              rank={index + 1}
              onClick={() => setSelectedTicker(signal.ticker)}
            />
          ))}
        </div>

        {/* 詳細分析 */}
        <div className="lg:col-span-2">
          {selectedTicker ? (
            <div className="bg-white rounded-lg shadow p-6">
              <h2 className="text-xl font-semibold mb-4">
                {selectedTicker} IV/HV 走勢分析
              </h2>
              {history && <IVHVChart metrics={history} />}
            </div>
          ) : (
            <div className="bg-gray-50 rounded-lg p-12 text-center text-gray-500">
              請選擇左側標的查看詳細分析
            </div>
          )}
        </div>
      </div>

      <footer className="mt-8 text-center text-sm text-gray-400">
        最後更新: {candidates?.generatedAt}
      </footer>
    </div>
  );
}
```

### Task 4.11: 建立前端測試

```typescript
// src/components/squeeze/__tests__/SqueezeCard.test.tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { SqueezeCard } from '../SqueezeCard';
import type { SqueezeSignal } from '../../../types';

const mockSignal: SqueezeSignal = {
  ticker: '2330',
  score: 85,
  trend: 'BULLISH',
  comment: '軋空潛力高，法人回補訊號強勁',
  factors: {
    borrowScore: 90,
    gammaScore: 75,
    marginScore: 80,
    momentumScore: 85,
  },
};

describe('SqueezeCard', () => {
  it('renders ticker and score correctly', () => {
    render(<SqueezeCard signal={mockSignal} rank={1} />);

    expect(screen.getByText('2330')).toBeInTheDocument();
    expect(screen.getByText('85')).toBeInTheDocument();
    expect(screen.getByText('#1')).toBeInTheDocument();
  });

  it('shows BULLISH trend with correct styling', () => {
    render(<SqueezeCard signal={mockSignal} rank={1} />);

    expect(screen.getByText('BULLISH')).toBeInTheDocument();
  });

  it('calls onClick when clicked', () => {
    const handleClick = vi.fn();
    render(<SqueezeCard signal={mockSignal} rank={1} onClick={handleClick} />);

    fireEvent.click(screen.getByText('2330'));
    expect(handleClick).toHaveBeenCalledTimes(1);
  });

  it('renders factor breakdown when factors exist', () => {
    render(<SqueezeCard signal={mockSignal} rank={1} />);

    expect(screen.getByText('法人回補')).toBeInTheDocument();
    expect(screen.getByText('Gamma壓縮')).toBeInTheDocument();
  });

  it('handles degraded mode correctly', () => {
    const degradedSignal: SqueezeSignal = {
      ...mockSignal,
      trend: 'DEGRADED',
      factors: null,
    };

    render(<SqueezeCard signal={degradedSignal} rank={1} />);
    expect(screen.getByText('降級模式')).toBeInTheDocument();
  });
});
```

## 驗收標準

### 功能驗收
- [ ] Dashboard 正確顯示軋空排行
- [ ] 點擊標的可查看 IV/HV 走勢圖
- [ ] 因子分解圖正確呈現
- [ ] 降級模式正確顯示
- [ ] RWD 響應式設計

### 測試驗收
- [ ] 元件單元測試通過
- [ ] Hook 測試通過
- [ ] 整合測試通過

### 品質檢查
- [ ] 無 TypeScript 錯誤
- [ ] Lighthouse 效能分數 > 90
- [ ] 無 Console 錯誤

## 執行測試
```bash
cd web
npm run test
npm run build  # 確認可正常建置
```

## 完成後輸出
1. 可運行的 React Dashboard
2. 測試報告
3. 螢幕截圖展示
