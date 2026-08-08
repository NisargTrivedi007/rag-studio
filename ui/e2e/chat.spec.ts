import { test, expect } from '@playwright/test';

test('full chat flow: type message → see response', async ({ page }) => {
  await page.goto('/');
  const input = page.getByPlaceholder('Ask about your documents...');
  await input.fill('Hello, can you help me?');
  await page.getByLabel('Send').click();

  await expect(page.getByText('Hello, can you help me?')).toBeVisible();
  await expect(page.locator('[class*="msg-animate"]').last()).toBeVisible({ timeout: 15_000 });
});
