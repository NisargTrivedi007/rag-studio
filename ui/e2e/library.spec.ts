import { test, expect } from '@playwright/test';
import path from 'path';

test('library flow: upload doc → appears in list → delete', async ({ page }) => {
  await page.goto('/library');
  await expect(page.getByText('Document Library')).toBeVisible();

  const fileInput = page.locator('input[type="file"]');
  await fileInput.setInputFiles(path.join(__dirname, 'fixtures/test.txt'));

  await expect(page.getByText('test.txt')).toBeVisible({ timeout: 10_000 });

  await page.getByLabel(/Delete/).first().click();
  await expect(page.getByText('test.txt')).not.toBeVisible({ timeout: 5_000 });
});
