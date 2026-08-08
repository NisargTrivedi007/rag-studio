import { test, expect } from '@playwright/test';

test('SQL flow: ask question → see SQL + results', async ({ page }) => {
  await page.goto('/sql');
  await page.getByPlaceholder('Ask a question about the data...').fill('How many customers are there?');
  await page.getByRole('button', { name: 'Ask' }).click();

  await expect(page.locator('pre code').first()).toBeVisible({ timeout: 15_000 });
  await expect(page.locator('table')).toBeVisible();
});
