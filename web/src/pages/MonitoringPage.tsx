import { useState, useEffect } from 'react';
import { Link } from 'react-router-dom';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { monitoringApi, type DataSourceStatus, type SystemLogEntry } from '../services/api';
import { Card, CardHeader, CardTitle, CardContent } from '../components/ui/Card';
import { Badge } from '../components/ui/Badge';
import { cn, formatDate } from '../lib/utils';

const STATUS_COLORS = {
  OK: 'text-bullish-400 bg-bullish-500/10',
  WARNING: 'text-accent-400 bg-accent-500/10',
  ERROR: 'text-bearish-400 bg-bearish-500/10',
  EMPTY: 'text-accent-400 bg-accent-500/10',
  NOT_EXISTS: 'text-dark-400 bg-dark-500/10',
};

const LOG_LEVEL_COLORS = {
  INFO: 'text-primary-400',
  WARNING: 'text-accent-400',
  ERROR: 'text-bearish-400',
  DEBUG: 'text-dark-400',
};

function DataSourceCard({ source }: { source: DataSourceStatus }) {
  const statusColor = STATUS_COLORS[source.status] || STATUS_COLORS.ERROR;

  return (
    <div className="p-4 rounded-lg bg-dark-800/50 border border-dark-700/50 hover:border-dark-600/50 transition-colors">
      <div className="flex items-start justify-between mb-3">
        <div>
          <h4 className="font-medium text-dark-100">{source.name}</h4>
          <p className="text-xs text-dark-500 font-mono">{source.tableName}</p>
        </div>
        <span className={cn('px-2 py-0.5 rounded text-xs font-medium', statusColor)}>
          {source.status}
        </span>
      </div>

      <div className="grid grid-cols-2 gap-3 text-sm">
        <div>
          <p className="text-dark-500 text-xs mb-1">總筆數</p>
          <p className="text-dark-100 font-semibold tabular-nums">
            {source.totalRecords.toLocaleString()}
          </p>
        </div>
        <div>
          <p className="text-dark-500 text-xs mb-1">最後更新</p>
          <p className="text-dark-200 text-xs">
            {source.lastUpdate ? formatDate(source.lastUpdate, 'short') : '-'}
          </p>
        </div>
      </div>

      {source.additionalInfo && Object.keys(source.additionalInfo).length > 0 && (
        <div className="mt-3 pt-3 border-t border-dark-700/50">
          <div className="flex flex-wrap gap-2">
            {Object.entries(source.additionalInfo).map(([key, value]) => (
              <span key={key} className="text-xs text-dark-400">
                {key}: <span className="text-dark-200">{String(value)}</span>
              </span>
            ))}
          </div>
        </div>
      )}

      {source.errorMessage && (
        <div className="mt-3 p-2 rounded bg-bearish-500/10 text-xs text-bearish-400">
          {source.errorMessage}
        </div>
      )}
    </div>
  );
}

function LogEntry({ log }: { log: SystemLogEntry }) {
  const levelColor = LOG_LEVEL_COLORS[log.level as keyof typeof LOG_LEVEL_COLORS] || 'text-dark-400';

  return (
    <div className="flex items-start gap-3 py-2 border-b border-dark-700/30 last:border-0 font-mono text-xs">
      <span className="text-dark-500 whitespace-nowrap">
        {new Date(log.timestamp).toLocaleTimeString('zh-TW')}
      </span>
      <span className={cn('w-12 text-center', levelColor)}>[{log.level}]</span>
      <span className="text-dark-400 w-20 truncate">{log.source}</span>
      <span className="text-dark-200 flex-1">{log.message}</span>
    </div>
  );
}

export function MonitoringPage() {
  const queryClient = useQueryClient();
  const [autoRefresh, setAutoRefresh] = useState(true);
  const [logLevel, setLogLevel] = useState<string>('');

  const { data: status, isLoading: statusLoading, error: statusError, refetch: refetchStatus } = useQuery({
    queryKey: ['monitoring-status'],
    queryFn: () => monitoringApi.getStatus(),
    refetchInterval: autoRefresh ? 10000 : false,
  });

  const { data: logsData, isLoading: logsLoading, refetch: refetchLogs } = useQuery({
    queryKey: ['monitoring-logs', logLevel],
    queryFn: () => monitoringApi.getLogs(100, logLevel || undefined),
    refetchInterval: autoRefresh ? 5000 : false,
  });

  const clearLogsMutation = useMutation({
    mutationFn: () => monitoringApi.clearLogs(),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['monitoring-logs'] });
    },
  });

  const addLogMutation = useMutation({
    mutationFn: (message: string) => monitoringApi.addLog(message, 'WebUI', 'INFO'),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['monitoring-logs'] });
    },
  });

  // 頁面載入時記錄
  useEffect(() => {
    addLogMutation.mutate('監控頁面已開啟');
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const overallStatusColor = status?.overallStatus
    ? STATUS_COLORS[status.overallStatus]
    : STATUS_COLORS.OK;

  if (statusError) {
    return (
      <div className="flex items-center justify-center min-h-[400px]">
        <Card variant="glass" className="max-w-md w-full">
          <CardContent className="py-8 text-center">
            <div className="w-16 h-16 mx-auto mb-4 rounded-full bg-bearish-500/10 flex items-center justify-center">
              <svg className="w-8 h-8 text-bearish-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
              </svg>
            </div>
            <h3 className="text-lg font-semibold text-dark-100 mb-2">連線失敗</h3>
            <p className="text-sm text-dark-400">{(statusError as Error).message}</p>
          </CardContent>
        </Card>
      </div>
    );
  }

  return (
    <div className="space-y-6 animate-fade-in">
      {/* Header */}
      <div className="relative overflow-hidden rounded-2xl bg-gradient-to-br from-dark-800 via-dark-800 to-dark-900 border border-dark-700/50 p-6">
        <div className="absolute inset-0 bg-[radial-gradient(ellipse_at_top_right,rgba(51,129,255,0.1),transparent_50%)]" />

        <div className="relative flex flex-col md:flex-row md:items-center justify-between gap-4">
          <div className="flex items-center gap-4">
            <div className="w-12 h-12 rounded-xl bg-primary-500/20 flex items-center justify-center">
              <svg className="w-6 h-6 text-primary-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z" />
              </svg>
            </div>
            <div>
              <h1 className="text-2xl font-bold text-dark-50">系統監控</h1>
              <p className="text-dark-400 text-sm">資料狀態與系統日誌</p>
            </div>
          </div>

          <div className="flex items-center gap-4">
            <div className="flex items-center gap-2">
              <span className="text-dark-400 text-sm">自動更新</span>
              <button
                onClick={() => setAutoRefresh(!autoRefresh)}
                className={cn(
                  'relative w-10 h-5 rounded-full transition-colors',
                  autoRefresh ? 'bg-primary-500' : 'bg-dark-600'
                )}
              >
                <span
                  className={cn(
                    'absolute top-0.5 left-0.5 w-4 h-4 rounded-full bg-white transition-transform',
                    autoRefresh && 'translate-x-5'
                  )}
                />
              </button>
            </div>

            <div className={cn('px-3 py-1.5 rounded-lg text-sm font-medium', overallStatusColor)}>
              系統狀態: {status?.overallStatus || '檢測中...'}
            </div>

            <button
              onClick={() => { refetchStatus(); refetchLogs(); }}
              className="p-2 rounded-lg bg-dark-700 hover:bg-dark-600 transition-colors"
            >
              <svg className="w-4 h-4 text-dark-300" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
              </svg>
            </button>
          </div>
        </div>
      </div>

      {/* Data Sources Grid */}
      <Card>
        <CardHeader>
          <CardTitle>
            <svg className="w-5 h-5 text-primary-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 7v10c0 2.21 3.582 4 8 4s8-1.79 8-4V7M4 7c0 2.21 3.582 4 8 4s8-1.79 8-4M4 7c0-2.21 3.582-4 8-4s8 1.79 8 4m0 5c0 2.21-3.582 4-8 4s-8-1.79-8-4" />
            </svg>
            資料來源狀態
          </CardTitle>
        </CardHeader>
        <CardContent>
          {statusLoading ? (
            <div className="flex items-center justify-center py-12">
              <div className="w-8 h-8 border-2 border-dark-600 border-t-primary-500 rounded-full animate-spin" />
            </div>
          ) : (
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
              {status?.dataSources.map((source) => (
                <DataSourceCard key={source.tableName} source={source} />
              ))}
            </div>
          )}

          {status && (
            <div className="mt-4 pt-4 border-t border-dark-700/50 text-xs text-dark-500 text-right">
              最後檢查時間: {formatDate(status.timestamp, 'long')}
            </div>
          )}
        </CardContent>
      </Card>

      {/* System Logs */}
      <Card>
        <CardHeader>
          <div className="flex items-center justify-between w-full">
            <CardTitle>
              <svg className="w-5 h-5 text-accent-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
              </svg>
              系統日誌
              {logsData && (
                <Badge variant="default" size="sm" className="ml-2">
                  {logsData.totalCount} 筆
                </Badge>
              )}
            </CardTitle>

            <div className="flex items-center gap-2">
              <select
                value={logLevel}
                onChange={(e) => setLogLevel(e.target.value)}
                className="bg-dark-700 border border-dark-600 rounded px-2 py-1 text-sm text-dark-200"
              >
                <option value="">全部級別</option>
                <option value="INFO">INFO</option>
                <option value="WARNING">WARNING</option>
                <option value="ERROR">ERROR</option>
              </select>

              <button
                onClick={() => clearLogsMutation.mutate()}
                disabled={clearLogsMutation.isPending}
                className="px-3 py-1 text-xs bg-dark-700 hover:bg-dark-600 rounded transition-colors text-dark-300"
              >
                清除日誌
              </button>
            </div>
          </div>
        </CardHeader>
        <CardContent>
          <div className="max-h-[400px] overflow-y-auto bg-dark-900/50 rounded-lg p-3">
            {logsLoading ? (
              <div className="flex items-center justify-center py-8">
                <div className="w-6 h-6 border-2 border-dark-600 border-t-primary-500 rounded-full animate-spin" />
              </div>
            ) : logsData?.logs.length === 0 ? (
              <div className="text-center py-8 text-dark-500 text-sm">
                暫無日誌記錄
              </div>
            ) : (
              logsData?.logs.map((log, index) => (
                <LogEntry key={`${log.timestamp}-${index}`} log={log} />
              ))
            )}
          </div>
        </CardContent>
      </Card>

      {/* Quick Actions */}
      <Card>
        <CardHeader>
          <CardTitle>
            <svg className="w-5 h-5 text-bullish-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 10V3L4 14h7v7l9-11h-7z" />
            </svg>
            快速操作
          </CardTitle>
        </CardHeader>
        <CardContent>
          <div className="flex flex-wrap gap-3">
            <a
              href="/api/admin/backfill"
              target="_blank"
              rel="noopener noreferrer"
              className="px-4 py-2 rounded-lg bg-primary-500/20 text-primary-400 hover:bg-primary-500/30 transition-colors text-sm"
            >
              回補任務管理
            </a>
            <Link
              to="/tickers"
              className="px-4 py-2 rounded-lg bg-accent-500/20 text-accent-400 hover:bg-accent-500/30 transition-colors text-sm"
            >
              追蹤股票管理
            </Link>
            <button
              onClick={() => addLogMutation.mutate('手動測試日誌 - ' + new Date().toISOString())}
              className="px-4 py-2 rounded-lg bg-dark-700 text-dark-300 hover:bg-dark-600 transition-colors text-sm"
            >
              新增測試日誌
            </button>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
