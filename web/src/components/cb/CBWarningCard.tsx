import { CBBalanceBar } from '../charts/CBBalanceBar';
import type { CBWarningDto } from '../../types';

interface CBWarningCardProps {
  warning: CBWarningDto;
  onClick?: () => void;
}

export function CBWarningCard({ warning, onClick }: CBWarningCardProps) {
  return (
    <div
      className={`cursor-pointer hover:shadow-md transition-shadow ${onClick ? '' : 'cursor-default'}`}
      onClick={onClick}
    >
      <CBBalanceBar
        cbTicker={warning.cbTicker}
        underlyingTicker={warning.underlyingTicker}
        outstandingBalance={warning.outstandingBalance}
        initialBalance={warning.totalIssueAmount || warning.outstandingBalance * 1.5}
        daysAboveTrigger={warning.consecutiveDays}
        triggerDaysRequired={30}
        warningLevel={warning.warningLevel}
      />
      <div className="px-4 pb-4 bg-white rounded-b-lg border-t border-gray-100">
        <p className="text-sm text-gray-600 mt-2">{warning.comment}</p>
        <div className="flex justify-between items-center mt-2 text-xs text-gray-500">
          <span>轉換價: ${warning.conversionPrice.toFixed(2)}</span>
          <span>股價比: {warning.priceRatio.toFixed(1)}%</span>
        </div>
        {warning.cbName && (
          <div className="mt-1 text-xs text-gray-400">{warning.cbName}</div>
        )}
      </div>
    </div>
  );
}
