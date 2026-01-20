import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { discoveryApi } from '../services/api';
import type { DiscoveryPoolDto, DiscoveryFilterRequest } from '../types';
import { Card, CardHeader, CardTitle, CardContent } from '../components/ui/Card';
import { Badge } from '../components/ui/Badge';
import { cn, formatNumber } from '../lib/utils';

const SCORE_COLORS = {
  high: 'text-bullish-400 bg-bullish-500/10',
  medium: 'text-accent-400 bg-accent-500/10',
  low: 'text-dark-400 bg-dark-500/10',
};

function getScoreColor(score?: number) {
  if (!score) return SCORE_COLORS.low;
  if (score >= 70) return SCORE_COLORS.high;
  if (score >= 50) return SCORE_COLORS.medium;
  return SCORE_COLORS.low;
}

function DiscoveryRow({
  item,
  isSelected,
  onSelect,
}: {
  item: DiscoveryPoolDto;
  isSelected: boolean;
  onSelect: (ticker: string) => void;
}) {
  const scoreColor = getScoreColor(item.squeezeScore);

  return (
    <tr
      className={cn(
        'border-b border-dark-700/30 transition-colors cursor-pointer',
        isSelected ? 'bg-primary-500/10' : 'hover:bg-dark-800/30'
      )}
      onClick={() => onSelect(item.ticker)}
    >
      <td className="px-4 py-3">
        <input
          type="checkbox"
          checked={isSelected}
          onChange={() => onSelect(item.ticker)}
          className="w-4 h-4 rounded border-dark-600 text-primary-500 focus:ring-primary-500 bg-dark-700"
          onClick={(e) => e.stopPropagation()}
        />
      </td>
      <td className="px-4 py-3">
        <div className="flex items-center gap-2">
          <span className="font-mono font-semibold text-dark-100">{item.ticker}</span>
          {item.tickerName && (
            <span className="text-sm text-dark-400">{item.tickerName}</span>
          )}
        </div>
      </td>
      <td className="px-4 py-3">
        {item.industry && <Badge variant="default" size="sm">{item.industry}</Badge>}
      </td>
      <td className="px-4 py-3 text-right font-mono text-dark-200">
        {item.closePrice ? formatNumber(item.closePrice) : '-'}
      </td>
      <td className="px-4 py-3 text-right font-mono text-dark-200">
        {item.volume ? formatNumber(item.volume) : '-'}
      </td>
      <td className="px-4 py-3 text-right">
        <span className={cn('font-mono', item.volMultiplier && item.volMultiplier >= 2 ? 'text-bullish-400' : 'text-dark-300')}>
          {item.volMultiplier ? `${item.volMultiplier.toFixed(1)}x` : '-'}
        </span>
      </td>
      <td className="px-4 py-3 text-right">
        <span className={cn('font-mono', item.shortRatio && item.shortRatio >= 5 ? 'text-bearish-400' : 'text-dark-300')}>
          {item.shortRatio ? `${item.shortRatio.toFixed(2)}%` : '-'}
        </span>
      </td>
      <td className="px-4 py-3 text-center">
        {item.hasCB ? (
          <Badge variant="bullish" size="sm">
            {item.cbTicker || 'CB'}
          </Badge>
        ) : (
          <span className="text-dark-500">-</span>
        )}
      </td>
      <td className="px-4 py-3 text-center">
        <span className={cn('px-2 py-0.5 rounded text-sm font-semibold', scoreColor)}>
          {item.squeezeScore ?? '-'}
        </span>
      </td>
    </tr>
  );
}

function FilterPanel({
  filter,
  onChange,
  config,
}: {
  filter: DiscoveryFilterRequest;
  onChange: (filter: DiscoveryFilterRequest) => void;
  config: Record<string, string>;
}) {
  return (
    <div className="grid grid-cols-2 md:grid-cols-4 lg:grid-cols-6 gap-4">
      <div>
        <label className="block text-xs text-dark-400 mb-1">最低股價</label>
        <input
          type="number"
          value={filter.minPrice ?? config.MinPrice ?? 10}
          onChange={(e) => onChange({ ...filter, minPrice: parseFloat(e.target.value) || undefined })}
          className="w-full px-3 py-1.5 rounded bg-dark-700 border border-dark-600 text-dark-200 text-sm focus:outline-none focus:border-primary-500"
          min={1}
          step={1}
        />
      </div>
      <div>
        <label className="block text-xs text-dark-400 mb-1">最低成交量</label>
        <input
          type="number"
          value={filter.minVolume ?? config.MinVolume ?? 1000}
          onChange={(e) => onChange({ ...filter, minVolume: parseInt(e.target.value) || undefined })}
          className="w-full px-3 py-1.5 rounded bg-dark-700 border border-dark-600 text-dark-200 text-sm focus:outline-none focus:border-primary-500"
          min={100}
          step={100}
        />
      </div>
      <div>
        <label className="block text-xs text-dark-400 mb-1">借券比例 %</label>
        <input
          type="number"
          value={filter.minShortRatio ?? config.MinShortRatio ?? 3}
          onChange={(e) => onChange({ ...filter, minShortRatio: parseFloat(e.target.value) || undefined })}
          className="w-full px-3 py-1.5 rounded bg-dark-700 border border-dark-600 text-dark-200 text-sm focus:outline-none focus:border-primary-500"
          min={0}
          step={0.5}
        />
      </div>
      <div>
        <label className="block text-xs text-dark-400 mb-1">量能倍數</label>
        <input
          type="number"
          value={filter.minVolMultiplier ?? config.MinVolMultiplier ?? 1.5}
          onChange={(e) => onChange({ ...filter, minVolMultiplier: parseFloat(e.target.value) || undefined })}
          className="w-full px-3 py-1.5 rounded bg-dark-700 border border-dark-600 text-dark-200 text-sm focus:outline-none focus:border-primary-500"
          min={1}
          step={0.1}
        />
      </div>
      <div>
        <label className="block text-xs text-dark-400 mb-1">最低分數</label>
        <input
          type="number"
          value={filter.minScore ?? 0}
          onChange={(e) => onChange({ ...filter, minScore: parseInt(e.target.value) || undefined })}
          className="w-full px-3 py-1.5 rounded bg-dark-700 border border-dark-600 text-dark-200 text-sm focus:outline-none focus:border-primary-500"
          min={0}
          max={100}
          step={5}
        />
      </div>
      <div>
        <label className="block text-xs text-dark-400 mb-1">需有 CB</label>
        <select
          value={filter.hasCB === true ? 'yes' : filter.hasCB === false ? 'no' : ''}
          onChange={(e) => onChange({ ...filter, hasCB: e.target.value === 'yes' ? true : e.target.value === 'no' ? false : undefined })}
          className="w-full px-3 py-1.5 rounded bg-dark-700 border border-dark-600 text-dark-200 text-sm focus:outline-none focus:border-primary-500"
        >
          <option value="">不限</option>
          <option value="yes">是</option>
          <option value="no">否</option>
        </select>
      </div>
    </div>
  );
}

export function DiscoveryPage() {
  const queryClient = useQueryClient();
  const [selectedTickers, setSelectedTickers] = useState<Set<string>>(new Set());
  const [filter, setFilter] = useState<DiscoveryFilterRequest>({ limit: 100 });

  // Fetch config
  const { data: config = {} } = useQuery({
    queryKey: ['discovery-config'],
    queryFn: discoveryApi.getConfig,
  });

  // Fetch discovery pool
  const { data: poolData, isLoading, error, refetch } = useQuery({
    queryKey: ['discovery-pool', filter],
    queryFn: () => discoveryApi.filterPool(filter),
  });

  // Fetch watchlist
  const { data: watchList = [] } = useQuery({
    queryKey: ['discovery-watchlist'],
    queryFn: discoveryApi.getWatchList,
  });

  // Bulk add mutation
  const bulkAddMutation = useMutation({
    mutationFn: (tickers: string[]) => discoveryApi.bulkAddToWatchList(tickers),
    onSuccess: (result) => {
      queryClient.invalidateQueries({ queryKey: ['discovery-watchlist'] });
      alert(`已新增 ${result.addedCount} 檔至追蹤清單`);
      setSelectedTickers(new Set());
    },
  });

  // Toggle selection
  const handleSelect = (ticker: string) => {
    setSelectedTickers((prev) => {
      const next = new Set(prev);
      if (next.has(ticker)) {
        next.delete(ticker);
      } else {
        next.add(ticker);
      }
      return next;
    });
  };

  // Select all
  const handleSelectAll = () => {
    if (poolData?.items) {
      if (selectedTickers.size === poolData.items.length) {
        setSelectedTickers(new Set());
      } else {
        setSelectedTickers(new Set(poolData.items.map((i) => i.ticker)));
      }
    }
  };

  // Add selected to watchlist
  const handleAddToWatchList = () => {
    if (selectedTickers.size > 0) {
      bulkAddMutation.mutate(Array.from(selectedTickers));
    }
  };

  if (error) {
    return (
      <div className="flex items-center justify-center min-h-[400px]">
        <Card variant="glass" className="max-w-md w-full">
          <CardContent className="py-8 text-center">
            <div className="w-16 h-16 mx-auto mb-4 rounded-full bg-bearish-500/10 flex items-center justify-center">
              <svg className="w-8 h-8 text-bearish-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
              </svg>
            </div>
            <h3 className="text-lg font-semibold text-dark-100 mb-2">載入失敗</h3>
            <p className="text-sm text-dark-400">{(error as Error).message}</p>
          </CardContent>
        </Card>
      </div>
    );
  }

  return (
    <div className="space-y-6 animate-fade-in">
      {/* Header */}
      <div className="relative overflow-hidden rounded-2xl bg-gradient-to-br from-dark-800 via-dark-800 to-dark-900 border border-dark-700/50 p-6">
        <div className="absolute inset-0 bg-[radial-gradient(ellipse_at_top_right,rgba(51,129,255,0.1),transparent_50%)]" />

        <div className="relative flex flex-col md:flex-row md:items-center justify-between gap-4">
          <div className="flex items-center gap-4">
            <div className="w-12 h-12 rounded-xl bg-primary-500/20 flex items-center justify-center">
              <svg className="w-6 h-6 text-primary-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9.663 17h4.673M12 3v1m6.364 1.636l-.707.707M21 12h-1M4 12H3m3.343-5.657l-.707-.707m2.828 9.9a5 5 0 117.072 0l-.548.547A3.374 3.374 0 0014 18.469V19a2 2 0 11-4 0v-.531c0-.895-.356-1.754-.988-2.386l-.548-.547z" />
              </svg>
            </div>
            <div>
              <h1 className="text-2xl font-bold text-dark-50">雷達掃描</h1>
              <p className="text-dark-400 text-sm">
                掃描日期: {poolData?.scanDate || '-'} | 共 {poolData?.totalCount || 0} 檔
              </p>
            </div>
          </div>

          <div className="flex items-center gap-3">
            {selectedTickers.size > 0 && (
              <button
                onClick={handleAddToWatchList}
                disabled={bulkAddMutation.isPending}
                className="flex items-center gap-2 px-4 py-2 rounded-lg bg-bullish-500 text-white hover:bg-bullish-600 transition-colors disabled:opacity-50"
              >
                <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
                </svg>
                加入追蹤 ({selectedTickers.size})
              </button>
            )}
            <button
              onClick={() => refetch()}
              className="p-2 rounded-lg bg-dark-700 hover:bg-dark-600 transition-colors"
            >
              <svg className="w-5 h-5 text-dark-300" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
              </svg>
            </button>
          </div>
        </div>
      </div>

      {/* Filter Panel */}
      <Card>
        <CardHeader>
          <CardTitle>
            <svg className="w-5 h-5 text-accent-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 6V4m0 2a2 2 0 100 4m0-4a2 2 0 110 4m-6 8a2 2 0 100-4m0 4a2 2 0 110-4m0 4v2m0-6V4m6 6v10m6-2a2 2 0 100-4m0 4a2 2 0 110-4m0 4v2m0-6V4" />
            </svg>
            篩選條件
          </CardTitle>
        </CardHeader>
        <CardContent>
          <FilterPanel filter={filter} onChange={setFilter} config={config} />
        </CardContent>
      </Card>

      {/* Results Table */}
      <Card>
        <CardContent className="p-0">
          {isLoading ? (
            <div className="flex items-center justify-center py-12">
              <div className="w-8 h-8 border-2 border-dark-600 border-t-primary-500 rounded-full animate-spin" />
            </div>
          ) : poolData?.items && poolData.items.length > 0 ? (
            <div className="overflow-x-auto">
              <table className="w-full">
                <thead>
                  <tr className="border-b border-dark-700/50 bg-dark-800/50">
                    <th className="px-4 py-3 text-left">
                      <input
                        type="checkbox"
                        checked={selectedTickers.size === poolData.items.length && poolData.items.length > 0}
                        onChange={handleSelectAll}
                        className="w-4 h-4 rounded border-dark-600 text-primary-500 focus:ring-primary-500 bg-dark-700"
                      />
                    </th>
                    <th className="px-4 py-3 text-left text-sm font-medium text-dark-400">股票</th>
                    <th className="px-4 py-3 text-left text-sm font-medium text-dark-400">產業</th>
                    <th className="px-4 py-3 text-right text-sm font-medium text-dark-400">股價</th>
                    <th className="px-4 py-3 text-right text-sm font-medium text-dark-400">成交量</th>
                    <th className="px-4 py-3 text-right text-sm font-medium text-dark-400">量倍數</th>
                    <th className="px-4 py-3 text-right text-sm font-medium text-dark-400">借券比</th>
                    <th className="px-4 py-3 text-center text-sm font-medium text-dark-400">CB</th>
                    <th className="px-4 py-3 text-center text-sm font-medium text-dark-400">分數</th>
                  </tr>
                </thead>
                <tbody>
                  {poolData.items.map((item) => (
                    <DiscoveryRow
                      key={item.ticker}
                      item={item}
                      isSelected={selectedTickers.has(item.ticker)}
                      onSelect={handleSelect}
                    />
                  ))}
                </tbody>
              </table>
            </div>
          ) : (
            <div className="text-center py-12 text-dark-500">
              沒有符合條件的標的
            </div>
          )}
        </CardContent>
      </Card>

      {/* Watchlist Summary */}
      {watchList.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle>
              <svg className="w-5 h-5 text-bullish-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 5a2 2 0 012-2h10a2 2 0 012 2v16l-7-3.5L5 21V5z" />
              </svg>
              我的追蹤清單
              <Badge variant="default" size="sm" className="ml-2">
                {watchList.filter((w) => w.isActive).length} 檔啟用
              </Badge>
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="flex flex-wrap gap-2">
              {watchList.map((w) => (
                <span
                  key={w.ticker}
                  className={cn(
                    'px-3 py-1 rounded-full text-sm font-mono',
                    w.isActive
                      ? 'bg-primary-500/20 text-primary-400'
                      : 'bg-dark-700 text-dark-500'
                  )}
                >
                  {w.ticker}
                  {w.lastSqueezeScore && (
                    <span className="ml-1 text-xs">({w.lastSqueezeScore})</span>
                  )}
                </span>
              ))}
            </div>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
