import type { FactorScores } from '../../types';
import { cn } from '../../lib/utils';

interface FactorBreakdownProps {
  factors: FactorScores;
  showWeighted?: boolean;
  compact?: boolean;
}

const factorConfig = {
  borrowScore: {
    label: '法人回補',
    shortLabel: '法人',
    weight: 0.35,
    gradient: 'from-blue-500 to-blue-400',
    bg: 'bg-blue-500/10',
    text: 'text-blue-400',
  },
  gammaScore: {
    label: 'Gamma壓縮',
    shortLabel: 'Gamma',
    weight: 0.25,
    gradient: 'from-purple-500 to-purple-400',
    bg: 'bg-purple-500/10',
    text: 'text-purple-400',
  },
  marginScore: {
    label: '空單擁擠',
    shortLabel: '空單',
    weight: 0.20,
    gradient: 'from-orange-500 to-orange-400',
    bg: 'bg-orange-500/10',
    text: 'text-orange-400',
  },
  momentumScore: {
    label: '價量動能',
    shortLabel: '動能',
    weight: 0.20,
    gradient: 'from-emerald-500 to-emerald-400',
    bg: 'bg-emerald-500/10',
    text: 'text-emerald-400',
  },
};

export function FactorBreakdown({ factors, showWeighted = true, compact = false }: FactorBreakdownProps) {
  const entries = Object.entries(factors) as [keyof FactorScores, number][];

  if (compact) {
    return (
      <div className="grid grid-cols-4 gap-2">
        {entries.map(([key, value]) => {
          const config = factorConfig[key];
          return (
            <div
              key={key}
              className={cn(
                'flex flex-col items-center justify-center py-2 px-1 rounded-lg',
                config.bg
              )}
            >
              <span className={cn('text-lg font-bold tabular-nums', config.text)}>
                {value.toFixed(0)}
              </span>
              <span className="text-[10px] text-dark-500 font-medium">
                {config.shortLabel}
              </span>
            </div>
          );
        })}
      </div>
    );
  }

  return (
    <div className="space-y-3">
      {entries.map(([key, value]) => {
        const config = factorConfig[key];
        const percentage = Math.min(100, Math.max(0, value));
        const weightedScore = value * config.weight;

        return (
          <div key={key} className="space-y-1.5">
            <div className="flex items-center justify-between">
              <span className="text-xs font-medium text-dark-400">
                {config.label}
              </span>
              <div className="flex items-center gap-2">
                <span className={cn('text-sm font-bold tabular-nums', config.text)}>
                  {value.toFixed(0)}
                </span>
                {showWeighted && (
                  <span className="text-xs text-dark-500 tabular-nums">
                    +{weightedScore.toFixed(1)}
                  </span>
                )}
              </div>
            </div>
            <div className="relative h-1.5 bg-dark-700 rounded-full overflow-hidden">
              <div
                className={cn(
                  'absolute inset-y-0 left-0 rounded-full bg-gradient-to-r transition-all duration-500 ease-out',
                  config.gradient
                )}
                style={{ width: `${percentage}%` }}
              />
            </div>
          </div>
        );
      })}
    </div>
  );
}

/**
 * 簡化版因子顯示 (單行)
 */
export function FactorSummary({ factors }: { factors: FactorScores }) {
  return (
    <div className="flex gap-3 text-xs">
      <span className="text-blue-400">借:{factors.borrowScore.toFixed(0)}</span>
      <span className="text-purple-400">G:{factors.gammaScore.toFixed(0)}</span>
      <span className="text-orange-400">空:{factors.marginScore.toFixed(0)}</span>
      <span className="text-emerald-400">量:{factors.momentumScore.toFixed(0)}</span>
    </div>
  );
}
