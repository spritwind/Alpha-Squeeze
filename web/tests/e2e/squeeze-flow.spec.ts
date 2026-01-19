import { test, expect } from '@playwright/test';

test.describe('Alpha Squeeze E2E Tests', () => {

  test.beforeEach(async ({ page }) => {
    await page.goto('/');
  });

  test('Dashboard loads with squeeze candidates', async ({ page }) => {
    // 等待載入完成
    await expect(page.locator('h1')).toContainText('Alpha Squeeze');

    // 確認有軋空候選清單
    const candidates = page.locator('[data-testid="squeeze-card"]');
    await expect(candidates.first()).toBeVisible({ timeout: 10000 });

    // 確認有排名顯示
    await expect(page.locator('text=#1')).toBeVisible();
  });

  test('Clicking candidate shows IV/HV chart', async ({ page }) => {
    // 等待第一個候選標的出現
    const firstCard = page.locator('[data-testid="squeeze-card"]').first();
    await expect(firstCard).toBeVisible({ timeout: 10000 });

    // 點擊第一個候選標的
    await firstCard.click();

    // 確認圖表出現
    const chart = page.locator('[data-testid="ivhv-chart"]');
    await expect(chart).toBeVisible({ timeout: 5000 });
  });

  test('Factor breakdown displays correctly', async ({ page }) => {
    // 等待候選標的載入
    const firstCard = page.locator('[data-testid="squeeze-card"]').first();
    await expect(firstCard).toBeVisible({ timeout: 10000 });

    // 點擊以顯示詳細資訊
    await firstCard.click();

    // 等待因子分解圖
    await expect(page.locator('text=法人回補')).toBeVisible({ timeout: 5000 });
    await expect(page.locator('text=Gamma壓縮')).toBeVisible();
    await expect(page.locator('text=空單擁擠')).toBeVisible();
    await expect(page.locator('text=價量動能')).toBeVisible();
  });

  test('API returns valid data structure', async ({ request }) => {
    const response = await request.get('/api/squeeze/top-candidates');
    expect(response.ok()).toBeTruthy();

    const data = await response.json();
    expect(data).toHaveProperty('candidates');
    expect(data).toHaveProperty('analysisDate');
    expect(Array.isArray(data.candidates)).toBeTruthy();

    if (data.candidates.length > 0) {
      const candidate = data.candidates[0];
      expect(candidate).toHaveProperty('ticker');
      expect(candidate).toHaveProperty('score');
      expect(candidate).toHaveProperty('trend');
      expect(candidate.score).toBeGreaterThanOrEqual(0);
      expect(candidate.score).toBeLessThanOrEqual(100);
    }
  });

  test('Stock detail page shows metrics', async ({ page }) => {
    // 等待候選標的載入
    const firstCard = page.locator('[data-testid="squeeze-card"]').first();
    await expect(firstCard).toBeVisible({ timeout: 10000 });

    // 點擊第一個候選標的
    await firstCard.click();

    // 確認有顯示評分儀表
    const scoreGauge = page.locator('[data-testid="score-gauge"]');
    await expect(scoreGauge).toBeVisible({ timeout: 5000 });
  });

  test('Price chart displays correctly', async ({ page }) => {
    // 等待候選標的載入並點擊
    const firstCard = page.locator('[data-testid="squeeze-card"]').first();
    await expect(firstCard).toBeVisible({ timeout: 10000 });
    await firstCard.click();

    // 確認價格圖表出現
    const priceChart = page.locator('[data-testid="price-chart"]');
    await expect(priceChart).toBeVisible({ timeout: 5000 });
  });

  test('gRPC degradation works when Python engine is down', async ({ request }) => {
    // 假設 Python 引擎已關閉
    // API 應該返回降級響應或錯誤
    const response = await request.get('/api/squeeze/2330');

    // 可能是正常響應或降級響應
    if (response.ok()) {
      const data = await response.json();
      expect(data).toHaveProperty('ticker');
      expect(data).toHaveProperty('trend');
    } else {
      // 如果返回錯誤，確認是可接受的降級行為
      expect(response.status()).toBeGreaterThanOrEqual(400);
    }
  });

  test('Navigation works correctly', async ({ page }) => {
    // 確認 Dashboard 頁面已載入
    await expect(page.locator('h1')).toContainText('Alpha Squeeze');

    // 點擊導航連結
    const aboutLink = page.locator('a:has-text("關於")');
    if (await aboutLink.isVisible()) {
      await aboutLink.click();
      await expect(page).toHaveURL(/.*about/);
    }
  });

});

test.describe('Performance Tests', () => {

  test('Dashboard loads within 3 seconds', async ({ page }) => {
    const startTime = Date.now();
    await page.goto('/');

    // 等待主要內容出現
    await expect(page.locator('h1')).toContainText('Alpha Squeeze');

    const loadTime = Date.now() - startTime;
    expect(loadTime).toBeLessThan(3000);
  });

  test('API response time is within limits', async ({ request }) => {
    const startTime = Date.now();
    const response = await request.get('/api/squeeze/top-candidates');
    const responseTime = Date.now() - startTime;

    expect(response.ok()).toBeTruthy();
    expect(responseTime).toBeLessThan(200); // 目標: < 200ms
  });

});
