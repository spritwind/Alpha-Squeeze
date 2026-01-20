import { BrowserRouter, Routes, Route } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { Layout } from './components/layout/Layout';
import { Dashboard } from './pages/Dashboard';
import { StockDetail } from './pages/StockDetail';
import { MetricsPage } from './pages/MetricsPage';
import { SettingsPage } from './pages/SettingsPage';
import { CBDashboardPage } from './pages/CBDashboardPage';
import { MonitoringPage } from './pages/MonitoringPage';
import { TickersPage } from './pages/TickersPage';
import { DiscoveryPage } from './pages/DiscoveryPage';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: 1,
      refetchOnWindowFocus: false,
    },
  },
});

function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <Routes>
          <Route element={<Layout />}>
            <Route index element={<Dashboard />} />
            <Route path="/stock/:ticker" element={<StockDetail />} />
            <Route path="/metrics" element={<MetricsPage />} />
            <Route path="/cb" element={<CBDashboardPage />} />
            <Route path="/discovery" element={<DiscoveryPage />} />
            <Route path="/monitoring" element={<MonitoringPage />} />
            <Route path="/tickers" element={<TickersPage />} />
            <Route path="/settings" element={<SettingsPage />} />
          </Route>
        </Routes>
      </BrowserRouter>
    </QueryClientProvider>
  );
}

export default App;
