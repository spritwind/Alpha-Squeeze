import { cn } from '../../lib/utils';
import type { SqueezeSignal } from '../../types';
import { FactorBreakdown } from './FactorBreakdown';
import { Badge, getTrendVariant } from '../ui/Badge';

interface SqueezeCardProps {
  signal: SqueezeSignal;
  rank: number;
  isSelected?: boolean;
  onClick?: () => void;
}

const trendAccentColors = {
  BULLISH: 'from-bullish-500/20 to-transparent',
  BEARISH: 'from-bearish-500/20 to-transparent',
  NEUTRAL: 'from-dark-600/20 to-transparent',
  DEGRADED: 'from-accent-500/20 to-transparent',
};

const trendBorderColors = {
  BULLISH: 'border-l-bullish-500',
  BEARISH: 'border-l-bearish-500',
  NEUTRAL: 'border-l-dark-500',
  DEGRADED: 'border-l-accent-500',
};

export function SqueezeCard({ signal, rank, isSelected, onClick }: SqueezeCardProps) {
  const scoreColor =
    signal.score >= 70
      ? 'text-bullish-400'
      : signal.score <= 40
        ? 'text-bearish-400'
        : 'text-dark-300';

  const scoreBg =
    signal.score >= 70
      ? 'from-bullish-500/20 to-bullish-500/5'
      : signal.score <= 40
        ? 'from-bearish-500/20 to-bearish-500/5'
        : 'from-dark-600/20 to-dark-600/5';

  const trendLabel = signal.trend === 'DEGRADED' ? '降級模式' : signal.trend;

  return (
    <div
      data-testid="squeeze-card"
      className={cn(
        'relative group rounded-xl border-l-4 overflow-hidden',
        'bg-dark-800/60 backdrop-blur-sm border border-dark-700/50',
        'transition-all duration-300 cursor-pointer',
        trendBorderColors[signal.trend],
        isSelected
          ? 'ring-2 ring-primary-500/50 shadow-lg shadow-primary-500/10 border-dark-600/50'
          : 'hover:bg-dark-800/80 hover:border-dark-600/50 hover:shadow-lg hover:-translate-y-0.5'
      )}
      onClick={onClick}
    >
      {/* Gradient accent overlay */}
      <div className={cn(
        'absolute inset-0 bg-gradient-to-r opacity-50',
        trendAccentColors[signal.trend]
      )} />

      <div className="relative p-4">
        {/* Header */}
        <div className="flex items-start justify-between mb-3">
          <div className="flex items-center gap-3">
            {/* Rank Badge */}
            <div className="flex items-center justify-center w-8 h-8 rounded-lg bg-dark-700/80 border border-dark-600/50">
              <span className="text-sm font-bold text-dark-400">#{rank}</span>
            </div>

            <div>
              <h3 className="text-lg font-bold text-dark-100 tracking-tight">{signal.ticker}</h3>
              <Badge variant={getTrendVariant(signal.trend)} size="sm">
                {trendLabel}
              </Badge>
            </div>
          </div>

          {/* Score */}
          <div className={cn(
            'flex flex-col items-center justify-center px-3 py-2 rounded-xl',
            'bg-gradient-to-br',
            scoreBg
          )}>
            <span className={cn('text-2xl font-bold tabular-nums leading-none', scoreColor)}>
              {signal.score}
            </span>
            <span className="text-[10px] text-dark-500 font-medium mt-0.5">SCORE</span>
          </div>
        </div>

        {/* Comment */}
        <p className="text-sm text-dark-400 mb-4 line-clamp-2 leading-relaxed">
          {signal.comment}
        </p>

        {/* Factors */}
        {signal.factors && (
          <FactorBreakdown factors={signal.factors} showWeighted={false} compact />
        )}
      </div>

      {/* Selected indicator */}
      {isSelected && (
        <div className="absolute top-0 right-0 w-0 h-0 border-t-[24px] border-l-[24px] border-t-primary-500 border-l-transparent" />
      )}
    </div>
  );
}

/**
 * 緊湊版卡片
 */
export function SqueezeCardCompact({ signal, rank, onClick }: Omit<SqueezeCardProps, 'isSelected'>) {
  const scoreColor =
    signal.score >= 70
      ? 'text-bullish-400'
      : signal.score <= 40
        ? 'text-bearish-400'
        : 'text-dark-400';

  return (
    <div
      className={cn(
        'flex items-center justify-between p-3 rounded-lg',
        'bg-dark-800/40 border border-dark-700/30',
        'hover:bg-dark-800/60 hover:border-dark-600/50 transition-all cursor-pointer'
      )}
      onClick={onClick}
    >
      <div className="flex items-center gap-3">
        <span className="text-sm font-bold text-dark-500 w-6">#{rank}</span>
        <div className="flex items-center gap-2">
          <span className="font-semibold text-dark-100">{signal.ticker}</span>
          <Badge variant={getTrendVariant(signal.trend)} size="sm">
            {signal.trend === 'DEGRADED' ? '降級' : signal.trend}
          </Badge>
        </div>
      </div>
      <span className={cn('text-xl font-bold tabular-nums', scoreColor)}>
        {signal.score}
      </span>
    </div>
  );
}
