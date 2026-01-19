import axios from 'axios';
import type { SqueezeSignal, TopCandidates, StockMetric, HealthStatus } from '../types';

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

export default api;
