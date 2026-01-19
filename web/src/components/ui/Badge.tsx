import { cn } from '../../lib/utils';

type BadgeVariant = 'bullish' | 'bearish' | 'neutral' | 'degraded' | 'default' | 'primary';

interface BadgeProps {
  children: React.ReactNode;
  variant?: BadgeVariant;
  size?: 'sm' | 'md';
  className?: string;
}

const variantStyles: Record<BadgeVariant, string> = {
  bullish: 'bg-bullish-500/10 text-bullish-400 border-bullish-500/20',
  bearish: 'bg-bearish-500/10 text-bearish-400 border-bearish-500/20',
  neutral: 'bg-dark-600/50 text-dark-300 border-dark-500/30',
  degraded: 'bg-accent-500/10 text-accent-400 border-accent-500/20',
  default: 'bg-dark-700/50 text-dark-300 border-dark-600/30',
  primary: 'bg-primary-500/10 text-primary-400 border-primary-500/20',
};

const sizeStyles = {
  sm: 'px-2 py-0.5 text-[10px]',
  md: 'px-2.5 py-1 text-xs',
};

export function Badge({ children, variant = 'default', size = 'md', className }: BadgeProps) {
  return (
    <span
      className={cn(
        'inline-flex items-center font-medium rounded-full border backdrop-blur-sm',
        variantStyles[variant],
        sizeStyles[size],
        className
      )}
    >
      {children}
    </span>
  );
}

/**
 * 根據趨勢類型取得對應的 variant
 */
export function getTrendVariant(trend: string): BadgeVariant {
  switch (trend) {
    case 'BULLISH':
      return 'bullish';
    case 'BEARISH':
      return 'bearish';
    case 'DEGRADED':
      return 'degraded';
    default:
      return 'neutral';
  }
}
