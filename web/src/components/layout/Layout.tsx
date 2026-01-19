import { Outlet } from 'react-router-dom';
import { Header } from './Header';

export function Layout() {
  return (
    <div className="min-h-screen bg-dark-900">
      {/* Background Pattern */}
      <div className="fixed inset-0 bg-[radial-gradient(ellipse_80%_80%_at_50%_-20%,rgba(51,129,255,0.15),transparent)] pointer-events-none" />
      <div className="fixed inset-0 bg-[radial-gradient(circle_at_80%_20%,rgba(251,191,36,0.05),transparent)] pointer-events-none" />

      <div className="relative">
        <Header />

        <main className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
          <Outlet />
        </main>

        <footer className="border-t border-dark-800 mt-16">
          <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
            <div className="flex flex-col md:flex-row justify-between items-center gap-4">
              <div className="flex items-center gap-3">
                <div className="w-8 h-8 rounded-lg bg-gradient-to-br from-primary-500 to-primary-700 flex items-center justify-center">
                  <svg className="w-5 h-5 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 7h8m0 0v8m0-8l-8 8-4-4-6 6" />
                  </svg>
                </div>
                <div>
                  <span className="text-sm font-semibold text-dark-200">Alpha Squeeze</span>
                  <p className="text-xs text-dark-500">戰術級量化決策支援平台</p>
                </div>
              </div>
              <div className="flex items-center gap-6 text-xs text-dark-500">
                <span>Built with React + TypeScript</span>
                <span className="hidden sm:inline">•</span>
                <span className="hidden sm:inline">Data powered by FinMind</span>
              </div>
            </div>
          </div>
        </footer>
      </div>
    </div>
  );
}
