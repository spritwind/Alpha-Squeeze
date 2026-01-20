import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { adminApi } from '../services/api';
import type { TrackedTickerDto, AddTrackedTickerRequest } from '../types';
import { Card, CardHeader, CardTitle, CardContent } from '../components/ui/Card';
import { Badge } from '../components/ui/Badge';
import { cn, formatDate } from '../lib/utils';

const CATEGORY_OPTIONS = [
  { value: '', label: '未分類' },
  { value: '半導體', label: '半導體' },
  { value: '金融', label: '金融' },
  { value: '電子', label: '電子' },
  { value: '傳產', label: '傳產' },
  { value: '航運', label: '航運' },
  { value: '生技', label: '生技' },
  { value: '其他', label: '其他' },
];

function TickerRow({
  ticker,
  onToggle,
  onEdit,
  onDelete,
}: {
  ticker: TrackedTickerDto;
  onToggle: (ticker: string, active: boolean) => void;
  onEdit: (ticker: TrackedTickerDto) => void;
  onDelete: (ticker: string) => void;
}) {
  return (
    <tr className="border-b border-dark-700/30 hover:bg-dark-800/30 transition-colors">
      <td className="px-4 py-3">
        <div className="flex items-center gap-2">
          <span className="font-mono font-semibold text-dark-100">{ticker.ticker}</span>
          {ticker.tickerName && (
            <span className="text-sm text-dark-400">{ticker.tickerName}</span>
          )}
        </div>
      </td>
      <td className="px-4 py-3">
        {ticker.category ? (
          <Badge variant="default" size="sm">{ticker.category}</Badge>
        ) : (
          <span className="text-dark-500 text-sm">-</span>
        )}
      </td>
      <td className="px-4 py-3 text-center">
        <span className="text-dark-300 font-mono">{ticker.priority}</span>
      </td>
      <td className="px-4 py-3 text-center">
        <button
          onClick={() => onToggle(ticker.ticker, !ticker.isActive)}
          className={cn(
            'relative w-10 h-5 rounded-full transition-colors',
            ticker.isActive ? 'bg-bullish-500' : 'bg-dark-600'
          )}
        >
          <span
            className={cn(
              'absolute top-0.5 left-0.5 w-4 h-4 rounded-full bg-white transition-transform',
              ticker.isActive && 'translate-x-5'
            )}
          />
        </button>
      </td>
      <td className="px-4 py-3 text-sm text-dark-400">
        {formatDate(ticker.addedAt, 'short')}
      </td>
      <td className="px-4 py-3">
        <div className="flex items-center gap-2 justify-end">
          <button
            onClick={() => onEdit(ticker)}
            className="p-1.5 rounded hover:bg-dark-700 transition-colors text-dark-400 hover:text-primary-400"
            title="編輯"
          >
            <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z" />
            </svg>
          </button>
          <button
            onClick={() => onDelete(ticker.ticker)}
            className="p-1.5 rounded hover:bg-dark-700 transition-colors text-dark-400 hover:text-bearish-400"
            title="刪除"
          >
            <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
            </svg>
          </button>
        </div>
      </td>
    </tr>
  );
}

function AddTickerForm({
  onSubmit,
  onCancel,
  initialData,
  isEdit,
}: {
  onSubmit: (data: AddTrackedTickerRequest) => void;
  onCancel: () => void;
  initialData?: TrackedTickerDto;
  isEdit?: boolean;
}) {
  const [formData, setFormData] = useState<AddTrackedTickerRequest>({
    ticker: initialData?.ticker || '',
    tickerName: initialData?.tickerName || '',
    category: initialData?.category || '',
    priority: initialData?.priority || 100,
    notes: initialData?.notes || '',
  });

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    onSubmit(formData);
  };

  return (
    <form onSubmit={handleSubmit} className="space-y-4">
      <div className="grid grid-cols-2 gap-4">
        <div>
          <label className="block text-sm font-medium text-dark-300 mb-1">
            股票代號 <span className="text-bearish-400">*</span>
          </label>
          <input
            type="text"
            value={formData.ticker}
            onChange={(e) => setFormData({ ...formData, ticker: e.target.value.toUpperCase() })}
            disabled={isEdit}
            className={cn(
              'w-full px-3 py-2 rounded-lg bg-dark-700 border border-dark-600 text-dark-100',
              'focus:outline-none focus:border-primary-500 focus:ring-1 focus:ring-primary-500',
              isEdit && 'opacity-50 cursor-not-allowed'
            )}
            placeholder="例: 2330"
            required
          />
        </div>
        <div>
          <label className="block text-sm font-medium text-dark-300 mb-1">股票名稱</label>
          <input
            type="text"
            value={formData.tickerName}
            onChange={(e) => setFormData({ ...formData, tickerName: e.target.value })}
            className="w-full px-3 py-2 rounded-lg bg-dark-700 border border-dark-600 text-dark-100 focus:outline-none focus:border-primary-500 focus:ring-1 focus:ring-primary-500"
            placeholder="例: 台積電"
          />
        </div>
      </div>

      <div className="grid grid-cols-2 gap-4">
        <div>
          <label className="block text-sm font-medium text-dark-300 mb-1">類股分類</label>
          <select
            value={formData.category}
            onChange={(e) => setFormData({ ...formData, category: e.target.value })}
            className="w-full px-3 py-2 rounded-lg bg-dark-700 border border-dark-600 text-dark-100 focus:outline-none focus:border-primary-500 focus:ring-1 focus:ring-primary-500"
          >
            {CATEGORY_OPTIONS.map((opt) => (
              <option key={opt.value} value={opt.value}>{opt.label}</option>
            ))}
          </select>
        </div>
        <div>
          <label className="block text-sm font-medium text-dark-300 mb-1">優先級</label>
          <input
            type="number"
            value={formData.priority}
            onChange={(e) => setFormData({ ...formData, priority: parseInt(e.target.value) || 100 })}
            className="w-full px-3 py-2 rounded-lg bg-dark-700 border border-dark-600 text-dark-100 focus:outline-none focus:border-primary-500 focus:ring-1 focus:ring-primary-500"
            min={1}
            max={1000}
          />
          <p className="text-xs text-dark-500 mt-1">數字越小優先級越高</p>
        </div>
      </div>

      <div>
        <label className="block text-sm font-medium text-dark-300 mb-1">備註</label>
        <textarea
          value={formData.notes}
          onChange={(e) => setFormData({ ...formData, notes: e.target.value })}
          className="w-full px-3 py-2 rounded-lg bg-dark-700 border border-dark-600 text-dark-100 focus:outline-none focus:border-primary-500 focus:ring-1 focus:ring-primary-500 resize-none"
          rows={2}
          placeholder="選填備註..."
        />
      </div>

      <div className="flex justify-end gap-3 pt-2">
        <button
          type="button"
          onClick={onCancel}
          className="px-4 py-2 rounded-lg bg-dark-700 text-dark-300 hover:bg-dark-600 transition-colors"
        >
          取消
        </button>
        <button
          type="submit"
          className="px-4 py-2 rounded-lg bg-primary-500 text-white hover:bg-primary-600 transition-colors"
        >
          {isEdit ? '更新' : '新增'}
        </button>
      </div>
    </form>
  );
}

export function TickersPage() {
  const queryClient = useQueryClient();
  const [showAddForm, setShowAddForm] = useState(false);
  const [editingTicker, setEditingTicker] = useState<TrackedTickerDto | null>(null);
  const [filterCategory, setFilterCategory] = useState('');
  const [filterActive, setFilterActive] = useState<'all' | 'active' | 'inactive'>('all');

  const { data: tickers, isLoading, error } = useQuery({
    queryKey: ['admin-tickers'],
    queryFn: () => adminApi.getTickers(),
  });

  const addMutation = useMutation({
    mutationFn: adminApi.addTicker,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin-tickers'] });
      setShowAddForm(false);
    },
  });

  const updateMutation = useMutation({
    mutationFn: ({ ticker, data }: { ticker: string; data: AddTrackedTickerRequest }) =>
      adminApi.updateTicker(ticker, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin-tickers'] });
      setEditingTicker(null);
    },
  });

  const toggleMutation = useMutation({
    mutationFn: ({ ticker, active }: { ticker: string; active: boolean }) =>
      adminApi.setTickerActive(ticker, active),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin-tickers'] });
    },
  });

  const deleteMutation = useMutation({
    mutationFn: adminApi.removeTicker,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin-tickers'] });
    },
  });

  const handleToggle = (ticker: string, active: boolean) => {
    toggleMutation.mutate({ ticker, active });
  };

  const handleDelete = (ticker: string) => {
    if (window.confirm(`確定要刪除 ${ticker} 嗎？`)) {
      deleteMutation.mutate(ticker);
    }
  };

  const handleAdd = (data: AddTrackedTickerRequest) => {
    addMutation.mutate(data);
  };

  const handleUpdate = (data: AddTrackedTickerRequest) => {
    if (editingTicker) {
      updateMutation.mutate({ ticker: editingTicker.ticker, data });
    }
  };

  // Filter tickers
  const filteredTickers = tickers?.filter((t) => {
    if (filterCategory && t.category !== filterCategory) return false;
    if (filterActive === 'active' && !t.isActive) return false;
    if (filterActive === 'inactive' && t.isActive) return false;
    return true;
  });

  const activeCount = tickers?.filter((t) => t.isActive).length || 0;
  const totalCount = tickers?.length || 0;

  if (error) {
    return (
      <div className="flex items-center justify-center min-h-[400px]">
        <Card variant="glass" className="max-w-md w-full">
          <CardContent className="py-8 text-center">
            <div className="w-16 h-16 mx-auto mb-4 rounded-full bg-bearish-500/10 flex items-center justify-center">
              <svg className="w-8 h-8 text-bearish-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
              </svg>
            </div>
            <h3 className="text-lg font-semibold text-dark-100 mb-2">載入失敗</h3>
            <p className="text-sm text-dark-400">{(error as Error).message}</p>
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
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10" />
              </svg>
            </div>
            <div>
              <h1 className="text-2xl font-bold text-dark-50">追蹤股票管理</h1>
              <p className="text-dark-400 text-sm">
                共 {totalCount} 檔，啟用中 {activeCount} 檔
              </p>
            </div>
          </div>

          <button
            onClick={() => setShowAddForm(true)}
            className="flex items-center gap-2 px-4 py-2 rounded-lg bg-primary-500 text-white hover:bg-primary-600 transition-colors"
          >
            <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
            </svg>
            新增股票
          </button>
        </div>
      </div>

      {/* Add/Edit Form Modal */}
      {(showAddForm || editingTicker) && (
        <Card>
          <CardHeader>
            <CardTitle>
              <svg className="w-5 h-5 text-primary-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z" />
              </svg>
              {editingTicker ? `編輯 ${editingTicker.ticker}` : '新增追蹤股票'}
            </CardTitle>
          </CardHeader>
          <CardContent>
            <AddTickerForm
              onSubmit={editingTicker ? handleUpdate : handleAdd}
              onCancel={() => {
                setShowAddForm(false);
                setEditingTicker(null);
              }}
              initialData={editingTicker || undefined}
              isEdit={!!editingTicker}
            />
            {(addMutation.error || updateMutation.error) && (
              <div className="mt-4 p-3 rounded-lg bg-bearish-500/10 text-bearish-400 text-sm">
                {((addMutation.error || updateMutation.error) as Error).message}
              </div>
            )}
          </CardContent>
        </Card>
      )}

      {/* Filters */}
      <Card>
        <CardContent className="py-4">
          <div className="flex flex-wrap items-center gap-4">
            <div className="flex items-center gap-2">
              <span className="text-dark-400 text-sm">類股:</span>
              <select
                value={filterCategory}
                onChange={(e) => setFilterCategory(e.target.value)}
                className="px-3 py-1.5 rounded-lg bg-dark-700 border border-dark-600 text-dark-200 text-sm focus:outline-none focus:border-primary-500"
              >
                <option value="">全部</option>
                {CATEGORY_OPTIONS.filter((o) => o.value).map((opt) => (
                  <option key={opt.value} value={opt.value}>{opt.label}</option>
                ))}
              </select>
            </div>
            <div className="flex items-center gap-2">
              <span className="text-dark-400 text-sm">狀態:</span>
              <select
                value={filterActive}
                onChange={(e) => setFilterActive(e.target.value as 'all' | 'active' | 'inactive')}
                className="px-3 py-1.5 rounded-lg bg-dark-700 border border-dark-600 text-dark-200 text-sm focus:outline-none focus:border-primary-500"
              >
                <option value="all">全部</option>
                <option value="active">啟用中</option>
                <option value="inactive">已停用</option>
              </select>
            </div>
            <div className="ml-auto text-sm text-dark-500">
              顯示 {filteredTickers?.length || 0} / {totalCount} 筆
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Tickers Table */}
      <Card>
        <CardContent className="p-0">
          {isLoading ? (
            <div className="flex items-center justify-center py-12">
              <div className="w-8 h-8 border-2 border-dark-600 border-t-primary-500 rounded-full animate-spin" />
            </div>
          ) : filteredTickers && filteredTickers.length > 0 ? (
            <div className="overflow-x-auto">
              <table className="w-full">
                <thead>
                  <tr className="border-b border-dark-700/50 bg-dark-800/50">
                    <th className="px-4 py-3 text-left text-sm font-medium text-dark-400">股票</th>
                    <th className="px-4 py-3 text-left text-sm font-medium text-dark-400">類股</th>
                    <th className="px-4 py-3 text-center text-sm font-medium text-dark-400">優先級</th>
                    <th className="px-4 py-3 text-center text-sm font-medium text-dark-400">啟用</th>
                    <th className="px-4 py-3 text-left text-sm font-medium text-dark-400">加入時間</th>
                    <th className="px-4 py-3 text-right text-sm font-medium text-dark-400">操作</th>
                  </tr>
                </thead>
                <tbody>
                  {filteredTickers.map((ticker) => (
                    <TickerRow
                      key={ticker.ticker}
                      ticker={ticker}
                      onToggle={handleToggle}
                      onEdit={setEditingTicker}
                      onDelete={handleDelete}
                    />
                  ))}
                </tbody>
              </table>
            </div>
          ) : (
            <div className="text-center py-12 text-dark-500">
              {tickers?.length === 0 ? '尚無追蹤股票' : '沒有符合篩選條件的股票'}
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
