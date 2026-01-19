import { cn } from '../../lib/utils';

interface ScoreGaugeProps {
  score: number;
  size?: 'sm' | 'md' | 'lg';
  showLabel?: boolean;
}

const sizeStyles = {
  sm: { container: 'w-14 h-14', text: 'text-lg', label: 'text-[10px]' },
  md: { container: 'w-20 h-20', text: 'text-2xl', label: 'text-xs' },
  lg: { container: 'w-24 h-24', text: 'text-3xl', label: 'text-xs' },
};

export function ScoreGauge({ score, size = 'md', showLabel = true }: ScoreGaugeProps) {
  const styles = sizeStyles[size];
  const percentage = Math.min(100, Math.max(0, score));
  const circumference = 2 * Math.PI * 40; // r=40
  const strokeDashoffset = circumference - (percentage / 100) * circumference;

  const getColorClass = (s: number) => {
    if (s >= 70) return { stroke: '#f87171', text: 'text-bullish-400', glow: 'rgba(248, 113, 113, 0.3)' };
    if (s <= 40) return { stroke: '#4ade80', text: 'text-bearish-400', glow: 'rgba(74, 222, 128, 0.3)' };
    return { stroke: '#94a3b8', text: 'text-dark-400', glow: 'rgba(148, 163, 184, 0.2)' };
  };

  const colors = getColorClass(score);

  return (
    <div data-testid="score-gauge" className={cn('relative', styles.container)}>
      {/* Glow effect */}
      <div
        className="absolute inset-0 rounded-full blur-xl opacity-50"
        style={{ backgroundColor: colors.glow }}
      />

      <svg className="w-full h-full transform -rotate-90 relative" viewBox="0 0 100 100">
        {/* Background circle */}
        <circle
          cx="50"
          cy="50"
          r="40"
          stroke="currentColor"
          strokeWidth="6"
          fill="none"
          className="text-dark-700"
        />
        {/* Progress circle */}
        <circle
          cx="50"
          cy="50"
          r="40"
          stroke={colors.stroke}
          strokeWidth="6"
          fill="none"
          strokeLinecap="round"
          strokeDasharray={circumference}
          strokeDashoffset={strokeDashoffset}
          className="transition-all duration-700 ease-out"
          style={{
            filter: `drop-shadow(0 0 6px ${colors.stroke}40)`,
          }}
        />
      </svg>

      <div className="absolute inset-0 flex flex-col items-center justify-center">
        <span className={cn('font-bold tabular-nums leading-none', styles.text, colors.text)}>
          {score}
        </span>
        {showLabel && (
          <span className={cn('text-dark-500 font-medium mt-0.5', styles.label)}>SCORE</span>
        )}
      </div>
    </div>
  );
}
