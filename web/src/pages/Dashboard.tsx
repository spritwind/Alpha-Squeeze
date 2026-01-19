import { useState } from 'react';
import { useTopCandidates } from '../hooks/useSqueezeSignals';
import { useStockHistory } from '../hooks/useStockMetrics';
import { SqueezeList } from '../components/squeeze/SqueezeList';
import { IVHVChart } from '../components/charts/IVHVChart';
import { PriceChart } from '../components/charts/PriceChart';
import { FactorBreakdown } from '../components/squeeze/FactorBreakdown';
import { ScoreGauge } from '../components/charts/ScoreGauge';
import { Card, CardHeader, CardTitle, CardContent } from '../components/ui/Card';
import { Badge, getTrendVariant } from '../components/ui/Badge';
import { formatDate, formatPercent, formatVolume } from '../lib/utils';
import { cn } from '../lib/utils';

export function Dashboard() {
  const [selectedTicker, setSelectedTicker] = useState<string | null>(null);
  const { data: candidates, isLoading, error } = useTopCandidates(10, 60);
  const { data: history } = useStockHistory(selectedTicker || '', 30);

  const selectedSignal = candidates?.candidates.find(
    (c) => c.ticker === selectedTicker
  );

  if (error) {
    return (
      <div className="flex items-center justify-center min-h-[400px]">
        <Card variant="glass" className="max-w-md w-full">
          <CardContent className="py-8 text-center">
            <div className="w-16 h-16 mx-auto mb-4 rounded-full bg-bullish-500/10 flex items-center justify-center">
              <svg className="w-8 h-8 text-bullish-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
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
    <div className="space-y-8 animate-fade-in">
      {/* Hero Section */}
      <div className="relative overflow-hidden rounded-2xl bg-gradient-to-br from-dark-800 via-dark-800 to-dark-900 border border-dark-700/50 p-8">
        <div className="absolute inset-0 bg-[radial-gradient(ellipse_at_top_right,rgba(51,129,255,0.15),transparent_50%)]" />
        <div className="absolute inset-0 bg-[radial-gradient(ellipse_at_bottom_left,rgba(251,191,36,0.08),transparent_50%)]" />

        <div className="relative">
          <div className="flex flex-col md:flex-row md:items-end justify-between gap-4">
            <div>
              <div className="flex items-center gap-3 mb-2">
                <h1 className="text-3xl font-bold text-dark-50">軋空訊號 Dashboard</h1>
                <Badge variant="primary">LIVE</Badge>
              </div>
              <p className="text-dark-400">
                即時追蹤高潛力軋空標的，掌握法人回補與 Gamma 擠壓機會
              </p>
            </div>
            <div className="flex items-center gap-6 text-sm">
              <div className="text-right">
                <p className="text-dark-500">分析日期</p>
                <p className="font-semibold text-dark-200">{candidates?.analysisDate || '-'}</p>
              </div>
              <div className="text-right">
                <p className="text-dark-500">最後更新</p>
                <p className="font-semibold text-dark-200">
                  {candidates?.generatedAt ? formatDate(candidates.generatedAt, 'long') : '-'}
                </p>
              </div>
            </div>
          </div>
        </div>
      </div>

      {/* Main Content */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6">
        {/* Left Sidebar - Squeeze Ranking */}
        <div className="lg:col-span-4 xl:col-span-3">
          <Card variant="glass" className="sticky top-24">
            <CardHeader className="border-b-0 pb-2">
              <div className="flex items-center justify-between">
                <CardTitle>
                  <svg className="w-5 h-5 text-accent-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 7h8m0 0v8m0-8l-8 8-4-4-6 6" />
                  </svg>
                  今日軋空潛力排行
                </CardTitle>
                <Badge variant="default" size="sm">
                  {candidates?.candidates.length || 0} 檔
                </Badge>
              </div>
            </CardHeader>
            <CardContent className="max-h-[calc(100vh-280px)] overflow-y-auto hide-scrollbar">
              <SqueezeList
                candidates={candidates?.candidates || []}
                isLoading={isLoading}
                selectedTicker={selectedTicker}
                onSelect={setSelectedTicker}
              />
            </CardContent>
          </Card>
        </div>

        {/* Right Content - Detail Analysis */}
        <div className="lg:col-span-8 xl:col-span-9 space-y-6">
          {selectedTicker && selectedSignal ? (
            <>
              {/* Stock Summary Card */}
              <Card variant="gradient">
                <CardContent className="py-6">
                  <div className="flex flex-col lg:flex-row lg:items-start justify-between gap-6">
                    {/* Left: Score & Info */}
                    <div className="flex items-start gap-5">
                      <ScoreGauge score={selectedSignal.score} size="lg" />
                      <div>
                        <div className="flex items-center gap-3 mb-2">
                          <h2 className="text-2xl font-bold text-dark-50">
                            {selectedSignal.ticker}
                          </h2>
                          <Badge variant={getTrendVariant(selectedSignal.trend)}>
                            {selectedSignal.trend === 'DEGRADED' ? '降級模式' : selectedSignal.trend}
                          </Badge>
                        </div>
                        <p className="text-dark-400 max-w-lg leading-relaxed">
                          {selectedSignal.comment}
                        </p>
                      </div>
                    </div>

                    {/* Right: Latest Metrics */}
                    {history && history.length > 0 && (
                      <div className="flex gap-6 lg:gap-8">
                        <div className="text-center">
                          <p className="text-xs text-dark-500 mb-1">收盤價</p>
                          <p className="text-2xl font-bold text-dark-100 tabular-nums">
                            ${history[history.length - 1]?.closePrice?.toFixed(2) || '-'}
                          </p>
                        </div>
                        <div className="text-center">
                          <p className="text-xs text-dark-500 mb-1">成交量</p>
                          <p className="text-lg font-semibold text-dark-200 tabular-nums">
                            {formatVolume(history[history.length - 1]?.volume)}
                          </p>
                        </div>
                        <div className="text-center">
                          <p className="text-xs text-dark-500 mb-1">券資比</p>
                          <p className={cn(
                            'text-lg font-semibold tabular-nums',
                            (history[history.length - 1]?.marginRatio ?? 0) >= 20
                              ? 'text-bullish-400'
                              : 'text-dark-200'
                          )}>
                            {formatPercent(history[history.length - 1]?.marginRatio)}
                          </p>
                        </div>
                      </div>
                    )}
                  </div>
                </CardContent>
              </Card>

              {/* Factor Breakdown */}
              {selectedSignal.factors && (
                <Card>
                  <CardHeader>
                    <CardTitle>
                      <svg className="w-5 h-5 text-primary-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z" />
                      </svg>
                      因子分解
                    </CardTitle>
                  </CardHeader>
                  <CardContent>
                    <FactorBreakdown factors={selectedSignal.factors} showWeighted />
                  </CardContent>
                </Card>
              )}

              {/* Charts */}
              <div className="grid grid-cols-1 xl:grid-cols-2 gap-6">
                {/* Price Chart */}
                <Card>
                  <CardHeader>
                    <CardTitle>
                      <svg className="w-5 h-5 text-bearish-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M7 12l3-3 3 3 4-4M8 21l4-4 4 4M3 4h18M4 4h16v12a1 1 0 01-1 1H5a1 1 0 01-1-1V4z" />
                      </svg>
                      價格走勢 (30日)
                    </CardTitle>
                  </CardHeader>
                  <CardContent>
                    {history ? (
                      <PriceChart metrics={history} />
                    ) : (
                      <div className="h-64 flex items-center justify-center">
                        <div className="flex flex-col items-center gap-2 text-dark-500">
                          <div className="w-8 h-8 border-2 border-dark-600 border-t-primary-500 rounded-full animate-spin" />
                          <span className="text-sm">載入中...</span>
                        </div>
                      </div>
                    )}
                  </CardContent>
                </Card>

                {/* IV/HV Chart */}
                <Card>
                  <CardHeader>
                    <CardTitle>
                      <svg className="w-5 h-5 text-accent-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 17h8m0 0V9m0 8l-8-8-4 4-6-6" />
                      </svg>
                      波動率走勢 (IV/HV)
                    </CardTitle>
                  </CardHeader>
                  <CardContent>
                    {history ? (
                      <IVHVChart metrics={history} />
                    ) : (
                      <div className="h-80 flex items-center justify-center">
                        <div className="flex flex-col items-center gap-2 text-dark-500">
                          <div className="w-8 h-8 border-2 border-dark-600 border-t-primary-500 rounded-full animate-spin" />
                          <span className="text-sm">載入中...</span>
                        </div>
                      </div>
                    )}
                  </CardContent>
                </Card>
              </div>
            </>
          ) : (
            /* Empty State */
            <Card variant="glass" className="min-h-[500px] flex items-center justify-center">
              <div className="text-center max-w-sm">
                <div className="w-20 h-20 mx-auto mb-6 rounded-2xl bg-gradient-to-br from-primary-500/20 to-accent-500/10 flex items-center justify-center">
                  <svg className="w-10 h-10 text-primary-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M15 15l-2 5L9 9l11 4-5 2zm0 0l5 5M7.188 2.239l.777 2.897M5.136 7.965l-2.898-.777M13.95 4.05l-2.122 2.122m-5.657 5.656l-2.12 2.122" />
                  </svg>
                </div>
                <h3 className="text-xl font-semibold text-dark-100 mb-2">
                  選擇標的開始分析
                </h3>
                <p className="text-dark-400 leading-relaxed">
                  從左側軋空排行榜選擇任一標的，即可查看詳細的因子分解與走勢圖表
                </p>
              </div>
            </Card>
          )}
        </div>
      </div>
    </div>
  );
}
