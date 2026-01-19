export interface FactorScores {
  borrowScore: number;
  gammaScore: number;
  marginScore: number;
  momentumScore: number;
}

export interface SqueezeSignal {
  ticker: string;
  score: number;
  trend: 'BULLISH' | 'NEUTRAL' | 'BEARISH' | 'DEGRADED';
  comment: string;
  factors: FactorScores | null;
}

export interface TopCandidates {
  candidates: SqueezeSignal[];
  analysisDate: string;
  generatedAt: string;
}

export interface StockMetric {
  ticker: string;
  tradeDate: string;
  closePrice: number | null;
  openPrice: number | null;
  highPrice: number | null;
  lowPrice: number | null;
  borrowingBalanceChange: number | null;
  marginRatio: number | null;
  historicalVolatility20D: number | null;
  volume: number | null;
}

export interface HealthStatus {
  status: string;
  engineAvailable: boolean;
  timestamp: string;
}
