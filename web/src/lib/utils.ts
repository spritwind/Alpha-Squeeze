import { clsx, type ClassValue } from 'clsx';
import { twMerge } from 'tailwind-merge';

/**
 * 合併 Tailwind CSS 類名
 */
export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs));
}

/**
 * 格式化數字
 */
export function formatNumber(value: number | null | undefined, decimals = 2): string {
  if (value == null) return '-';
  return value.toLocaleString('zh-TW', {
    minimumFractionDigits: decimals,
    maximumFractionDigits: decimals,
  });
}

/**
 * 格式化百分比
 */
export function formatPercent(value: number | null | undefined, decimals = 2): string {
  if (value == null) return '-';
  return `${formatNumber(value, decimals)}%`;
}

/**
 * 格式化大數字 (如成交量)
 */
export function formatVolume(value: number | null | undefined): string {
  if (value == null) return '-';
  if (value >= 1e8) return `${(value / 1e8).toFixed(2)} 億`;
  if (value >= 1e4) return `${(value / 1e4).toFixed(0)} 萬`;
  return value.toLocaleString('zh-TW');
}

/**
 * 格式化日期
 */
export function formatDate(dateStr: string | null | undefined, format: 'short' | 'long' = 'short'): string {
  if (!dateStr) return '-';
  const date = new Date(dateStr);
  if (format === 'short') {
    return date.toLocaleDateString('zh-TW', { month: '2-digit', day: '2-digit' });
  }
  return date.toLocaleDateString('zh-TW', { year: 'numeric', month: '2-digit', day: '2-digit' });
}
