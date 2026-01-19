import { useState, useEffect } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { configApi } from '../services/api';
import type { WeightsDto, ThresholdsDto } from '../types';

interface FormData {
  weights: WeightsDto;
  thresholds: ThresholdsDto;
}

export function SettingsPage() {
  const queryClient = useQueryClient();
  const [formData, setFormData] = useState<FormData>({
    weights: { borrow: 0.35, gamma: 0.25, margin: 0.20, momentum: 0.20 },
    thresholds: { bullish: 70, bearish: 40 },
  });
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  // 載入配置
  const { data: config, isLoading, isError } = useQuery({
    queryKey: ['squeezeConfig'],
    queryFn: configApi.getSqueezeConfig,
  });

  // 更新配置
  const mutation = useMutation({
    mutationFn: (data: FormData) =>
      configApi.updateSqueezeConfig(data.weights, data.thresholds),
    onSuccess: () => {
      setSuccess('配置已成功更新');
      setError(null);
      queryClient.invalidateQueries({ queryKey: ['squeezeConfig'] });
      setTimeout(() => setSuccess(null), 3000);
    },
    onError: (err: Error) => {
      setError(err.message || '更新配置失敗');
      setSuccess(null);
    },
  });

  // 初始化表單資料
  useEffect(() => {
    if (config) {
      setFormData({
        weights: { ...config.weights },
        thresholds: { ...config.thresholds },
      });
    }
  }, [config]);

  // 計算權重總和
  const weightsTotal =
    formData.weights.borrow +
    formData.weights.gamma +
    formData.weights.margin +
    formData.weights.momentum;

  const isWeightsValid = Math.abs(weightsTotal - 1.0) < 0.001;
  const isThresholdsValid = formData.thresholds.bearish < formData.thresholds.bullish;

  const handleWeightChange = (field: keyof WeightsDto, value: string) => {
    const numValue = parseFloat(value) || 0;
    setFormData((prev) => ({
      ...prev,
      weights: { ...prev.weights, [field]: numValue },
    }));
    setError(null);
  };

  const handleThresholdChange = (field: keyof ThresholdsDto, value: string) => {
    const numValue = parseInt(value, 10) || 0;
    setFormData((prev) => ({
      ...prev,
      thresholds: { ...prev.thresholds, [field]: numValue },
    }));
    setError(null);
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!isWeightsValid) {
      setError(`權重總和必須為 1.0，目前為 ${weightsTotal.toFixed(2)}`);
      return;
    }
    if (!isThresholdsValid) {
      setError('看空門檻必須小於看多門檻');
      return;
    }
    mutation.mutate(formData);
  };

  const handleReset = () => {
    if (config) {
      setFormData({
        weights: { ...config.weights },
        thresholds: { ...config.thresholds },
      });
    }
    setError(null);
    setSuccess(null);
  };

  if (isLoading) {
    return (
      <div className="flex justify-center items-center h-64">
        <div className="text-gray-500">載入配置中...</div>
      </div>
    );
  }

  if (isError) {
    return (
      <div className="flex justify-center items-center h-64">
        <div className="text-red-500">載入配置失敗，請確認 API 服務是否啟動</div>
      </div>
    );
  }

  return (
    <div className="max-w-2xl mx-auto">
      <h1 className="text-2xl font-bold text-gray-900 mb-6">系統設定</h1>

      <form onSubmit={handleSubmit} className="space-y-6">
        {/* 權重設定 */}
        <div className="bg-white shadow rounded-lg p-6">
          <h2 className="text-lg font-semibold text-gray-900 mb-4">
            軋空演算法權重
          </h2>
          <p className="text-sm text-gray-500 mb-4">
            各維度權重總和必須為 1.0 (100%)
          </p>

          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                借券餘額 (F_B)
              </label>
              <div className="flex items-center">
                <input
                  type="number"
                  step="0.05"
                  min="0"
                  max="1"
                  value={formData.weights.borrow}
                  onChange={(e) => handleWeightChange('borrow', e.target.value)}
                  className="flex-1 rounded-md border-gray-300 shadow-sm focus:border-blue-500 focus:ring-blue-500"
                />
                <span className="ml-2 text-gray-500">
                  ({(formData.weights.borrow * 100).toFixed(0)}%)
                </span>
              </div>
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                Gamma 效應 (F_G)
              </label>
              <div className="flex items-center">
                <input
                  type="number"
                  step="0.05"
                  min="0"
                  max="1"
                  value={formData.weights.gamma}
                  onChange={(e) => handleWeightChange('gamma', e.target.value)}
                  className="flex-1 rounded-md border-gray-300 shadow-sm focus:border-blue-500 focus:ring-blue-500"
                />
                <span className="ml-2 text-gray-500">
                  ({(formData.weights.gamma * 100).toFixed(0)}%)
                </span>
              </div>
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                券資比 (F_M)
              </label>
              <div className="flex items-center">
                <input
                  type="number"
                  step="0.05"
                  min="0"
                  max="1"
                  value={formData.weights.margin}
                  onChange={(e) => handleWeightChange('margin', e.target.value)}
                  className="flex-1 rounded-md border-gray-300 shadow-sm focus:border-blue-500 focus:ring-blue-500"
                />
                <span className="ml-2 text-gray-500">
                  ({(formData.weights.margin * 100).toFixed(0)}%)
                </span>
              </div>
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                價量動能 (F_V)
              </label>
              <div className="flex items-center">
                <input
                  type="number"
                  step="0.05"
                  min="0"
                  max="1"
                  value={formData.weights.momentum}
                  onChange={(e) => handleWeightChange('momentum', e.target.value)}
                  className="flex-1 rounded-md border-gray-300 shadow-sm focus:border-blue-500 focus:ring-blue-500"
                />
                <span className="ml-2 text-gray-500">
                  ({(formData.weights.momentum * 100).toFixed(0)}%)
                </span>
              </div>
            </div>
          </div>

          <div className={`mt-4 text-sm ${isWeightsValid ? 'text-green-600' : 'text-red-600'}`}>
            權重總和: {(weightsTotal * 100).toFixed(0)}%
            {isWeightsValid ? ' (有效)' : ' (無效，必須為 100%)'}
          </div>
        </div>

        {/* 門檻設定 */}
        <div className="bg-white shadow rounded-lg p-6">
          <h2 className="text-lg font-semibold text-gray-900 mb-4">
            趨勢判定門檻
          </h2>
          <p className="text-sm text-gray-500 mb-4">
            分數範圍 0-100，看空門檻必須小於看多門檻
          </p>

          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                看多門檻 (BULLISH)
              </label>
              <input
                type="number"
                min="0"
                max="100"
                value={formData.thresholds.bullish}
                onChange={(e) => handleThresholdChange('bullish', e.target.value)}
                className="w-full rounded-md border-gray-300 shadow-sm focus:border-blue-500 focus:ring-blue-500"
              />
              <p className="mt-1 text-xs text-gray-500">
                分數 &ge; {formData.thresholds.bullish} 判定為看多
              </p>
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                看空門檻 (BEARISH)
              </label>
              <input
                type="number"
                min="0"
                max="100"
                value={formData.thresholds.bearish}
                onChange={(e) => handleThresholdChange('bearish', e.target.value)}
                className="w-full rounded-md border-gray-300 shadow-sm focus:border-blue-500 focus:ring-blue-500"
              />
              <p className="mt-1 text-xs text-gray-500">
                分數 &le; {formData.thresholds.bearish} 判定為看空
              </p>
            </div>
          </div>

          {!isThresholdsValid && (
            <div className="mt-4 text-sm text-red-600">
              門檻設定無效：看空門檻 ({formData.thresholds.bearish}) 必須小於看多門檻 ({formData.thresholds.bullish})
            </div>
          )}
        </div>

        {/* 訊息提示 */}
        {error && (
          <div className="bg-red-50 border border-red-200 rounded-md p-4 text-red-700">
            {error}
          </div>
        )}
        {success && (
          <div className="bg-green-50 border border-green-200 rounded-md p-4 text-green-700">
            {success}
          </div>
        )}

        {/* 按鈕 */}
        <div className="flex justify-end space-x-4">
          <button
            type="button"
            onClick={handleReset}
            className="px-4 py-2 text-gray-700 bg-gray-100 rounded-md hover:bg-gray-200 focus:outline-none focus:ring-2 focus:ring-gray-500"
          >
            重設
          </button>
          <button
            type="submit"
            disabled={mutation.isPending || !isWeightsValid || !isThresholdsValid}
            className="px-4 py-2 text-white bg-blue-600 rounded-md hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-500 disabled:opacity-50 disabled:cursor-not-allowed"
          >
            {mutation.isPending ? '儲存中...' : '儲存設定'}
          </button>
        </div>
      </form>

      {/* 說明區塊 */}
      <div className="mt-8 bg-gray-50 rounded-lg p-6">
        <h3 className="text-lg font-semibold text-gray-900 mb-3">演算法說明</h3>
        <p className="text-sm text-gray-600 mb-4">
          Squeeze Score 計算公式：
          <code className="block mt-2 bg-gray-100 p-2 rounded text-xs">
            S = (W_B x F_B) + (W_G x F_G) + (W_M x F_M) + (W_V x F_V)
          </code>
        </p>
        <ul className="text-sm text-gray-600 space-y-2">
          <li><strong>F_B (借券餘額)</strong>：負值（回補）越多，得分越高</li>
          <li><strong>F_G (Gamma 效應)</strong>：IV &lt; HV 時，得分越高</li>
          <li><strong>F_M (券資比)</strong>：數值越高（空單擁擠），得分越高</li>
          <li><strong>F_V (價量動能)</strong>：帶量突破壓力位，得分越高</li>
        </ul>
      </div>
    </div>
  );
}
