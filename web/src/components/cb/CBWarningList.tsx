import { useState } from 'react';
import { useCBWarnings } from '../../hooks/useCBWarnings';
import { CBWarningCard } from './CBWarningCard';
import { Spinner } from '../ui/Spinner';
import type { CBWarningLevel } from '../../types';

interface CBWarningListProps {
  date?: string;
  onSelectCB?: (cbTicker: string) => void;
}

const levelOptions: { value: CBWarningLevel; label: string }[] = [
  { value: 'SAFE', label: '全部' },
  { value: 'CAUTION', label: '注意以上' },
  { value: 'WARNING', label: '警戒以上' },
  { value: 'CRITICAL', label: '僅已觸發' },
];

export function CBWarningList({ date, onSelectCB }: CBWarningListProps) {
  const [minLevel, setMinLevel] = useState<CBWarningLevel>('SAFE');
  const { data, isLoading, error } = useCBWarnings(date, minLevel);

  if (isLoading) {
    return (
      <div className="flex justify-center items-center py-12">
        <Spinner size="lg" />
        <span className="ml-2 text-gray-500">載入中...</span>
      </div>
    );
  }

  if (error) {
    return (
      <div className="bg-red-50 border border-red-200 rounded-lg p-4 text-red-600">
        載入失敗: {error instanceof Error ? error.message : '未知錯誤'}
      </div>
    );
  }

  return (
    <div className="space-y-4">
      {/* 篩選器與統計 */}
      <div className="flex flex-wrap gap-4 items-center justify-between bg-white p-4 rounded-lg shadow-sm">
        <div className="flex gap-2 items-center">
          <label className="text-sm text-gray-600">篩選等級:</label>
          <select
            value={minLevel}
            onChange={(e) => setMinLevel(e.target.value as CBWarningLevel)}
            className="border border-gray-300 rounded-md px-3 py-1 text-sm focus:ring-2 focus:ring-blue-500 focus:border-transparent"
          >
            {levelOptions.map((opt) => (
              <option key={opt.value} value={opt.value}>
                {opt.label}
              </option>
            ))}
          </select>
        </div>

        {/* 統計摘要 */}
        {data && (
          <div className="flex gap-4 text-sm">
            <span className="text-gray-500">
              共 <span className="font-semibold text-gray-700">{data.totalCount}</span> 檔 CB
            </span>
            {data.criticalCount > 0 && (
              <span className="text-red-600">
                已觸發: <span className="font-semibold">{data.criticalCount}</span>
              </span>
            )}
            {data.warningCount > 0 && (
              <span className="text-orange-600">
                警戒: <span className="font-semibold">{data.warningCount}</span>
              </span>
            )}
            {data.cautionCount > 0 && (
              <span className="text-yellow-600">
                注意: <span className="font-semibold">{data.cautionCount}</span>
              </span>
            )}
          </div>
        )}
      </div>

      {/* 分析日期 */}
      {data && (
        <div className="text-sm text-gray-500 text-right">
          分析日期: {data.analysisDate}
        </div>
      )}

      {/* CB 卡片列表 */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
        {data?.warnings.map((warning) => (
          <CBWarningCard
            key={warning.cbTicker}
            warning={warning}
            onClick={onSelectCB ? () => onSelectCB(warning.cbTicker) : undefined}
          />
        ))}
      </div>

      {/* 空狀態 */}
      {data?.warnings.length === 0 && (
        <div className="text-center py-12 text-gray-500">
          目前沒有符合條件的 CB 預警資料
        </div>
      )}
    </div>
  );
}
