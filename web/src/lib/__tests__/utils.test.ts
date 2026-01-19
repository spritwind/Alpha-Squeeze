import { describe, it, expect } from 'vitest';
import { cn, formatNumber, formatPercent, formatVolume, formatDate } from '../utils';

describe('cn', () => {
  it('merges class names', () => {
    expect(cn('text-red-500', 'bg-blue-500')).toBe('text-red-500 bg-blue-500');
  });

  it('handles conditional classes', () => {
    expect(cn('base', true && 'conditional')).toBe('base conditional');
    expect(cn('base', false && 'conditional')).toBe('base');
  });

  it('merges tailwind classes correctly', () => {
    expect(cn('p-2', 'p-4')).toBe('p-4');
    expect(cn('text-red-500', 'text-blue-500')).toBe('text-blue-500');
  });
});

describe('formatNumber', () => {
  it('formats numbers with default decimals', () => {
    expect(formatNumber(123.456)).toBe('123.46');
  });

  it('formats numbers with custom decimals', () => {
    expect(formatNumber(123.456, 1)).toBe('123.5');
    expect(formatNumber(123.456, 0)).toBe('123');
  });

  it('returns dash for null/undefined', () => {
    expect(formatNumber(null)).toBe('-');
    expect(formatNumber(undefined)).toBe('-');
  });

  it('handles zero', () => {
    expect(formatNumber(0)).toBe('0.00');
  });
});

describe('formatPercent', () => {
  it('formats percentage with default decimals', () => {
    expect(formatPercent(12.345)).toBe('12.35%');
  });

  it('formats percentage with custom decimals', () => {
    expect(formatPercent(12.345, 1)).toBe('12.3%');
  });

  it('returns dash for null/undefined', () => {
    expect(formatPercent(null)).toBe('-');
    expect(formatPercent(undefined)).toBe('-');
  });
});

describe('formatVolume', () => {
  it('formats large numbers in 億', () => {
    expect(formatVolume(150000000)).toBe('1.50 億');
    expect(formatVolume(100000000)).toBe('1.00 億');
  });

  it('formats medium numbers in 萬', () => {
    expect(formatVolume(50000)).toBe('5 萬');
    expect(formatVolume(10000)).toBe('1 萬');
  });

  it('formats small numbers as is', () => {
    expect(formatVolume(1234)).toMatch(/1,234|1234/);
  });

  it('returns dash for null/undefined', () => {
    expect(formatVolume(null)).toBe('-');
    expect(formatVolume(undefined)).toBe('-');
  });
});

describe('formatDate', () => {
  it('formats date in short format', () => {
    const result = formatDate('2025-01-15', 'short');
    expect(result).toMatch(/01.*15/);
  });

  it('formats date in long format', () => {
    const result = formatDate('2025-01-15', 'long');
    expect(result).toMatch(/2025.*01.*15/);
  });

  it('returns dash for null/undefined', () => {
    expect(formatDate(null)).toBe('-');
    expect(formatDate(undefined)).toBe('-');
  });
});
