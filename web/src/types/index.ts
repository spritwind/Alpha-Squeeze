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

// 配置相關類型
export interface WeightsDto {
  borrow: number;
  gamma: number;
  margin: number;
  momentum: number;
}

export interface ThresholdsDto {
  bullish: number;
  bearish: number;
}

export interface SqueezeConfigDto {
  weights: WeightsDto;
  thresholds: ThresholdsDto;
}

export interface ConfigItemDto {
  key: string;
  value: string;
  valueType: string;
  description: string;
  minValue: number | null;
  maxValue: number | null;
  isReadOnly: boolean;
  updatedAt: string;
}

export interface ConfigCategoryDto {
  category: string;
  description: string;
  items: ConfigItemDto[];
}

// ========== CB 預警燈相關型別 ==========

export type CBWarningLevel = 'SAFE' | 'CAUTION' | 'WARNING' | 'CRITICAL';

export interface CBWarningDto {
  cbTicker: string;
  underlyingTicker: string;
  cbName?: string;
  tradeDate: string;
  currentPrice: number;
  conversionPrice: number;
  priceRatio: number;
  isAboveTrigger: boolean;
  consecutiveDays: number;
  daysRemaining: number;
  triggerProgress: number;
  outstandingBalance: number;
  totalIssueAmount?: number;
  balanceChangePercent?: number;
  warningLevel: CBWarningLevel;
  comment: string;
  maturityDate?: string;
}

export interface CBWarningListResponse {
  warnings: CBWarningDto[];
  analysisDate: string;
  totalCount: number;
  criticalCount: number;
  warningCount: number;
  cautionCount: number;
}

export interface CBIssuanceDto {
  cbTicker: string;
  underlyingTicker: string;
  cbName?: string;
  issueDate: string;
  maturityDate: string;
  currentConversionPrice: number;
  totalIssueAmount: number;
  outstandingAmount: number;
  redemptionTriggerPct: number;
  redemptionTriggerDays: number;
  isActive: boolean;
}

export interface CBDailyTrackingDto {
  tradeDate: string;
  underlyingClosePrice?: number;
  priceRatio?: number;
  isAboveTrigger: boolean;
  consecutiveDays: number;
  outstandingBalance?: number;
  warningLevel?: CBWarningLevel;
}

export interface CBTrackingHistoryDto {
  cbTicker: string;
  history: CBDailyTrackingDto[];
}

// ========== 追蹤股票管理相關型別 ==========

export interface TrackedTickerDto {
  ticker: string;
  tickerName?: string;
  category?: string;
  isActive: boolean;
  priority: number;
  addedAt: string;
  notes?: string;
}

export interface AddTrackedTickerRequest {
  ticker: string;
  tickerName?: string;
  category?: string;
  priority?: number;
  notes?: string;
}

export interface BackfillJobDto {
  id: number;
  jobType: string;
  startDate: string;
  endDate: string;
  status: string;
  totalTickers: number;
  processedTickers: number;
  failedTickers: number;
  progressPercent: number;
  errorMessage?: string;
  startedAt?: string;
  completedAt?: string;
  createdAt: string;
}

// ========== Discovery 雷達掃描相關型別 ==========

export interface DiscoveryPoolDto {
  ticker: string;
  tickerName?: string;
  industry?: string;
  closePrice?: number;
  volume?: number;
  volMultiplier?: number;
  shortRatio?: number;
  marginRatio?: number;
  hasCB: boolean;
  cbTicker?: string;
  cbPriceRatio?: number;
  squeezeScore?: number;
  scanDate: string;
}

export interface DiscoveryPoolResponse {
  items: DiscoveryPoolDto[];
  scanDate: string;
  totalCount: number;
}

export interface DiscoveryFilterRequest {
  scanDate?: string;
  minShortRatio?: number;
  minVolMultiplier?: number;
  minPrice?: number;
  minVolume?: number;
  hasCB?: boolean;
  minScore?: number;
  limit?: number;
}

export interface UserWatchListDto {
  ticker: string;
  tickerName?: string;
  addedTime: string;
  addedBy: string;
  isActive: boolean;
  priority: number;
  lastDeepScrapedTime?: string;
  lastSqueezeScore?: number;
  notes?: string;
}
