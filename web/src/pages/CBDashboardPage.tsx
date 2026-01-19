import { useState } from 'react';
import { CBWarningList } from '../components/cb/CBWarningList';
import { useCriticalCBs, useCBWarning } from '../hooks/useCBWarnings';
import { CBWarningCard } from '../components/cb/CBWarningCard';
import { Spinner } from '../components/ui/Spinner';

export function CBDashboardPage() {
  const [selectedCB, setSelectedCB] = useState<string | null>(null);
  const { data: criticalCBs, isLoading: criticalLoading } = useCriticalCBs(5, 10);
  const { data: cbDetail, isLoading: detailLoading } = useCBWarning(selectedCB || '');

  return (
    <div className="min-h-screen bg-gray-100">
      <div className="container mx-auto py-6 px-4">
        {/* 頁面標題 */}
        <div className="mb-6">
          <h1 className="text-2xl font-bold text-gray-900">CB 預警燈</h1>
          <p className="text-gray-500 mt-1">
            監控可轉債強制贖回風險 - 當股價連續 30 日超過轉換價 130% 時觸發
          </p>
        </div>

        <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
          {/* 左側: 主要列表 */}
          <div className="lg:col-span-2">
            <div className="bg-white rounded-lg shadow-sm p-6">
              <h2 className="text-lg font-semibold text-gray-900 mb-4">
                CB 預警總覽
              </h2>
              <CBWarningList onSelectCB={setSelectedCB} />
            </div>
          </div>

          {/* 右側: 高風險 CB 與詳情 */}
          <div className="space-y-6">
            {/* 高風險 CB */}
            <div className="bg-white rounded-lg shadow-sm p-6">
              <h2 className="text-lg font-semibold text-gray-900 mb-4 flex items-center">
                <span className="w-3 h-3 bg-red-500 rounded-full mr-2 animate-pulse" />
                高風險 CB
              </h2>

              {criticalLoading ? (
                <div className="flex justify-center py-6">
                  <Spinner />
                </div>
              ) : criticalCBs && criticalCBs.length > 0 ? (
                <div className="space-y-3">
                  {criticalCBs.map((cb) => (
                    <div
                      key={cb.cbTicker}
                      className="p-3 bg-red-50 border border-red-200 rounded-lg cursor-pointer hover:bg-red-100 transition-colors"
                      onClick={() => setSelectedCB(cb.cbTicker)}
                    >
                      <div className="flex justify-between items-center">
                        <div>
                          <span className="font-semibold text-red-800">{cb.cbTicker}</span>
                          <span className="text-sm text-red-600 ml-2">({cb.underlyingTicker})</span>
                        </div>
                        <span className="text-sm font-medium text-red-700">
                          {cb.consecutiveDays} 天
                        </span>
                      </div>
                      <div className="text-xs text-red-600 mt-1">
                        觸發進度: {cb.triggerProgress.toFixed(0)}%
                      </div>
                    </div>
                  ))}
                </div>
              ) : (
                <div className="text-center py-6 text-gray-500">
                  目前沒有高風險 CB
                </div>
              )}
            </div>

            {/* CB 詳情 */}
            {selectedCB && (
              <div className="bg-white rounded-lg shadow-sm p-6">
                <div className="flex justify-between items-center mb-4">
                  <h2 className="text-lg font-semibold text-gray-900">CB 詳情</h2>
                  <button
                    onClick={() => setSelectedCB(null)}
                    className="text-gray-400 hover:text-gray-600"
                  >
                    <svg
                      xmlns="http://www.w3.org/2000/svg"
                      className="h-5 w-5"
                      viewBox="0 0 20 20"
                      fill="currentColor"
                    >
                      <path
                        fillRule="evenodd"
                        d="M4.293 4.293a1 1 0 011.414 0L10 8.586l4.293-4.293a1 1 0 111.414 1.414L11.414 10l4.293 4.293a1 1 0 01-1.414 1.414L10 11.414l-4.293 4.293a1 1 0 01-1.414-1.414L8.586 10 4.293 5.707a1 1 0 010-1.414z"
                        clipRule="evenodd"
                      />
                    </svg>
                  </button>
                </div>

                {detailLoading ? (
                  <div className="flex justify-center py-6">
                    <Spinner />
                  </div>
                ) : cbDetail ? (
                  <div className="space-y-4">
                    <CBWarningCard warning={cbDetail} />

                    {/* 額外詳情 */}
                    <div className="grid grid-cols-2 gap-2 text-sm">
                      <div className="bg-gray-50 p-2 rounded">
                        <span className="text-gray-500">現價</span>
                        <div className="font-semibold">${cbDetail.currentPrice.toFixed(2)}</div>
                      </div>
                      <div className="bg-gray-50 p-2 rounded">
                        <span className="text-gray-500">剩餘天數</span>
                        <div className="font-semibold">{cbDetail.daysRemaining} 天</div>
                      </div>
                      <div className="bg-gray-50 p-2 rounded">
                        <span className="text-gray-500">是否超標</span>
                        <div className={`font-semibold ${cbDetail.isAboveTrigger ? 'text-red-600' : 'text-green-600'}`}>
                          {cbDetail.isAboveTrigger ? '是' : '否'}
                        </div>
                      </div>
                      <div className="bg-gray-50 p-2 rounded">
                        <span className="text-gray-500">餘額變化</span>
                        <div className={`font-semibold ${(cbDetail.balanceChangePercent || 0) < 0 ? 'text-red-600' : 'text-green-600'}`}>
                          {cbDetail.balanceChangePercent?.toFixed(2) || 0}%
                        </div>
                      </div>
                    </div>

                    {cbDetail.maturityDate && (
                      <div className="text-xs text-gray-500 text-center">
                        到期日: {new Date(cbDetail.maturityDate).toLocaleDateString('zh-TW')}
                      </div>
                    )}
                  </div>
                ) : (
                  <div className="text-center py-6 text-gray-500">
                    選擇一個 CB 查看詳情
                  </div>
                )}
              </div>
            )}

            {/* CB 強贖說明 */}
            <div className="bg-blue-50 border border-blue-200 rounded-lg p-4">
              <h3 className="font-semibold text-blue-800 mb-2">什麼是 CB 強制贖回?</h3>
              <p className="text-sm text-blue-700">
                當標的股價連續 30 個營業日收盤價超過轉換價的 130% 時，
                發行公司有權提前贖回 CB。這會迫使 CB 持有人轉換或賣出，
                可能形成籌碼壓力或軋空機會。
              </p>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
