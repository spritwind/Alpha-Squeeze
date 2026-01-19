import type { SqueezeSignal } from '../../types';
import { SqueezeCard } from './SqueezeCard';
import { LoadingOverlay } from '../ui/Spinner';

interface SqueezeListProps {
  candidates: SqueezeSignal[];
  isLoading?: boolean;
  selectedTicker?: string | null;
  onSelect?: (ticker: string) => void;
}

export function SqueezeList({
  candidates,
  isLoading,
  selectedTicker,
  onSelect,
}: SqueezeListProps) {
  if (isLoading) {
    return <LoadingOverlay message="載入軋空排行中..." />;
  }

  if (candidates.length === 0) {
    return (
      <div className="text-center py-12">
        <div className="w-16 h-16 mx-auto mb-4 rounded-full bg-dark-700/50 flex items-center justify-center">
          <svg className="w-8 h-8 text-dark-500" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M20 13V6a2 2 0 00-2-2H6a2 2 0 00-2 2v7m16 0v5a2 2 0 01-2 2H6a2 2 0 01-2-2v-5m16 0h-2.586a1 1 0 00-.707.293l-2.414 2.414a1 1 0 01-.707.293h-3.172a1 1 0 01-.707-.293l-2.414-2.414A1 1 0 006.586 13H4" />
          </svg>
        </div>
        <p className="text-dark-300 font-medium">目前無符合條件的軋空候選</p>
        <p className="text-sm text-dark-500 mt-1">請稍後再試或調整篩選條件</p>
      </div>
    );
  }

  return (
    <div className="space-y-3">
      {candidates.map((signal, index) => (
        <SqueezeCard
          key={signal.ticker}
          signal={signal}
          rank={index + 1}
          isSelected={selectedTicker === signal.ticker}
          onClick={() => onSelect?.(signal.ticker)}
        />
      ))}
    </div>
  );
}
