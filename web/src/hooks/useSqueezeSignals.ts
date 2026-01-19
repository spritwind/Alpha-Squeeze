import { useQuery } from '@tanstack/react-query';
import { squeezeApi } from '../services/api';

/**
 * 取得今日軋空潛力排行
 */
export function useTopCandidates(limit = 10, minScore = 60) {
  return useQuery({
    queryKey: ['topCandidates', limit, minScore],
    queryFn: () => squeezeApi.getTopCandidates(limit, minScore),
    refetchInterval: 60000, // 每分鐘更新
    staleTime: 30000,
  });
}

/**
 * 取得單一標的軋空訊號
 */
export function useSqueezeSignal(ticker: string, date?: string) {
  return useQuery({
    queryKey: ['squeezeSignal', ticker, date],
    queryFn: () => squeezeApi.getSignal(ticker, date),
    enabled: !!ticker,
  });
}

/**
 * 批量取得軋空訊號
 */
export function useBatchSignals(tickers: string[]) {
  return useQuery({
    queryKey: ['batchSignals', tickers],
    queryFn: () => squeezeApi.getBatchSignals(tickers),
    enabled: tickers.length > 0,
  });
}

/**
 * 取得系統健康狀態
 */
export function useHealthStatus() {
  return useQuery({
    queryKey: ['health'],
    queryFn: () => squeezeApi.getHealth(),
    refetchInterval: 30000,
    staleTime: 10000,
  });
}
