import { test as setup, expect } from '@playwright/test'
import path from 'node:path'
setup('authenticate seeded member once', async ({ page }) => {
  const password = process.env.E2E_SEED_PASSWORD
  if (!password) throw new Error('Set E2E_SEED_PASSWORD to the Compose demo seed password.')
  await page.goto('/login')
  await page.getByRole('textbox', { name: 'Email' }).fill('sipho.dlamini@example.com')
  await page.getByLabel('Password').fill(password)
  await page.getByRole('button', { name: 'Sign in' }).click()
  await expect(page).toHaveURL(/\/dashboard\/member/)
  await page.context().storageState({ path: path.join('e2e', '.auth', 'member.json') })
})
