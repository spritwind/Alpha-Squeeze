import {
  LineChart,
  Line,
  XAxis,
  YAxis,
  Tooltip,
  Legend,
  ResponsiveContainer,
  CartesianGrid,
} from 'recharts';
import { format, parseISO } from 'date-fns';
import type { StockMetric } from '../../types';

interface IVHVChartProps {
  metrics: StockMetric[];
  ivData?: { date: string; iv: number }[];
}

export function IVHVChart({ metrics, ivData = [] }: IVHVChartProps) {
  // 合併 HV 與 IV 資料
  const chartData = metrics.map((m) => {
    const iv = ivData.find((i) => i.date === m.tradeDate)?.iv;
    return {
      date: m.tradeDate,
      hv: m.historicalVolatility20D != null ? m.historicalVolatility20D * 100 : null,
      iv: iv != null ? iv * 100 : null,
      close: m.closePrice,
    };
  }).sort((a, b) => new Date(a.date).getTime() - new Date(b.date).getTime());

  if (chartData.length === 0) {
    return (
      <div className="flex items-center justify-center h-80 text-dark-500">
        <div className="text-center">
          <svg className="w-12 h-12 mx-auto mb-2 text-dark-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z" />
          </svg>
          <p className="text-sm">無歷史資料</p>
        </div>
      </div>
    );
  }

  return (
    <div data-testid="ivhv-chart" className="w-full h-80">
      <ResponsiveContainer>
        <LineChart
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
            yAxisId="volatility"
            tickFormatter={(value) => `${value.toFixed(0)}%`}
            domain={['auto', 'auto']}
            stroke="#64748b"
            fontSize={11}
            tickLine={false}
            axisLine={false}
            width={45}
          />
          <YAxis
            yAxisId="price"
            orientation="right"
            domain={['auto', 'auto']}
            stroke="#64748b"
            fontSize={11}
            hide
          />
          <Tooltip
            formatter={(value, name) => {
              if (value == null) return ['-', name ?? ''];
              const numValue = Number(value);
              if (name === '收盤價') return [`$${numValue.toFixed(2)}`, name ?? ''];
              return [`${numValue.toFixed(2)}%`, name ?? ''];
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
          <Legend
            wrapperStyle={{ paddingTop: '10px' }}
            formatter={(value) => <span style={{ color: '#94a3b8', fontSize: '12px' }}>{value}</span>}
          />
          <Line
            yAxisId="volatility"
            type="monotone"
            dataKey="hv"
            name="HV (20日歷史波動率)"
            stroke="#3b82f6"
            strokeWidth={2}
            dot={false}
            connectNulls
            activeDot={{ r: 4, fill: '#3b82f6', stroke: '#1e293b', strokeWidth: 2 }}
          />
          <Line
            yAxisId="volatility"
            type="monotone"
            dataKey="iv"
            name="IV (隱含波動率)"
            stroke="#f97316"
            strokeWidth={2}
            dot={false}
            connectNulls
            activeDot={{ r: 4, fill: '#f97316', stroke: '#1e293b', strokeWidth: 2 }}
          />
        </LineChart>
      </ResponsiveContainer>
    </div>
  );
}
