import { useMemo } from 'react';
import type { CBWarningLevel } from '../../types';

interface CBBalanceBarProps {
  cbTicker: string;
  underlyingTicker: string;
  outstandingBalance: number;    // 剩餘餘額 (億)
  initialBalance: number;        // 初始餘額 (億)
  daysAboveTrigger: number;      // 連續觸發天數
  triggerDaysRequired?: number;  // 觸發所需天數 (預設 30)
  warningLevel: CBWarningLevel;
}

const warningStyles = {
  SAFE: {
    bar: 'bg-green-500',
    text: 'text-green-600',
    bg: 'bg-green-50',
    border: 'border-green-500',
    label: '安全',
  },
  CAUTION: {
    bar: 'bg-yellow-500',
    text: 'text-yellow-600',
    bg: 'bg-yellow-50',
    border: 'border-yellow-500',
    label: '注意追蹤',
  },
  WARNING: {
    bar: 'bg-orange-500',
    text: 'text-orange-600',
    bg: 'bg-orange-50',
    border: 'border-orange-500',
    label: '高度警戒',
  },
  CRITICAL: {
    bar: 'bg-red-500',
    text: 'text-red-600',
    bg: 'bg-red-50',
    border: 'border-red-500',
    label: '已觸發',
  },
};

export function CBBalanceBar({
  cbTicker,
  underlyingTicker,
  outstandingBalance,
  initialBalance,
  daysAboveTrigger,
  triggerDaysRequired = 30,
  warningLevel,
}: CBBalanceBarProps) {
  // 計算餘額百分比
  const balancePercent = useMemo(() => {
    if (initialBalance <= 0) return 0;
    return Math.min(100, (outstandingBalance / initialBalance) * 100);
  }, [outstandingBalance, initialBalance]);

  // 計算觸發進度百分比
  const triggerPercent = useMemo(() => {
    return Math.min(100, (daysAboveTrigger / triggerDaysRequired) * 100);
  }, [daysAboveTrigger, triggerDaysRequired]);

  const styles = warningStyles[warningLevel];

  // 觸發進度條顏色
  const getTriggerBarColor = () => {
    if (triggerPercent >= 100) return 'bg-red-600';
    if (triggerPercent >= 66) return 'bg-orange-500';
    if (triggerPercent >= 33) return 'bg-yellow-500';
    return 'bg-green-500';
  };

  return (
    <div className={`p-4 rounded-lg border-l-4 bg-white shadow-sm ${styles.border}`}>
      {/* Header */}
      <div className="flex justify-between items-start mb-3">
        <div>
          <h4 className="font-semibold text-gray-900">{cbTicker}</h4>
          <span className="text-sm text-gray-500">標的: {underlyingTicker}</span>
        </div>
        <span className={`px-2 py-1 rounded text-xs font-medium ${styles.text} ${styles.bg}`}>
          {styles.label}
        </span>
      </div>

      {/* 餘額橫條圖 */}
      <div className="mb-3">
        <div className="flex justify-between text-sm mb-1">
          <span className="text-gray-600">剩餘餘額</span>
          <span className="font-medium">
            {outstandingBalance.toFixed(2)} 億 / {initialBalance.toFixed(2)} 億
          </span>
        </div>
        <div className="w-full bg-gray-200 rounded-full h-3">
          <div
            className={`h-3 rounded-full transition-all duration-500 ${styles.bar}`}
            style={{ width: `${balancePercent}%` }}
          />
        </div>
        <div className="text-right text-xs text-gray-500 mt-1">
          {balancePercent.toFixed(1)}% 尚未轉換
        </div>
      </div>

      {/* 觸發進度條 */}
      <div>
        <div className="flex justify-between text-sm mb-1">
          <span className="text-gray-600">強贖觸發進度</span>
          <span className="font-medium">
            {daysAboveTrigger} / {triggerDaysRequired} 天
          </span>
        </div>
        <div className="w-full bg-gray-200 rounded-full h-2">
          <div
            className={`h-2 rounded-full transition-all duration-500 ${getTriggerBarColor()} ${
              triggerPercent >= 100 ? 'animate-pulse' : ''
            }`}
            style={{ width: `${triggerPercent}%` }}
          />
        </div>
        {warningLevel === 'CRITICAL' && (
          <div className="mt-2 text-xs text-red-600 font-medium animate-pulse">
            已達強制贖回門檻
          </div>
        )}
      </div>
    </div>
  );
}
