import { useParams, Link } from 'react-router-dom';
import { useSqueezeSignal } from '../hooks/useSqueezeSignals';
import { useStockHistory, useStockMetric } from '../hooks/useStockMetrics';
import { IVHVChart } from '../components/charts/IVHVChart';
import { PriceChart } from '../components/charts/PriceChart';
import { FactorBreakdown } from '../components/squeeze/FactorBreakdown';
import { ScoreGauge } from '../components/charts/ScoreGauge';
import { Card, CardHeader, CardTitle, CardContent } from '../components/ui/Card';
import { Badge, getTrendVariant } from '../components/ui/Badge';
import { Button } from '../components/ui/Button';
import { LoadingOverlay } from '../components/ui/Spinner';
import { formatPercent, formatVolume, formatNumber } from '../lib/utils';

export function StockDetail() {
  const { ticker } = useParams<{ ticker: string }>();

  const { data: signal, isLoading: signalLoading, error: signalError } = useSqueezeSignal(ticker || '');
  const { data: metric, isLoading: metricLoading } = useStockMetric(ticker || '');
  const { data: history, isLoading: historyLoading } = useStockHistory(ticker || '', 60);

  const isLoading = signalLoading || metricLoading || historyLoading;

  if (isLoading) {
    return <LoadingOverlay message={`載入 ${ticker} 資料中...`} />;
  }

  if (signalError) {
    return (
      <div className="space-y-4">
        <Link to="/">
          <Button variant="outline">← 返回 Dashboard</Button>
        </Link>
        <div className="bg-red-50 border border-red-200 text-red-600 p-6 rounded-lg">
          <h3 className="font-semibold text-lg mb-2">無法載入資料</h3>
          <p>{(signalError as Error).message}</p>
        </div>
      </div>
    );
  }

  if (!signal) {
    return (
      <div className="space-y-4">
        <Link to="/">
          <Button variant="outline">← 返回 Dashboard</Button>
        </Link>
        <div className="bg-yellow-50 border border-yellow-200 text-yellow-700 p-6 rounded-lg">
          <h3 className="font-semibold text-lg mb-2">找不到資料</h3>
          <p>找不到 {ticker} 的軋空訊號資料</p>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* 導航 */}
      <div>
        <Link to="/">
          <Button variant="ghost" size="sm">← 返回 Dashboard</Button>
        </Link>
      </div>

      {/* 標的摘要 */}
      <Card>
        <CardContent className="py-8">
          <div className="flex flex-col md:flex-row md:items-center md:justify-between gap-6">
            <div className="flex items-center gap-6">
              <ScoreGauge score={signal.score} size="lg" />
              <div>
                <h1 className="text-3xl font-bold text-gray-900">{signal.ticker}</h1>
                <Badge variant={getTrendVariant(signal.trend)} className="mt-1">
                  {signal.trend === 'DEGRADED' ? '降級模式' : signal.trend}
                </Badge>
                <p className="text-gray-600 mt-3 max-w-lg">
                  {signal.comment}
                </p>
              </div>
            </div>

            {/* 最新指標 */}
            {metric && (
              <div className="grid grid-cols-2 md:grid-cols-4 gap-4 md:gap-6">
                <div className="text-center">
                  <div className="text-2xl font-bold text-gray-900">
                    ${formatNumber(metric.closePrice, 2)}
                  </div>
                  <div className="text-sm text-gray-500">收盤價</div>
                </div>
                <div className="text-center">
                  <div className="text-2xl font-bold text-gray-900">
                    {formatPercent(metric.marginRatio)}
                  </div>
                  <div className="text-sm text-gray-500">券資比</div>
                </div>
                <div className="text-center">
                  <div className="text-2xl font-bold text-gray-900">
                    {formatVolume(metric.volume)}
                  </div>
                  <div className="text-sm text-gray-500">成交量</div>
                </div>
                <div className="text-center">
                  <div className="text-2xl font-bold text-gray-900">
                    {metric.borrowingBalanceChange
                      ? `${metric.borrowingBalanceChange > 0 ? '+' : ''}${formatNumber(metric.borrowingBalanceChange, 0)}`
                      : '-'}
                  </div>
                  <div className="text-sm text-gray-500">借券變化</div>
                </div>
              </div>
            )}
          </div>
        </CardContent>
      </Card>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* 因子分解 */}
        {signal.factors && (
          <Card>
            <CardHeader>
              <CardTitle>📊 因子分解</CardTitle>
            </CardHeader>
            <CardContent>
              <FactorBreakdown factors={signal.factors} showWeighted />
              <div className="mt-4 pt-4 border-t border-gray-100">
                <p className="text-sm text-gray-500">
                  軋空分數 = 法人回補(35%) + Gamma壓縮(25%) + 空單擁擠(20%) + 價量動能(20%)
                </p>
              </div>
            </CardContent>
          </Card>
        )}

        {/* 歷史波動率 */}
        {metric && (
          <Card>
            <CardHeader>
              <CardTitle>📈 關鍵指標</CardTitle>
            </CardHeader>
            <CardContent>
              <div className="space-y-4">
                <div className="flex justify-between items-center py-2 border-b border-gray-100">
                  <span className="text-gray-600">20日歷史波動率</span>
                  <span className="font-medium">{formatPercent(metric.historicalVolatility20D)}</span>
                </div>
                <div className="flex justify-between items-center py-2 border-b border-gray-100">
                  <span className="text-gray-600">今日最高/最低</span>
                  <span className="font-medium">
                    ${formatNumber(metric.highPrice, 2)} / ${formatNumber(metric.lowPrice, 2)}
                  </span>
                </div>
                <div className="flex justify-between items-center py-2 border-b border-gray-100">
                  <span className="text-gray-600">開盤價</span>
                  <span className="font-medium">${formatNumber(metric.openPrice, 2)}</span>
                </div>
              </div>
            </CardContent>
          </Card>
        )}
      </div>

      {/* 價格走勢圖 */}
      <Card>
        <CardHeader>
          <CardTitle>📈 價格走勢 (60日)</CardTitle>
        </CardHeader>
        <CardContent>
          {history && history.length > 0 ? (
            <PriceChart metrics={history} />
          ) : (
            <div className="h-64 flex items-center justify-center text-gray-500">
              無歷史資料
            </div>
          )}
        </CardContent>
      </Card>

      {/* IV/HV 走勢圖 */}
      <Card>
        <CardHeader>
          <CardTitle>📉 波動率走勢 (IV/HV)</CardTitle>
        </CardHeader>
        <CardContent>
          {history && history.length > 0 ? (
            <IVHVChart metrics={history} />
          ) : (
            <div className="h-80 flex items-center justify-center text-gray-500">
              無歷史資料
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
