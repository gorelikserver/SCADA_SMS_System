/**
 * SOAP SMS Gateway — Playwright E2E regression tests
 *
 * HOW TO RUN:
 *   1. Install deps (once):
 *        npm install
 *        npx playwright install chromium
 *   2. Start the app in another terminal:
 *        dotnet run --project SCADASMSSystem.Web.csproj
 *   3. Run the tests:
 *        npm run test:e2e
 *      or with headed browser:
 *        npm run test:e2e:headed
 *
 * The tests configure all required state through the Settings UI —
 * no manual appsettings.json edits required.
 *
 * Covered scenarios:
 *   S1 — TestMode live-reload (ISSUE-001): mock gate honours IOptionsMonitor
 *   S2 — SOAP workbench auto-refresh (ISSUE-002): template loads from Settings
 *   S3 — SOAP KV placeholder UI (ISSUE-003): hover border + select tooltip + literal switch
 *   S4 — Settings save + reload round-trip: SoapAction persists across reload
 *   S5 — SOAP workbench fire to mock: end-to-end 200 success via mock server
 */

import { test, expect, type Page } from '@playwright/test';

// ── Helpers ────────────────────────────────────────────────────────────────────

/** Remove the Settings localStorage draft so Alpine reads fresh server state. */
async function clearDraft(page: Page): Promise<void> {
  await page.evaluate(() => localStorage.removeItem('scada_settings_draft'));
}

/**
 * Wait for the Bootstrap confirmModal to become visible, click OK, then wait
 * for the resulting full-page form submission to settle.
 * The toggles (provider switch, TestMode) call _autoSaveAfterToggle() on confirm,
 * which submits the form and reloads /Settings.
 */
async function acceptModal(page: Page): Promise<void> {
  const modal = page.locator('#confirmModal');
  await modal.waitFor({ state: 'visible', timeout: 5000 });
  await page.locator('#confirmModalOk').click();
  // _autoSaveAfterToggle delays form.submit() by 50ms via setTimeout.
  // waitForLoadState('networkidle') resolves during that gap before the form fires.
  // Wait for .alert-success instead — only present after the POST completes and page reloads.
  await page.locator('.alert-success').waitFor({ state: 'visible', timeout: 10_000 });
}

/**
 * Dismiss any Bootstrap confirmModal that may be open (e.g. left over by a
 * failed previous test step).  No-op when no modal is visible.
 */
async function dismissModalIfOpen(page: Page): Promise<void> {
  try {
    const modal = page.locator('#confirmModal');
    if (await modal.isVisible({ timeout: 500 })) {
      // Click the backdrop close button (X)
      await page.locator('#confirmModal .btn-close').first().click();
      await modal.waitFor({ state: 'hidden', timeout: 3000 });
    }
  } catch {
    // no modal visible — fine
  }
}

/**
 * Navigate to /Settings and switch to SOAP provider if not already active.
 * Provider switch triggers a confirmation modal + auto-save, so the page
 * reloads at the end.
 */
async function ensureSoap(page: Page): Promise<void> {
  await page.goto('/Settings');
  await clearDraft(page);

  const soapBtn = page.locator('button.pm-toggle-btn').filter({ hasText: 'SOAP' });
  const isActive = await soapBtn.evaluate((el) => el.classList.contains('active'));
  if (!isActive) {
    await soapBtn.click();
    await acceptModal(page); // confirm switch + wait for auto-save reload
  }
}

/**
 * Enable or disable TestMode via the Settings → Test Mode tab.
 * If the state is already as requested, does nothing.
 * Toggling triggers a confirmation modal + auto-save.
 */
async function setTestMode(page: Page, enable: boolean): Promise<void> {
  await page.goto('/Settings');
  await clearDraft(page);

  await page.locator('#tab-testmode').click();
  const toggle = page.locator('#testModeSwitch');
  await toggle.waitFor({ state: 'visible' });

  const current = await toggle.isChecked();
  if (current === enable) return; // already in the desired state

  await toggle.click();
  await acceptModal(page); // confirm toggle + wait for auto-save reload
}

// ── Tests ──────────────────────────────────────────────────────────────────────

test.describe('SOAP SMS Gateway — bug regression suite', () => {

  test.beforeEach(async ({ page }) => {
    // Clean slate: navigate to Settings and dismiss any stale modals / drafts.
    await page.goto('/Settings');
    await dismissModalIfOpen(page);
    await clearDraft(page);
  });

  test.afterEach(async ({ page }) => {
    // Always restore TestMode=false so subsequent tests start in a known state.
    await setTestMode(page, false);
  });

  // ── Scenario 1 ──────────────────────────────────────────────────────────────
  test('S1 — TestMode live-reload: mock gate opens/closes without restart', async ({ page, request }) => {
    // Setup: SOAP mode + TestMode ON
    await ensureSoap(page);
    await setTestMode(page, true);

    const soapEnvelope = `<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
  <soap:Body>
    <SendSMS><Phone>+1234567890</Phone><Message>Playwright test</Message></SendSMS>
  </soap:Body>
</soap:Envelope>`;

    // With TestMode ON: mock endpoint should return 200
    let resp = await request.post('/mock/sms/soap', {
      headers: { 'Content-Type': 'text/xml', SOAPAction: '"test"' },
      data: soapEnvelope,
    });
    expect(resp.status(), 'mock/sms/soap should be 200 when TestMode is ON').toBe(200);

    // Disable TestMode via Settings UI
    await setTestMode(page, false);

    // With TestMode OFF: mock endpoint must return 503
    resp = await request.post('/mock/sms/soap', {
      headers: { 'Content-Type': 'text/xml', SOAPAction: '"test"' },
      data: soapEnvelope,
    });
    expect(resp.status(), 'mock/sms/soap should be 503 when TestMode is OFF').toBe(503);
    const body = await resp.json();
    expect(body.error).toBe('Mock endpoint only active in TestMode');
  });

  // ── Scenario 2 ──────────────────────────────────────────────────────────────
  test('S2 — SOAP workbench auto-refresh: saved template appears in Assembled Envelope Preview', async ({ page }) => {
    const template = '<Test><Message>{Message}</Message></Test>';

    // Setup: SOAP mode + save the template
    await ensureSoap(page);
    await page.goto('/Settings');
    await clearDraft(page);

    // Fill the SOAP Body Template textarea (visible only in SOAP mode)
    const templateArea = page.locator('#soapTemplateArea');
    await templateArea.waitFor({ state: 'visible' });
    await templateArea.fill(template);

    // Save settings (standard save button — not the auto-save toggle path)
    await page.locator('#saveBtn').click();
    await page.waitForLoadState('networkidle');

    // Navigate to /Test/Sms — server renders SettingsJson including soapBodyTemplate
    await page.goto('/Test/Sms');
    await page.waitForLoadState('domcontentloaded');

    // SOAP Workbench tab is auto-active when providerType=SOAP; click it to be sure
    const workbenchTabBtn = page.locator('#tab-soap-workbench-btn');
    if (!(await workbenchTabBtn.evaluate((el) => el.classList.contains('active')))) {
      await workbenchTabBtn.click();
    }

    // Wait for Alpine to finish initialising (reads SettingsJson, builds assembledEnvelope)
    const noTemplateWarning = page.locator('#tab-soap-workbench .alert-warning').filter({
      hasText: 'No SOAP Body Template configured',
    });
    await expect(noTemplateWarning).not.toBeVisible({ timeout: 5000 });

    // The Assembled Envelope Preview <pre> should contain the body template tag
    const envelopePreview = page.locator('pre[x-text="assembledEnvelope"]');
    await expect(envelopePreview).toContainText('<Test>', { timeout: 5000 });

    // Click "Load from Settings" sync button — verifies loadFromSettings() path (ISSUE-002 fix)
    await page.locator('#tab-soap-workbench button[title="Reload from saved settings"]').click();
    await page.waitForTimeout(500); // let the async fetch settle

    await expect(envelopePreview).toContainText('<Test>');
  });

  // ── Scenario 3 ──────────────────────────────────────────────────────────────
  test('S3 — SOAP KV placeholder: hover border, Phone row select title, literal switch', async ({ page }) => {
    await ensureSoap(page);
    await page.goto('/Settings');
    await clearDraft(page);

    // Ensure the SOAP section is visible
    const soapSection = page.locator('[x-show]').filter({ hasText: 'Body Parameters' }).first();
    await soapSection.waitFor({ state: 'visible' });

    // ── Part A: hover over a SOAP placeholder key input → border should appear ──
    //    .pm-kv-key inputs have border-bottom:1px solid transparent normally;
    //    on hover the CSS rule changes to border-bottom-color:#ced4da.
    //    Must scope to SOAP table — REST table rows share the same classes but are hidden (display:none).
    //    .first() without scoping picks the hidden REST row (offsetParent=null) → hover times out.
    const soapParamTable = page.locator('.pm-kv-table').filter({ hasText: 'Placeholder' });
    const firstKeyInput = soapParamTable.locator('input.pm-kv-key').first();
    await firstKeyInput.hover();
    await page.waitForTimeout(250); // CSS border-color transition is .15s — wait for it to settle

    const borderColor = await firstKeyInput.evaluate(
      (el) => getComputedStyle(el).borderBottomColor,
    );
    // Any non-transparent color is acceptable — original bug was border:none
    expect(
      borderColor,
      'key input border-bottom should be non-transparent on hover',
    ).not.toMatch(/^rgba\(0,\s*0,\s*0,\s*0\)/);
    expect(borderColor, 'key input border-bottom should not be transparent').not.toBe('transparent');

    // ── Part B: find a dynamic row (isDynamic=true), assert select title contains "↩ literal" ──
    //    Dynamic rows render a <select class="pm-kv-val-select-sm"> with the tooltip.
    //    Scope to soapParamTable to avoid iterating hidden REST rows.
    const allKvRows = soapParamTable.locator('.pm-kv-row');
    const rowCount = await allKvRows.count();
    let dynRowIndex = -1;
    for (let i = 0; i < rowCount; i++) {
      const dynSelect = allKvRows.nth(i).locator('select.pm-kv-val-select-sm');
      if ((await dynSelect.count()) > 0 && (await dynSelect.isVisible())) {
        dynRowIndex = i;
        break;
      }
    }
    expect(dynRowIndex, 'A dynamic (isDynamic=true) row must exist in SOAP param table').not.toBe(-1);

    const dynRow = allKvRows.nth(dynRowIndex);
    const valueSelect = dynRow.locator('select.pm-kv-val-select-sm');
    await expect(valueSelect).toBeVisible();

    const title = await valueSelect.getAttribute('title');
    expect(title, 'select title should mention ↩ literal').toContain('↩ literal');

    // ── Part C: add a new row, make it dynamic by typing a DYNAMIC_KEYWORD, then switch to literal ──
    //    "Message" is mandatory → its select is always disabled; can't test the switch there.
    //    Add a fresh non-mandatory row, type "phone" (a DYNAMIC_KEYWORD) → onValueChange sets
    //    isDynamic=true → x-if="row.isDynamic" select appears → select "" (↩ literal) → isDynamic=false.
    const addRowBtn = page.locator('.pm-add-row-btn').filter({ hasText: 'Add Placeholder' });
    await addRowBtn.click();
    await page.waitForTimeout(200); // Alpine adds the row

    // Verify new row's static input is visible (isDynamic=false initially)
    const newRow = soapParamTable.locator('.pm-kv-row').last();
    const newValInput = newRow.locator('input.pm-kv-val-input');
    await expect(newValInput).toBeVisible();

    // fill() fires native input events — Alpine @input="onValueChange(row)" runs automatically.
    // "phone" is a DYNAMIC_KEYWORD → isDynamic=true → x-if="!row.isDynamic" input disappears.
    await newValInput.fill('phone');
    await page.waitForTimeout(300); // Alpine reactivity flush

    // Verify the x-if="row.isDynamic" select appeared
    const newSelect = newRow.locator('select.pm-kv-val-select-sm');
    await expect(newSelect).toBeVisible({ timeout: 3000 });

    // Alpine v3 x-if init sets :disabled on first render due to stale scope evaluation.
    // Dispatch 'change' via evaluate to trigger onValueChange(row) → isDynamic=false.
    await page.evaluate(() => {
      const tables = Array.from(document.querySelectorAll('.pm-kv-table'));
      const soapTable = tables.find(t => t.textContent.includes('Placeholder'));
      if (!soapTable) return;
      const rows = soapTable.querySelectorAll('.pm-kv-row');
      const lastRow = rows[rows.length - 1];
      const sel = lastRow.querySelector('select.pm-kv-val-select-sm');
      if (sel) { sel.value = ''; sel.dispatchEvent(new Event('change', { bubbles: true })); }
    });
    await page.waitForTimeout(300); // Alpine reactivity flush

    // isDynamic=false → x-if="row.isDynamic" select removed, x-if="!row.isDynamic" input restored
    await expect(newRow.locator('input.pm-kv-val-input')).toBeVisible({ timeout: 3000 });
    await expect(newSelect).not.toBeVisible();
  });

  // ── Scenario 4 ──────────────────────────────────────────────────────────────
  test('S4 — Settings save + reload round-trip: SoapAction persists', async ({ page }) => {
    await ensureSoap(page);
    await page.goto('/Settings');
    await clearDraft(page);

    const soapActionInput = page.locator('input[placeholder="http://your-provider.com/SendSMS"]');
    await expect(soapActionInput).toBeVisible();

    // Set a unique test value
    await soapActionInput.fill('TestAction_E2E');

    // Save via the standard Save Settings button
    await page.locator('#saveBtn').click();
    await page.waitForLoadState('networkidle');

    // Reload the page — alpine reads from server (providerType), then from draft.
    // Clear the draft so the reload exercises the server-side persistence path.
    await clearDraft(page);
    await page.reload();
    await page.waitForLoadState('domcontentloaded');

    // The SOAP section should show the persisted SoapAction value
    await expect(soapActionInput).toHaveValue('TestAction_E2E', { timeout: 5000 });

    // Cleanup: reset SoapAction to empty
    await soapActionInput.fill('');
    await page.locator('#saveBtn').click();
    await page.waitForLoadState('networkidle');
  });

  // ── Scenario 5 ──────────────────────────────────────────────────────────────
  test('S5 — SOAP workbench fire to mock: HTTP 200 + success response', async ({ page }) => {
    const template = '<SendSMS><Phone>{Phone}</Phone><Message>{Message}</Message></SendSMS>';

    // Setup: SOAP mode + save template + TestMode ON
    await ensureSoap(page);
    await page.goto('/Settings');
    await clearDraft(page);

    const templateArea = page.locator('#soapTemplateArea');
    await templateArea.waitFor({ state: 'visible' });
    await templateArea.fill(template);
    await page.locator('#saveBtn').click();
    await page.waitForLoadState('networkidle');

    await setTestMode(page, true);

    // Navigate to SOAP Workbench
    await page.goto('/Test/Sms');
    await page.waitForLoadState('domcontentloaded');

    // Activate the SOAP Workbench tab
    const workbenchTabBtn = page.locator('#tab-soap-workbench-btn');
    if (!(await workbenchTabBtn.evaluate((el) => el.classList.contains('active')))) {
      await workbenchTabBtn.click();
    }

    // Wait for Alpine init and template token inputs to render
    const msgInput = page.locator('#tab-soap-workbench input[placeholder="Test alarm message"]');
    await msgInput.waitFor({ state: 'visible', timeout: 5000 });

    // Click "Mock Server" button (switches targetMock = true)
    await page.locator('#tab-soap-workbench button').filter({ hasText: 'Mock Server' }).click();
    await page.waitForTimeout(200);

    // Fill placeholder inputs
    await msgInput.fill('Playwright E2E test');
    await page.locator('#tab-soap-workbench input[placeholder="+972501234567"]').fill('+1234567890');

    // Click Send and wait for response
    const sendBtn = page.locator('#tab-soap-workbench button.btn-warning').filter({ hasText: /Send/ });
    await sendBtn.click();

    // Assert status badge shows 200 OK
    await expect(
      page.locator('#tab-soap-workbench span.badge').filter({ hasText: '200 OK' }),
    ).toBeVisible({ timeout: 15_000 });

    // Assert success badge shows ✓ SUCCESS
    await expect(
      page.locator('#tab-soap-workbench span.badge').filter({ hasText: '✓ SUCCESS' }),
    ).toBeVisible({ timeout: 5000 });
  });
});
