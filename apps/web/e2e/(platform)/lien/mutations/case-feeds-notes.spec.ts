import { createMutationTest, expect } from '../../../support/mutation-test';
import { getEnv } from '../../../config/environments';

/**
 * Covers the "Feeds" widget (src/app/(platform)/lien/cases/[id]/components/feeds-section.tsx)
 * on a case's Details tab: adding a note, filtering (Newest / Oldest / Show Deleted Comment —
 * a single-select, mutually-exclusive filter, not independent toggles), deleting a note
 * through its confirm dialog, and that each note's timestamp renders via DateDisplay (legacy
 * "MM/DD/YYYY, H:MM AM/PM" in the tenant's timezone) rather than the raw UTC string the
 * get-notes API returns. Also covers the Email tab's "Compose New Email" placeholder toast.
 *
 * Which case this runs against isn't the point of this spec — it just needs *a* case to attach
 * notes to, and case creation isn't covered by e2e yet (tracked separately). So this test picks
 * whatever the first row in /lien/cases happens to be. If the tenant has zero cases, the test
 * skips itself (not-covered, not a failure) rather than trying to create one — re-visit once
 * case-creation e2e coverage exists.
 *
 * Note existence/visibility is always asserted via `noteParagraph()`, scoped to the rendered
 * `p.whitespace-pre-wrap` list items — never `feedsCard.getByText()` directly. Playwright's
 * getByText() also matches an <input>/<textarea>'s current *value*, and the "Add a note"
 * textarea sitting right below the list means a naive getByText(noteText) can resolve against
 * whatever's still typed in the box rather than the note actually having round-tripped through
 * the list. That false-positive caused a real race here: the assertion for note A was
 * "satisfied" before its add-note/get-notes cycle actually finished, so note B got typed and
 * submitted while note A's own `submitting` state was still in flight — and note A's delayed
 * `setNoteText("")` clobbered the just-typed note B text out from under it.
 *
 * Mutation spec: creates and deletes real case notes against local/qa only.
 */

const test = createMutationTest('lien');
const env = getEnv();

test.describe(`SynqLien case Feeds — notes add/filter/delete, email placeholder [${env.name}]`, () => {
  test('adds, filters, and deletes a note; email tab shows the coming-soon toast', async ({
    page,
    credentials,
  }) => {
    await page.goto(`${env.originFor(credentials.tenantCode)}/lien/cases`);

    // BaseTable renders a single "Loading..." placeholder row while the
    // initial fetch is in flight, then swaps it for either real rows or a
    // "No cases match your filters." empty row — wait past that swap
    // before deciding whether a case exists to click into.
    await page.waitForFunction(
      () => {
        const rows = document.querySelectorAll('table tbody tr');
        return rows.length > 0 && !(rows[0].textContent ?? '').includes('Loading...');
      },
      { timeout: 15_000 },
    );

    // Bail out gracefully if the tenant has no cases yet — case creation isn't
    // e2e-covered yet, so there's nothing to attach notes to here.
    const firstRow = page.locator('table tbody tr').first();
    const hasCase = !(await firstRow.locator('td[colspan]').isVisible().catch(() => false));
    test.skip(!hasCase, 'No cases exist in this environment yet — covered once case creation is e2e-tested.');

    await firstRow.locator('td').first().click();
    await page.waitForURL(/\/lien\/cases\/[^/]+\/details$/, { timeout: 20_000 });

    // Scope every subsequent query to the Feeds card so "Notes"/"Email" here
    // never collide with the case-level Details/Notes tab bar above it.
    const feedsCard = page
      .getByRole('heading', { name: 'Feeds', exact: true })
      .locator('xpath=ancestor::div[contains(@class, "border-gray-200")][1]');
    await expect(feedsCard).toBeVisible();

    const notesTabButton = feedsCard.getByRole('button', { name: 'Notes', exact: true });
    const emailTabButton = feedsCard.getByRole('button', { name: 'Email', exact: true });
    const filterTrigger = feedsCard.getByRole('button', { name: 'Select Filter' });
    const noteInput = feedsCard.getByPlaceholder('Add a note');
    const submitButton = feedsCard.getByRole('button', { name: 'Submit' });

    // Note text list items only — see file-level comment on why this isn't
    // feedsCard.getByText() directly.
    function noteParagraph(text: string) {
      return feedsCard.locator('p.whitespace-pre-wrap', { hasText: text });
    }

    // Shared by deleteNote() and the timestamp assertions below — climbs from
    // the note body paragraph to its enclosing row via ancestor:: (see
    // deleteNote's original comment on why a CSS `.filter({has})` chain off
    // an xpath-rooted locator was unreliable here).
    function noteRow(text: string) {
      return noteParagraph(text).locator('xpath=ancestor::div[contains(@class, "group")][1]');
    }

    async function chooseFilter(label: 'Newest' | 'Oldest' | 'Show Deleted Comment') {
      await filterTrigger.click();
      // Not `exact: true` — the currently-selected option's accessible name picks
      // up a trailing space from the adjacent (empty) checkmark icon.
      await feedsCard.getByRole('button', { name: label }).click();
    }

    // Fills, submits, and waits for the *list* (not the textarea) to reflect the
    // new note before returning — see file-level comment for why this matters.
    async function addNote(text: string) {
      await noteInput.fill(text);
      await submitButton.click();
      await expect(noteParagraph(text)).toBeVisible();
      await expect(noteInput).toHaveValue('');
    }

    async function deleteNote(text: string) {
      // Filter switches trigger their own async refetch (loadNotes) — wait for
      // the note to actually be on-screen under the current filter before
      // trying to locate/hover its row, rather than racing the refetch.
      await expect(noteParagraph(text)).toBeVisible();
      const row = noteRow(text);
      await row.hover();
      await row.getByRole('button', { name: 'Delete note' }).click();

      const confirmDialog = page.getByRole('dialog');
      await expect(confirmDialog.getByRole('heading', { name: 'Delete Note' })).toBeVisible();
      await confirmDialog.getByRole('button', { name: 'Delete', exact: true }).click();
      await expect(confirmDialog).toBeHidden();
    }

    await expect(notesTabButton).toBeVisible();
    // Default state on load: Newest, non-deleted.
    await expect(filterTrigger).toHaveText('Newest');

    // --- Add two notes, sequentially, so relative order is deterministic
    // regardless of whatever other notes already exist on this (shared,
    // reused-across-runs) case.
    const stamp = Date.now();
    const noteA = `E2E Feeds note A ${stamp}`;
    const noteB = `E2E Feeds note B ${stamp}`;

    await addNote(noteA);
    await addNote(noteB);

    // --- Timestamp: DateDisplay renders each note's "created" time in the
    // tenant's configured timezone, in the legacy "MM/DD/YYYY, H:MM AM/PM"
    // format — never the raw ISO/UTC string the get-notes API returns (that
    // was the bug: the feed used to print note.created verbatim, always UTC
    // regardless of the viewer's tenant).
    const LEGACY_TIMESTAMP_PATTERN = /^\d{2}\/\d{2}\/\d{4}, \d{1,2}:\d{2}\s?(AM|PM)$/i;
    const timestampText = await noteRow(noteA).locator('p.text-xs.text-gray-400').innerText();
    expect(timestampText).toMatch(LEGACY_TIMESTAMP_PATTERN);
    expect(timestampText).not.toMatch(/^\d{4}-\d{2}-\d{2}T/);

    // --- Filtering: Newest puts the just-created B above A; Oldest reverses it.
    async function indexOf(text: string): Promise<number> {
      const all = await feedsCard.locator('p.whitespace-pre-wrap').allTextContents();
      return all.indexOf(text);
    }

    await chooseFilter('Newest');
    await expect(filterTrigger).toHaveText('Newest');
    await expect(noteParagraph(noteB)).toBeVisible();
    expect(await indexOf(noteB)).toBeLessThan(await indexOf(noteA));

    await chooseFilter('Oldest');
    await expect(filterTrigger).toHaveText('Oldest');
    await expect(noteParagraph(noteA)).toBeVisible();
    expect(await indexOf(noteA)).toBeLessThan(await indexOf(noteB));

    // Filter is single-select — Show Deleted Comment must not carry Oldest
    // along with it, and neither non-deleted note should show up there yet.
    await chooseFilter('Show Deleted Comment');
    await expect(filterTrigger).toHaveText('Show Deleted Comment');
    await expect(noteParagraph(noteA)).toBeHidden();
    await expect(noteParagraph(noteB)).toBeHidden();

    // --- Delete note B via the confirm dialog.
    await chooseFilter('Newest');
    await deleteNote(noteB);

    // Deleted note drops out of the default (non-deleted) view...
    await expect(noteParagraph(noteB)).toBeHidden();
    await expect(noteParagraph(noteA)).toBeVisible();

    // ...and shows up under Show Deleted Comment, with A still absent there.
    await chooseFilter('Show Deleted Comment');
    await expect(noteParagraph(noteB)).toBeVisible();
    await expect(noteParagraph(noteA)).toBeHidden();

    // Clean up the remaining note so the shared QA case doesn't accumulate
    // e2e noise across repeated runs.
    await chooseFilter('Newest');
    await deleteNote(noteA);
    await expect(noteParagraph(noteA)).toBeHidden();

    // --- Email tab: "Compose New Email" is a placeholder — confirm the toast, not a real send.
    await emailTabButton.click();
    await feedsCard.getByRole('button', { name: 'Compose New Email' }).click();
    await expect(page.getByText('Coming Soon')).toBeVisible();
    await expect(
      page.getByText("Composing email from here isn't available yet."),
    ).toBeVisible();
  });
});
