import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { SqueezeCard } from '../SqueezeCard';
import type { SqueezeSignal } from '../../../types';

const mockSignal: SqueezeSignal = {
  ticker: '2330',
  score: 85,
  trend: 'BULLISH',
  comment: '軋空潛力高，法人回補訊號強勁',
  factors: {
    borrowScore: 90,
    gammaScore: 75,
    marginScore: 80,
    momentumScore: 85,
  },
};

describe('SqueezeCard', () => {
  it('renders ticker and score correctly', () => {
    render(<SqueezeCard signal={mockSignal} rank={1} />);

    expect(screen.getByText('2330')).toBeInTheDocument();
    // 使用 getAllByText 因為分數可能出現多次 (score 和 factor 可能相同)
    const scoreElements = screen.getAllByText('85');
    expect(scoreElements.length).toBeGreaterThan(0);
    expect(screen.getByText('#1')).toBeInTheDocument();
  });

  it('shows BULLISH trend with correct badge', () => {
    render(<SqueezeCard signal={mockSignal} rank={1} />);

    expect(screen.getByText('BULLISH')).toBeInTheDocument();
  });

  it('calls onClick when clicked', () => {
    const handleClick = vi.fn();
    render(<SqueezeCard signal={mockSignal} rank={1} onClick={handleClick} />);

    // 點擊整個卡片
    const card = screen.getByText('2330').closest('div');
    if (card) {
      fireEvent.click(card);
      expect(handleClick).toHaveBeenCalledTimes(1);
    }
  });

  it('renders factor breakdown when factors exist', () => {
    render(<SqueezeCard signal={mockSignal} rank={1} />);

    // SqueezeCard 使用 compact 模式，顯示短標籤
    expect(screen.getByText('法人')).toBeInTheDocument();
    expect(screen.getByText('Gamma')).toBeInTheDocument();
    expect(screen.getByText('空單')).toBeInTheDocument();
    expect(screen.getByText('動能')).toBeInTheDocument();
  });

  it('handles degraded mode correctly', () => {
    const degradedSignal: SqueezeSignal = {
      ...mockSignal,
      trend: 'DEGRADED',
      factors: null,
    };

    render(<SqueezeCard signal={degradedSignal} rank={1} />);
    expect(screen.getByText('降級模式')).toBeInTheDocument();
  });

  it('shows comment text', () => {
    render(<SqueezeCard signal={mockSignal} rank={1} />);
    expect(screen.getByText('軋空潛力高，法人回補訊號強勁')).toBeInTheDocument();
  });

  it('applies selected styling when isSelected is true', () => {
    render(<SqueezeCard signal={mockSignal} rank={1} isSelected />);

    // 檢查是否有 ring 樣式 - 找到最外層的卡片容器
    const tickerElement = screen.getByText('2330');
    // 向上遍歷 DOM 找到有 ring-2 的元素
    let element = tickerElement.parentElement;
    let foundRing = false;
    while (element) {
      if (element.className?.includes('ring-2')) {
        foundRing = true;
        break;
      }
      element = element.parentElement;
    }
    expect(foundRing).toBe(true);
  });

  it('shows different score colors based on score value', () => {
    // 創建不同的測試信號 (使用不同的 factor 值避免重複)
    const createSignal = (score: number): SqueezeSignal => ({
      ticker: '2330',
      score,
      trend: 'BULLISH',
      comment: 'test',
      factors: null, // 不顯示 factors 避免數字重複
    });

    // 高分 (紅色/bullish-400) - 使用唯一的分數
    const { rerender } = render(<SqueezeCard signal={createSignal(71)} rank={1} />);
    let scoreElement = screen.getByText('71');
    expect(scoreElement.className).toContain('text-bullish-400');

    // 低分 (綠色/bearish-400)
    rerender(<SqueezeCard signal={createSignal(39)} rank={1} />);
    scoreElement = screen.getByText('39');
    expect(scoreElement.className).toContain('text-bearish-400');

    // 中等分數 (灰色/dark-300)
    rerender(<SqueezeCard signal={createSignal(55)} rank={1} />);
    scoreElement = screen.getByText('55');
    expect(scoreElement.className).toContain('text-dark-300');
  });
});
