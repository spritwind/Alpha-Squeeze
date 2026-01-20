import axios from 'axios';
import type {
  SqueezeSignal,
  TopCandidates,
  StockMetric,
  HealthStatus,
  SqueezeConfigDto,
  ConfigCategoryDto,
  WeightsDto,
  ThresholdsDto,
  CBWarningDto,
  CBWarningListResponse,
  CBIssuanceDto,
  CBTrackingHistoryDto,
  TrackedTickerDto,
  AddTrackedTickerRequest,
  BackfillJobDto,
  DiscoveryPoolResponse,
  DiscoveryFilterRequest,
  UserWatchListDto,
} from '../types';

const api = axios.create({
  baseURL: import.meta.env.VITE_API_URL || '/api',
  timeout: 10000,
});

// 請求攔截器
api.interceptors.request.use(
  (config) => {
    // 可以在這裡添加認證 token 等
    return config;
  },
  (error) => {
    return Promise.reject(error);
  }
);

// 回應攔截器
api.interceptors.response.use(
  (response) => response,
  (error) => {
    console.error('API Error:', error.response?.data || error.message);
    return Promise.reject(error);
  }
);

export const squeezeApi = {
  /**
   * 取得今日軋空潛力排行
   */
  getTopCandidates: async (limit = 10, minScore = 60): Promise<TopCandidates> => {
    const { data } = await api.get('/squeeze/top-candidates', {
      params: { limit, minScore },
    });
    return data;
  },

  /**
   * 取得單一標的軋空訊號
   */
  getSignal: async (ticker: string, date?: string): Promise<SqueezeSignal> => {
    const { data } = await api.get(`/squeeze/${ticker}`, {
      params: { date },
    });
    return data;
  },

  /**
   * 批量取得軋空訊號
   */
  getBatchSignals: async (tickers: string[]): Promise<SqueezeSignal[]> => {
    const { data } = await api.get('/squeeze/batch', {
      params: { tickers: tickers.join(',') },
    });
    return data;
  },

  /**
   * 取得健康狀態
   */
  getHealth: async (): Promise<HealthStatus> => {
    const { data } = await api.get('/squeeze/health');
    return data;
  },
};

export const metricsApi = {
  /**
   * 取得指定日期的所有股票指標
   */
  getByDate: async (date?: string): Promise<StockMetric[]> => {
    const { data } = await api.get('/metrics', { params: { date } });
    return data;
  },

  /**
   * 取得單一標的指標
   */
  getByTicker: async (ticker: string, date?: string): Promise<StockMetric> => {
    const { data } = await api.get(`/metrics/${ticker}`, { params: { date } });
    return data;
  },

  /**
   * 取得單一標的歷史資料
   */
  getHistory: async (ticker: string, days = 30): Promise<StockMetric[]> => {
    const { data } = await api.get(`/metrics/${ticker}/history`, {
      params: { days },
    });
    return data;
  },

  /**
   * 取得高券資比標的
   */
  getHighMarginRatio: async (
    minRatio = 10,
    limit = 20,
    date?: string
  ): Promise<StockMetric[]> => {
    const { data } = await api.get('/metrics/high-margin-ratio', {
      params: { minRatio, limit, date },
    });
    return data;
  },

  /**
   * 取得大量回補標的
   */
  getShortCovering: async (limit = 20, date?: string): Promise<StockMetric[]> => {
    const { data } = await api.get('/metrics/short-covering', {
      params: { limit, date },
    });
    return data;
  },
};

export const configApi = {
  /**
   * 取得所有系統配置
   */
  getAll: async (): Promise<ConfigCategoryDto[]> => {
    const { data } = await api.get('/config');
    return data;
  },

  /**
   * 取得軋空演算法配置
   */
  getSqueezeConfig: async (): Promise<SqueezeConfigDto> => {
    const { data } = await api.get('/config/squeeze');
    return data;
  },

  /**
   * 更新軋空演算法配置
   */
  updateSqueezeConfig: async (
    weights: WeightsDto,
    thresholds: ThresholdsDto
  ): Promise<SqueezeConfigDto> => {
    const { data } = await api.put('/config/squeeze', { weights, thresholds });
    return data;
  },

  /**
   * 依分類取得配置
   */
  getByCategory: async (category: string): Promise<ConfigCategoryDto> => {
    const { data } = await api.get(`/config/category/${category}`);
    return data;
  },

  /**
   * 更新單一配置值
   */
  updateConfig: async (key: string, value: string): Promise<void> => {
    await api.put('/config', { key, value });
  },
};

// ========== CB 預警燈 API ==========

export const cbApi = {
  /**
   * 取得所有 CB 預警清單
   */
  getWarnings: async (date?: string, minLevel = 'SAFE'): Promise<CBWarningListResponse> => {
    const { data } = await api.get('/cb/warnings', {
      params: { date, minLevel },
    });
    return data;
  },

  /**
   * 取得單一 CB 預警狀態
   */
  getWarning: async (cbTicker: string, date?: string): Promise<CBWarningDto> => {
    const { data } = await api.get(`/cb/${cbTicker}`, { params: { date } });
    return data;
  },

  /**
   * 取得高風險 CB 排行
   */
  getCriticalCBs: async (limit = 10, minDays = 15): Promise<CBWarningDto[]> => {
    const { data } = await api.get('/cb/critical', {
      params: { limit, minDays },
    });
    return data;
  },

  /**
   * 依標的股票取得相關 CB
   */
  getByUnderlying: async (ticker: string): Promise<CBWarningDto[]> => {
    const { data } = await api.get(`/cb/by-underlying/${ticker}`);
    return data;
  },

  /**
   * 取得所有活躍 CB 發行資訊
   */
  getIssuances: async (): Promise<CBIssuanceDto[]> => {
    const { data } = await api.get('/cb/issuances');
    return data;
  },

  /**
   * 取得 CB 歷史追蹤資料
   */
  getHistory: async (cbTicker: string, days = 30): Promise<CBTrackingHistoryDto> => {
    const { data } = await api.get(`/cb/${cbTicker}/history`, {
      params: { days },
    });
    return data;
  },
};

// ========== Monitoring API ==========

export interface DataSourceStatus {
  name: string;
  tableName: string;
  totalRecords: number;
  lastUpdate: string | null;
  firstDate: string | null;
  status: 'OK' | 'EMPTY' | 'ERROR' | 'NOT_EXISTS';
  errorMessage?: string;
  additionalInfo?: Record<string, unknown>;
}

export interface SystemMonitoringData {
  timestamp: string;
  overallStatus: 'OK' | 'WARNING' | 'ERROR';
  dataSources: DataSourceStatus[];
}

export interface SystemLogEntry {
  timestamp: string;
  level: string;
  source: string;
  message: string;
  details?: string;
}

export interface SystemLogsResponse {
  logs: SystemLogEntry[];
  totalCount: number;
}

export const monitoringApi = {
  /**
   * 取得系統監控狀態
   */
  getStatus: async (): Promise<SystemMonitoringData> => {
    const { data } = await api.get('/monitoring/status');
    return data;
  },

  /**
   * 取得系統日誌
   */
  getLogs: async (limit = 100, level?: string): Promise<SystemLogsResponse> => {
    const { data } = await api.get('/monitoring/logs', {
      params: { limit, level },
    });
    return data;
  },

  /**
   * 新增系統日誌
   */
  addLog: async (message: string, source = 'WebUI', level = 'INFO', details?: string): Promise<void> => {
    await api.post('/monitoring/logs', { message, source, level, details });
  },

  /**
   * 清除日誌
   */
  clearLogs: async (): Promise<void> => {
    await api.delete('/monitoring/logs');
  },

  /**
   * 取得資料庫健康狀態
   */
  getDatabaseHealth: async (): Promise<{ status: string; timestamp: string; message: string }> => {
    const { data } = await api.get('/monitoring/health/database');
    return data;
  },
};

// ========== Discovery API ==========

export const discoveryApi = {
  /**
   * 取得最新掃描結果
   */
  getLatestPool: async (limit = 100): Promise<DiscoveryPoolResponse> => {
    const { data } = await api.get('/discovery/pool', { params: { limit } });
    return data;
  },

  /**
   * 依條件篩選掃描結果
   */
  filterPool: async (filter: DiscoveryFilterRequest): Promise<DiscoveryPoolResponse> => {
    const { data } = await api.get('/discovery/pool/filter', { params: filter });
    return data;
  },

  /**
   * 取得掃描配置
   */
  getConfig: async (): Promise<Record<string, string>> => {
    const { data } = await api.get('/discovery/config');
    return data;
  },

  /**
   * 更新掃描配置
   */
  updateConfig: async (key: string, value: string): Promise<void> => {
    await api.put(`/discovery/config/${key}`, { value });
  },

  /**
   * 取得用戶追蹤清單
   */
  getWatchList: async (): Promise<UserWatchListDto[]> => {
    const { data } = await api.get('/discovery/watchlist');
    return data;
  },

  /**
   * 新增至追蹤清單
   */
  addToWatchList: async (ticker: string, tickerName?: string): Promise<UserWatchListDto> => {
    const { data } = await api.post('/discovery/watchlist', { ticker, tickerName });
    return data;
  },

  /**
   * 批量新增至追蹤清單
   */
  bulkAddToWatchList: async (tickers: string[]): Promise<{ addedCount: number; requestedCount: number }> => {
    const { data } = await api.post('/discovery/watchlist/bulk', { tickers });
    return data;
  },

  /**
   * 從追蹤清單移除
   */
  removeFromWatchList: async (ticker: string): Promise<void> => {
    await api.delete(`/discovery/watchlist/${ticker}`);
  },

  /**
   * 設定追蹤項目啟用狀態
   */
  setWatchListActive: async (ticker: string, active: boolean): Promise<void> => {
    await api.patch(`/discovery/watchlist/${ticker}/active?active=${active}`);
  },
};

// ========== Admin API ==========

export const adminApi = {
  /**
   * 取得所有追蹤股票
   */
  getTickers: async (): Promise<TrackedTickerDto[]> => {
    const { data } = await api.get('/admin/tickers');
    return data;
  },

  /**
   * 取得啟用中的股票代號
   */
  getActiveTickers: async (): Promise<string[]> => {
    const { data } = await api.get('/admin/tickers/active');
    return data;
  },

  /**
   * 新增追蹤股票
   */
  addTicker: async (request: AddTrackedTickerRequest): Promise<TrackedTickerDto> => {
    const { data } = await api.post('/admin/tickers', request);
    return data;
  },

  /**
   * 更新追蹤股票
   */
  updateTicker: async (ticker: string, request: AddTrackedTickerRequest): Promise<TrackedTickerDto> => {
    const { data } = await api.put(`/admin/tickers/${ticker}`, request);
    return data;
  },

  /**
   * 設定股票啟用狀態
   */
  setTickerActive: async (ticker: string, active: boolean): Promise<void> => {
    await api.patch(`/admin/tickers/${ticker}/active?active=${active}`);
  },

  /**
   * 移除追蹤股票
   */
  removeTicker: async (ticker: string): Promise<void> => {
    await api.delete(`/admin/tickers/${ticker}`);
  },

  /**
   * 取得回補任務列表
   */
  getBackfillJobs: async (limit = 10): Promise<BackfillJobDto[]> => {
    const { data } = await api.get('/admin/backfill', { params: { limit } });
    return data;
  },

  /**
   * 建立回補任務
   */
  createBackfillJob: async (startDate: string, endDate: string, jobType = 'STOCK_METRICS', tickers?: string[]): Promise<BackfillJobDto> => {
    const { data } = await api.post('/admin/backfill', { startDate, endDate, jobType, tickers });
    return data;
  },
};

export default api;
