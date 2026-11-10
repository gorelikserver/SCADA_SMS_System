import { test, expect, type Page } from '@playwright/test';
import path from 'node:path';
import fs from 'node:fs';

// These tests load wwwroot/js/hebrew-ui.js into a static page (page.setContent + addScriptTag),
// so they need neither the web app nor network access.
const scriptPath = path.resolve(__dirname, '../wwwroot/js/hebrew-ui.js');

const fixture = `<!doctype html><html lang="he" dir="rtl"><head><title>t</title></head><body>
  <h1 id="heading">Delivery history</h1>
  <table><tbody id="activity"><tr><td>Loading recent activity…</td></tr></tbody></table>
  <div id="confirmModalTitle"></div><div id="confirmModalBody"></div>
  <button id="confirmModalOk">Delete</button>
  <button id="refresh">Refresh</button>
  <span id="alpine" x-text="loading ? 'Creating...' : 'Create team'">Create team</span>
  <p id="message" dir="auto" data-no-translate>Success</p>
  <div id="response" class="provider-response" data-no-translate>Failed</div>
  <span id="name" data-no-translate title="Success">Pending</span>
  <textarea id="payload">Success</textarea>
  <select id="role"><option value="Admin">Admin</option><option value="Viewer">Viewer</option></select>
  <select id="kind"><option>Success</option></select>
  <select id="status"><option value="SUCCESS">Success</option></select>
  <input id="search" value="Active" placeholder="Search" />
</body></html>`;

/**
 * Loads the translator and counts how often any MutationObserver callback runs, so a test can
 * prove that processing stops once the DOM is translated (no observer feedback loop).
 */
async function loadTranslator(page: Page) {
  await page.setContent(fixture);
  await page.evaluate(() => {
    const w = window as unknown as { __observerCalls: number };
    w.__observerCalls = 0;
    const Native = window.MutationObserver;
    window.MutationObserver = class extends Native {
      constructor(callback: MutationCallback) {
        super((records, observer) => { w.__observerCalls++; callback(records, observer); });
      }
    };
  });
  await page.addScriptTag({ path: scriptPath });
}

const observerCalls = (page: Page) =>
  page.evaluate(() => (window as unknown as { __observerCalls: number }).__observerCalls);

/** Waits several frames and returns the observer call count once it stopped changing. */
async function settledObserverCalls(page: Page) {
  let previous = -1;
  let current = await observerCalls(page);
  for (let i = 0; i < 20 && current !== previous; i++) {
    await page.waitForTimeout(50);
    previous = current;
    current = await observerCalls(page);
  }
  expect(current).toBe(previous);
  return current;
}

test('translates the initial page and leaves form values unchanged', async ({ page }) => {
  await loadTranslator(page);

  await expect(page.locator('#heading')).toHaveText('היסטוריית מסירה');
  await expect(page.locator('#activity td')).toHaveText('טוען פעילות אחרונה…');
  await expect(page.locator('#search')).toHaveAttribute('placeholder', 'חיפוש');
  await expect(page.locator('#search')).toHaveValue('Active');
  await expect(page.locator('#payload')).toHaveValue('Success');

  // <option>s with an explicit value attribute show translated text but post the same value.
  await expect(page.locator('#status option')).toHaveText('הצלחה');
  await expect(page.locator('#status')).toHaveValue('SUCCESS');
  await expect(page.locator('#role')).toHaveValue('Admin');
  // An <option> without a value posts its text, so its text must never change.
  await expect(page.locator('#kind option')).toHaveText('Success');
  await expect(page.locator('#kind')).toHaveValue('Success');
});

test('data cells whose text equals a dictionary key stay unchanged', async ({ page }) => {
  await loadTranslator(page);

  // 'Success', 'Failed' and 'Pending' are all dictionary keys.
  await expect(page.locator('#message')).toHaveText('Success');
  await expect(page.locator('#response')).toHaveText('Failed');
  await expect(page.locator('#name')).toHaveText('Pending');
  await expect(page.locator('#name')).toHaveAttribute('title', 'Success');

  // Data that arrives later (dashboard refresh) is protected the same way.
  await page.evaluate(() => {
    document.getElementById('activity')!.innerHTML =
      '<tr><td id="row-status">Success</td><td id="row-data" data-no-translate>Success</td></tr>';
  });
  await expect(page.locator('#row-status')).toHaveText('הצלחה');
  await expect(page.locator('#row-data')).toHaveText('Success');
  await page.evaluate(() => { document.getElementById('message')!.firstChild!.nodeValue = 'Failed'; });
  await page.waitForTimeout(50);
  await expect(page.locator('#message')).toHaveText('Failed');
});

test('translates appended and changed nodes and mutation processing terminates', async ({ page }) => {
  await loadTranslator(page);
  const baseline = await settledObserverCalls(page);

  // Index.cshtml: recent-activity refresh replaces tbody.innerHTML.
  await page.evaluate(() => {
    document.getElementById('activity')!.innerHTML =
      '<tr><td>No recent activity</td></tr><tr><td><span class="badge">Pending</span></td></tr>';
  });
  await expect(page.locator('#activity tr').first()).toHaveText('אין פעילות אחרונה');
  await expect(page.locator('#activity .badge')).toHaveText('ממתין');

  // main.js confirmModal: textContent on title, body and OK button.
  await page.evaluate(() => {
    document.getElementById('confirmModalTitle')!.textContent = 'Delete Records';
    document.getElementById('confirmModalBody')!.textContent = 'Processing...';
    document.getElementById('confirmModalOk')!.textContent = 'Delete';
  });
  await expect(page.locator('#confirmModalTitle')).toHaveText('מחיקת רשומות');
  await expect(page.locator('#confirmModalBody')).toHaveText('מעבד...');
  await expect(page.locator('#confirmModalOk')).toHaveText('מחיקה');

  // Button spinner: innerHTML swap and restore.
  await page.evaluate(() => {
    const button = document.getElementById('refresh')!;
    button.innerHTML = '<i class="fas fa-spinner"></i>Refreshing...';
  });
  await expect(page.locator('#refresh')).toHaveText('מרענן...');
  await page.evaluate(() => { document.getElementById('refresh')!.innerHTML = '<i class="fas fa-rotate"></i>Refresh'; });
  await expect(page.locator('#refresh')).toHaveText('רענון');

  // Alpine x-text writes textContent; a characterData write on an existing text node too.
  await page.evaluate(() => { document.getElementById('alpine')!.textContent = 'Creating...'; });
  await expect(page.locator('#alpine')).toHaveText('יוצר...');
  await page.evaluate(() => { document.getElementById('alpine')!.firstChild!.nodeValue = 'Create team'; });
  await expect(page.locator('#alpine')).toHaveText('יצירת צוות');

  // An attribute change is translated once.
  await page.evaluate(() => { document.getElementById('search')!.setAttribute('placeholder', 'Search recipients'); });
  await expect(page.locator('#search')).toHaveAttribute('placeholder', 'חיפוש נמענים');

  const afterUpdates = await settledObserverCalls(page);
  // Each external update above may wake the observer once (plus a few batched callbacks);
  // a feedback loop would grow without bound.
  expect(afterUpdates - baseline).toBeLessThanOrEqual(20);

  // Nothing changes on its own once translated: the count stays still.
  await page.waitForTimeout(300);
  expect(await observerCalls(page)).toBe(afterUpdates);
});

test('rewriting already-translated text does not queue more translator writes', async ({ page }) => {
  await loadTranslator(page);
  await settledObserverCalls(page);

  const writes = await page.evaluate(async () => {
    // Count characterData records seen by an independent observer for one external write.
    let records = 0;
    const counter = new MutationObserver(list => { records += list.length; });
    counter.observe(document.body, { characterData: true, subtree: true });
    document.getElementById('heading')!.firstChild!.nodeValue = 'Delivery alerts';
    await new Promise(resolve => setTimeout(resolve, 200));
    counter.disconnect();
    return { records, text: document.getElementById('heading')!.textContent };
  });

  expect(writes.text).toBe('התראות מסירה');
  // One record for the external write and one for the single translation write.
  expect(writes.records).toBe(2);
});

/** Returns the string keys of the `he` object literal, in source order, including duplicates. */
function dictionaryKeys(source: string): string[] {
  const start = source.indexOf('const he = {');
  expect(start).toBeGreaterThan(-1);
  const keys: string[] = [];
  let i = source.indexOf('{', start) + 1;
  let expectKey = true;
  for (; i < source.length; i++) {
    const ch = source[i];
    if (ch === '}') break;
    if (ch === "'" || ch === '"') {
      let value = '';
      for (i++; source[i] !== ch; i++) {
        if (source[i] === '\\') i++;
        value += source[i];
      }
      if (expectKey) keys.push(value);
    } else if (ch === ':') expectKey = false;
    else if (ch === ',') expectKey = true;
  }
  return keys;
}

test('Hebrew dictionary contains no duplicate keys', () => {
  const keys = dictionaryKeys(fs.readFileSync(scriptPath, 'utf8'));
  expect(keys.length).toBeGreaterThan(400);
  const duplicates = keys.filter((key, index) => keys.indexOf(key) !== index);
  expect(duplicates).toEqual([]);
  // Self-check of the scanner: a duplicated key is reported.
  expect(dictionaryKeys("const he = {'A':'x','B':'y','A':'z'};")).toEqual(['A', 'B', 'A']);
});
