import { cn } from '../../lib/utils';

interface CardProps {
  children: React.ReactNode;
  className?: string;
  onClick?: () => void;
  variant?: 'default' | 'glass' | 'gradient';
  hover?: boolean;
}

export function Card({ children, className, onClick, variant = 'default', hover = false }: CardProps) {
  const variants = {
    default: 'bg-dark-800 border-dark-700/50',
    glass: 'bg-dark-800/60 backdrop-blur-xl border-dark-700/30',
    gradient: 'bg-gradient-to-br from-dark-800 to-dark-900 border-dark-700/50',
  };

  return (
    <div
      className={cn(
        'rounded-xl border',
        variants[variant],
        hover && 'transition-all duration-300 hover:shadow-card-hover hover:border-dark-600/50 hover:-translate-y-0.5',
        onClick && 'cursor-pointer',
        className
      )}
      onClick={onClick}
    >
      {children}
    </div>
  );
}

export function CardHeader({ children, className }: { children: React.ReactNode; className?: string }) {
  return (
    <div className={cn('px-5 py-4 border-b border-dark-700/50', className)}>
      {children}
    </div>
  );
}

export function CardTitle({ children, className }: { children: React.ReactNode; className?: string }) {
  return (
    <h3 className={cn('text-base font-semibold text-dark-100 flex items-center gap-2', className)}>
      {children}
    </h3>
  );
}

export function CardDescription({ children, className }: { children: React.ReactNode; className?: string }) {
  return (
    <p className={cn('text-sm text-dark-400 mt-1', className)}>
      {children}
    </p>
  );
}

export function CardContent({ children, className }: { children: React.ReactNode; className?: string }) {
  return (
    <div className={cn('p-5', className)}>
      {children}
    </div>
  );
}

export function CardFooter({ children, className }: { children: React.ReactNode; className?: string }) {
  return (
    <div className={cn('px-5 py-4 border-t border-dark-700/50 bg-dark-800/50 rounded-b-xl', className)}>
      {children}
    </div>
  );
}

// 統計卡片
interface StatCardProps {
  label: string;
  value: string | number;
  change?: number;
  icon?: React.ReactNode;
}

export function StatCard({ label, value, change, icon }: StatCardProps) {
  return (
    <Card variant="glass" className="p-5">
      <div className="flex items-start justify-between">
        <div>
          <p className="text-sm font-medium text-dark-400">{label}</p>
          <p className="text-2xl font-bold text-dark-100 mt-1 tabular-nums">{value}</p>
          {change !== undefined && (
            <p className={cn(
              'text-sm font-medium mt-1',
              change > 0 ? 'text-bullish-400' : change < 0 ? 'text-bearish-400' : 'text-dark-400'
            )}>
              {change > 0 ? '+' : ''}{change}%
            </p>
          )}
        </div>
        {icon && (
          <div className="p-2 rounded-lg bg-dark-700/50 text-dark-400">
            {icon}
          </div>
        )}
      </div>
    </Card>
  );
}
