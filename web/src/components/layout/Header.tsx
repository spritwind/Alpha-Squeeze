import { Link, useLocation } from 'react-router-dom';
import { useHealthStatus } from '../../hooks/useSqueezeSignals';
import { cn } from '../../lib/utils';

export function Header() {
  const { data: health } = useHealthStatus();
  const location = useLocation();

  const isActive = (path: string) => location.pathname === path;

  return (
    <header className="sticky top-0 z-50 border-b border-dark-700/50 bg-dark-900/80 backdrop-blur-xl">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div className="flex justify-between items-center h-16">
          {/* Logo & Nav */}
          <div className="flex items-center gap-8">
            <Link to="/" className="flex items-center gap-3 group">
              <div className="relative">
                <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-primary-500 to-primary-700 flex items-center justify-center shadow-lg shadow-primary-500/25 group-hover:shadow-primary-500/40 transition-shadow">
                  <svg className="w-6 h-6 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 7h8m0 0v8m0-8l-8 8-4-4-6 6" />
                  </svg>
                </div>
                <div className="absolute -inset-1 bg-primary-500/20 rounded-xl blur-lg opacity-0 group-hover:opacity-100 transition-opacity" />
              </div>
              <div>
                <span className="text-lg font-bold text-white tracking-tight">Alpha Squeeze</span>
                <span className="hidden sm:block text-[10px] text-dark-400 font-medium tracking-wider uppercase">
                  Quantitative Platform
                </span>
              </div>
            </Link>

            <nav className="hidden md:flex items-center gap-1">
              <Link
                to="/"
                className={cn('nav-link', isActive('/') && 'active')}
              >
                <span className="flex items-center gap-2">
                  <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z" />
                  </svg>
                  Dashboard
                </span>
              </Link>
              <Link
                to="/metrics"
                className={cn('nav-link', isActive('/metrics') && 'active')}
              >
                <span className="flex items-center gap-2">
                  <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 17v-2m3 2v-4m3 4v-6m2 10H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
                  </svg>
                  市場指標
                </span>
              </Link>
            </nav>
          </div>

          {/* Status Indicator */}
          <div className="flex items-center gap-4">
            <div className="flex items-center gap-3 px-3 py-1.5 rounded-full bg-dark-800/50 border border-dark-700/50">
              <div className="relative flex items-center justify-center">
                <div
                  className={cn(
                    'w-2 h-2 rounded-full',
                    health?.status === 'Healthy'
                      ? 'bg-bearish-400'
                      : health?.status === 'Degraded'
                        ? 'bg-accent-400'
                        : 'bg-dark-500'
                  )}
                />
                {health?.status === 'Healthy' && (
                  <div className="absolute w-2 h-2 rounded-full bg-bearish-400 animate-ping" />
                )}
              </div>
              <span className="text-xs font-medium text-dark-300">
                {health?.status === 'Healthy'
                  ? '引擎正常'
                  : health?.status === 'Degraded'
                    ? '降級模式'
                    : '檢查中...'}
              </span>
            </div>

            {/* Mobile Menu Button */}
            <button className="md:hidden p-2 rounded-lg text-dark-400 hover:text-white hover:bg-dark-800 transition-colors">
              <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 6h16M4 12h16M4 18h16" />
              </svg>
            </button>
          </div>
        </div>
      </div>
    </header>
  );
}
