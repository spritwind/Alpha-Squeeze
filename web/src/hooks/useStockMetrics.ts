import { useQuery } from '@tanstack/react-query';
import { metricsApi } from '../services/api';

/**
 * 取得指定日期的所有股票指標
 */
export function useStockMetrics(date?: string) {
  return useQuery({
    queryKey: ['stockMetrics', date],
    queryFn: () => metricsApi.getByDate(date),
    staleTime: 60000,
  });
}

/**
 * 取得單一標的指標
 */
export function useStockMetric(ticker: string, date?: string) {
  return useQuery({
    queryKey: ['stockMetric', ticker, date],
    queryFn: () => metricsApi.getByTicker(ticker, date),
    enabled: !!ticker,
  });
}

/**
 * 取得單一標的歷史資料
 */
export function useStockHistory(ticker: string, days = 30) {
  return useQuery({
    queryKey: ['stockHistory', ticker, days],
    queryFn: () => metricsApi.getHistory(ticker, days),
    enabled: !!ticker,
  });
}

/**
 * 取得高券資比標的
 */
export function useHighMarginRatio(minRatio = 10, limit = 20, date?: string) {
  return useQuery({
    queryKey: ['highMarginRatio', minRatio, limit, date],
    queryFn: () => metricsApi.getHighMarginRatio(minRatio, limit, date),
    staleTime: 60000,
  });
}

/**
 * 取得大量回補標的
 */
export function useShortCovering(limit = 20, date?: string) {
  return useQuery({
    queryKey: ['shortCovering', limit, date],
    queryFn: () => metricsApi.getShortCovering(limit, date),
    staleTime: 60000,
  });
}
