import { useState } from 'react';
import { useHighMarginRatio, useShortCovering } from '../hooks/useStockMetrics';
import { Card, CardContent } from '../components/ui/Card';
import { Badge } from '../components/ui/Badge';
import { LoadingOverlay } from '../components/ui/Spinner';
import { formatPercent, formatVolume, formatNumber } from '../lib/utils';
import type { StockMetric } from '../types';

type Tab = 'highMargin' | 'shortCovering';

export function MetricsPage() {
  const [activeTab, setActiveTab] = useState<Tab>('highMargin');

  const { data: highMarginData, isLoading: loadingHighMargin } = useHighMarginRatio(10, 20);
  const { data: shortCoveringData, isLoading: loadingShortCovering } = useShortCovering(20);

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-gray-900">市場指標</h1>
        <p className="text-gray-500 mt-1">追蹤高券資比與大量回補標的</p>
      </div>

      {/* 標籤頁 */}
      <div className="border-b border-gray-200">
        <nav className="flex gap-4">
          <button
            onClick={() => setActiveTab('highMargin')}
            className={`py-3 px-1 border-b-2 font-medium text-sm transition-colors ${
              activeTab === 'highMargin'
                ? 'border-blue-500 text-blue-600'
                : 'border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300'
            }`}
          >
            🔥 高券資比標的
          </button>
          <button
            onClick={() => setActiveTab('shortCovering')}
            className={`py-3 px-1 border-b-2 font-medium text-sm transition-colors ${
              activeTab === 'shortCovering'
                ? 'border-blue-500 text-blue-600'
                : 'border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300'
            }`}
          >
            📈 大量回補標的
          </button>
        </nav>
      </div>

      {/* 內容 */}
      {activeTab === 'highMargin' && (
        loadingHighMargin ? (
          <LoadingOverlay message="載入高券資比標的..." />
        ) : (
          <MetricsTable
            data={highMarginData || []}
            type="highMargin"
          />
        )
      )}

      {activeTab === 'shortCovering' && (
        loadingShortCovering ? (
          <LoadingOverlay message="載入回補標的..." />
        ) : (
          <MetricsTable
            data={shortCoveringData || []}
            type="shortCovering"
          />
        )
      )}
    </div>
  );
}

interface MetricsTableProps {
  data: StockMetric[];
  type: 'highMargin' | 'shortCovering';
}

function MetricsTable({ data, type }: MetricsTableProps) {
  if (data.length === 0) {
    return (
      <Card>
        <CardContent className="py-12 text-center text-gray-500">
          目前無符合條件的標的
        </CardContent>
      </Card>
    );
  }

  return (
    <Card>
      <div className="overflow-x-auto">
        <table className="w-full">
          <thead className="bg-gray-50 border-b border-gray-200">
            <tr>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                排名
              </th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                股票
              </th>
              <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase tracking-wider">
                收盤價
              </th>
              <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase tracking-wider">
                {type === 'highMargin' ? '券資比' : '借券變化'}
              </th>
              <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase tracking-wider">
                成交量
              </th>
              <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase tracking-wider">
                20日波動率
              </th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-200">
            {data.map((metric, index) => (
              <tr key={metric.ticker} className="hover:bg-gray-50 transition-colors">
                <td className="px-4 py-3 text-sm text-gray-400 font-medium">
                  #{index + 1}
                </td>
                <td className="px-4 py-3">
                  <div className="flex items-center gap-2">
                    <span className="font-semibold text-gray-900">{metric.ticker}</span>
                    {type === 'highMargin' && metric.marginRatio && metric.marginRatio >= 20 && (
                      <Badge variant="bullish">熱門</Badge>
                    )}
                  </div>
                </td>
                <td className="px-4 py-3 text-right text-sm text-gray-900 font-medium tabular-nums">
                  ${formatNumber(metric.closePrice, 2)}
                </td>
                <td className="px-4 py-3 text-right text-sm font-medium tabular-nums">
                  {type === 'highMargin' ? (
                    <span className="text-red-600">{formatPercent(metric.marginRatio)}</span>
                  ) : (
                    <span className={metric.borrowingBalanceChange && metric.borrowingBalanceChange < 0 ? 'text-green-600' : 'text-gray-900'}>
                      {metric.borrowingBalanceChange
                        ? `${metric.borrowingBalanceChange > 0 ? '+' : ''}${formatNumber(metric.borrowingBalanceChange, 0)}`
                        : '-'}
                    </span>
                  )}
                </td>
                <td className="px-4 py-3 text-right text-sm text-gray-600 tabular-nums">
                  {formatVolume(metric.volume)}
                </td>
                <td className="px-4 py-3 text-right text-sm text-gray-600 tabular-nums">
                  {formatPercent(metric.historicalVolatility20D)}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </Card>
  );
}
