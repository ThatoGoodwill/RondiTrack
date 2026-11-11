import { test, expect } from '@playwright/test'
import AxeBuilder from '@axe-core/playwright'
const tags = ['wcag2a','wcag2aa','wcag21a','wcag21aa','wcag22aa']

test('signed-in account is excluded from indexing and has no serious axe violations', async ({ page }) => {
  await page.goto('/dashboard/member')
  await expect(page.locator('meta[name="robots"]')).toHaveAttribute('content', /noindex/)
  const report = await new AxeBuilder({ page }).withTags(tags).analyze()
  expect(report.violations.filter(x => x.impact === 'critical' || x.impact === 'serious')).toEqual([])
})

test('stokvel detail and contribution flow are keyboard and axe accessible', async ({ page }) => {
  await page.goto('/stokvels')
  const link = page.locator('a[href^="/stokvels/"]').first()
  await expect(link).toBeVisible()
  await link.click()
  const report = await new AxeBuilder({ page }).withTags(tags).analyze()
  expect(report.violations.filter(x => x.impact === 'critical' || x.impact === 'serious')).toEqual([])
  const close = page.getByRole('button', { name: /Close cycle safely/i })
  if (await close.count() && await close.isVisible()) {
    await close.click()
    await expect(page.getByRole('dialog')).toBeVisible()
    await page.keyboard.press('Escape')
    await expect(page.getByRole('dialog')).not.toBeVisible()
    await expect(close).toBeFocused()
  }
})

test('signed-out BFF returns JSON 401, not HTML', async ({ request }) => {
  const response = await request.get('/bff/activity', { headers: { Cookie: 'session=' } })
  expect(response.status()).toBe(401)
  expect(response.headers()['content-type']).toContain('application/json')
})

test('invite metadata contains no member information', async ({ browser }) => {
  const isolated = await browser.newContext({ storageState: { cookies: [], origins: [] } })
  try {
    const page = await isolated.newPage()
    await page.goto('/join?code=abcdef1234567890&step=rules')
    const html = await page.locator('head').innerHTML()
    expect(html).not.toContain('Ubuntu Savers')
    expect(html).not.toContain('R 500')
  } finally { await isolated.close() }
})
