import {
  ComposedChart,
  Line,
  Bar,
  XAxis,
  YAxis,
  Tooltip,
  ResponsiveContainer,
  CartesianGrid,
} from 'recharts';
import { format, parseISO } from 'date-fns';
import type { StockMetric } from '../../types';
import { formatVolume } from '../../lib/utils';

interface PriceChartProps {
  metrics: StockMetric[];
}

export function PriceChart({ metrics }: PriceChartProps) {
  const chartData = metrics
    .map((m) => ({
      date: m.tradeDate,
      close: m.closePrice,
      high: m.highPrice,
      low: m.lowPrice,
      open: m.openPrice,
      volume: m.volume,
    }))
    .sort((a, b) => new Date(a.date).getTime() - new Date(b.date).getTime());

  if (chartData.length === 0) {
    return (
      <div className="flex items-center justify-center h-64 text-dark-500">
        <div className="text-center">
          <svg className="w-12 h-12 mx-auto mb-2 text-dark-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M7 12l3-3 3 3 4-4M8 21l4-4 4 4M3 4h18M4 4h16v12a1 1 0 01-1 1H5a1 1 0 01-1-1V4z" />
          </svg>
          <p className="text-sm">無歷史資料</p>
        </div>
      </div>
    );
  }

  return (
    <div data-testid="price-chart" className="w-full h-64">
      <ResponsiveContainer>
        <ComposedChart
          data={chartData}
          margin={{ top: 5, right: 10, left: 0, bottom: 5 }}
        >
          <CartesianGrid strokeDasharray="3 3" stroke="#334155" vertical={false} />
          <XAxis
            dataKey="date"
            tickFormatter={(date) => {
              try {
                return format(parseISO(date), 'MM/dd');
              } catch {
                return date;
              }
            }}
            stroke="#64748b"
            fontSize={11}
            tickLine={false}
            axisLine={{ stroke: '#334155' }}
          />
          <YAxis
            yAxisId="price"
            domain={['auto', 'auto']}
            stroke="#64748b"
            fontSize={11}
            tickLine={false}
            axisLine={false}
            tickFormatter={(v) => v.toFixed(0)}
            width={50}
          />
          <YAxis
            yAxisId="volume"
            orientation="right"
            stroke="#64748b"
            fontSize={11}
            tickLine={false}
            axisLine={false}
            tickFormatter={(v) => formatVolume(v)}
            width={50}
          />
          <Tooltip
            formatter={(value, name) => {
              if (value == null) return ['-', name ?? ''];
              const numValue = Number(value);
              if (name === '成交量') return [formatVolume(numValue), name ?? ''];
              return [`$${numValue.toFixed(2)}`, name ?? ''];
            }}
            labelFormatter={(date) => {
              try {
                return format(parseISO(date as string), 'yyyy/MM/dd');
              } catch {
                return date;
              }
            }}
            contentStyle={{
              backgroundColor: '#1e293b',
              border: '1px solid #334155',
              borderRadius: '8px',
              boxShadow: '0 4px 20px rgba(0,0,0,0.3)',
            }}
            labelStyle={{ color: '#94a3b8', marginBottom: '4px' }}
            itemStyle={{ color: '#e2e8f0' }}
          />
          <Bar
            yAxisId="volume"
            dataKey="volume"
            name="成交量"
            fill="#334155"
            opacity={0.6}
            radius={[2, 2, 0, 0]}
          />
          <Line
            yAxisId="price"
            type="monotone"
            dataKey="close"
            name="收盤價"
            stroke="#22c55e"
            strokeWidth={2}
            dot={false}
            connectNulls
            activeDot={{ r: 4, fill: '#22c55e', stroke: '#1e293b', strokeWidth: 2 }}
          />
        </ComposedChart>
      </ResponsiveContainer>
    </div>
  );
}
