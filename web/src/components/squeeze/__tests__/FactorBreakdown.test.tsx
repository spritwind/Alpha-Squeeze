import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { FactorBreakdown, FactorSummary } from '../FactorBreakdown';
import type { FactorScores } from '../../../types';

const mockFactors: FactorScores = {
  borrowScore: 90,
  gammaScore: 75,
  marginScore: 80,
  momentumScore: 85,
};

describe('FactorBreakdown', () => {
  it('renders all factor labels', () => {
    render(<FactorBreakdown factors={mockFactors} />);

    expect(screen.getByText('法人回補')).toBeInTheDocument();
    expect(screen.getByText('Gamma壓縮')).toBeInTheDocument();
    expect(screen.getByText('空單擁擠')).toBeInTheDocument();
    expect(screen.getByText('價量動能')).toBeInTheDocument();
  });

  it('renders factor scores', () => {
    render(<FactorBreakdown factors={mockFactors} />);

    expect(screen.getByText('90')).toBeInTheDocument();
    expect(screen.getByText('75')).toBeInTheDocument();
    expect(screen.getByText('80')).toBeInTheDocument();
    expect(screen.getByText('85')).toBeInTheDocument();
  });

  it('shows weighted scores when showWeighted is true', () => {
    render(<FactorBreakdown factors={mockFactors} showWeighted />);

    // 法人回補: 90 * 0.35 = 31.5
    expect(screen.getByText('+31.5')).toBeInTheDocument();
    // Gamma壓縮: 75 * 0.25 = 18.8 (四捨五入)
    expect(screen.getByText('+18.8')).toBeInTheDocument();
  });

  it('hides weighted scores when showWeighted is false', () => {
    render(<FactorBreakdown factors={mockFactors} showWeighted={false} />);

    expect(screen.queryByText('+31.5')).not.toBeInTheDocument();
  });
});

describe('FactorSummary', () => {
  it('renders compact factor display', () => {
    render(<FactorSummary factors={mockFactors} />);

    expect(screen.getByText('借:90')).toBeInTheDocument();
    expect(screen.getByText('G:75')).toBeInTheDocument();
    expect(screen.getByText('空:80')).toBeInTheDocument();
    expect(screen.getByText('量:85')).toBeInTheDocument();
  });
});
