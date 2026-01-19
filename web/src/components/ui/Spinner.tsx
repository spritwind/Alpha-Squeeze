import { cn } from '../../lib/utils';

interface SpinnerProps {
  size?: 'sm' | 'md' | 'lg';
  className?: string;
}

const sizeStyles = {
  sm: 'h-4 w-4 border-2',
  md: 'h-8 w-8 border-2',
  lg: 'h-12 w-12 border-3',
};

export function Spinner({ size = 'md', className }: SpinnerProps) {
  return (
    <div
      className={cn(
        'rounded-full animate-spin',
        'border-dark-600 border-t-primary-500',
        sizeStyles[size],
        className
      )}
    />
  );
}

export function LoadingOverlay({ message = '載入中...' }: { message?: string }) {
  return (
    <div className="flex flex-col items-center justify-center py-12">
      <div className="relative">
        <Spinner size="lg" />
        <div className="absolute inset-0 rounded-full bg-primary-500/20 blur-lg animate-pulse" />
      </div>
      <p className="mt-4 text-sm text-dark-400">{message}</p>
    </div>
  );
}

export function LoadingSkeleton({ className }: { className?: string }) {
  return (
    <div className={cn('skeleton animate-pulse', className)} />
  );
}
