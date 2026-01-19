import { useQuery } from '@tanstack/react-query';
import { cbApi } from '../services/api';
import type { CBWarningLevel } from '../types';

/**
 * 取得所有 CB 預警清單
 */
export function useCBWarnings(date?: string, minLevel: CBWarningLevel = 'SAFE') {
  return useQuery({
    queryKey: ['cbWarnings', date, minLevel],
    queryFn: () => cbApi.getWarnings(date, minLevel),
    refetchInterval: 60000, // 每分鐘自動刷新
    staleTime: 30000,
  });
}

/**
 * 取得單一 CB 預警狀態
 */
export function useCBWarning(cbTicker: string, date?: string) {
  return useQuery({
    queryKey: ['cbWarning', cbTicker, date],
    queryFn: () => cbApi.getWarning(cbTicker, date),
    enabled: !!cbTicker,
  });
}

/**
 * 取得高風險 CB 排行
 */
export function useCriticalCBs(limit = 10, minDays = 15) {
  return useQuery({
    queryKey: ['criticalCBs', limit, minDays],
    queryFn: () => cbApi.getCriticalCBs(limit, minDays),
    refetchInterval: 60000,
  });
}

/**
 * 依標的股票取得相關 CB
 */
export function useCBsByUnderlying(ticker: string) {
  return useQuery({
    queryKey: ['cbsByUnderlying', ticker],
    queryFn: () => cbApi.getByUnderlying(ticker),
    enabled: !!ticker,
  });
}

/**
 * 取得所有活躍 CB 發行資訊
 */
export function useCBIssuances() {
  return useQuery({
    queryKey: ['cbIssuances'],
    queryFn: () => cbApi.getIssuances(),
    staleTime: 5 * 60 * 1000, // 5 分鐘
  });
}

/**
 * 取得 CB 歷史追蹤資料
 */
export function useCBHistory(cbTicker: string, days = 30) {
  return useQuery({
    queryKey: ['cbHistory', cbTicker, days],
    queryFn: () => cbApi.getHistory(cbTicker, days),
    enabled: !!cbTicker,
  });
}
