import { SESSION_COUNTER_SLOTS, buildSessionCounterSlots, getSessionActionCount } from '../sessionCounters';
import { SyncSessionItemSchema } from '../../../api/types';
import type { SyncSessionItem } from '../../../api/types';

function createSession (counts: Partial<SyncSessionItem> = {}): SyncSessionItem {
    return {
        id: 1,
        startedAt: '2026-10-04T11:02:28Z',
        completedAt: '2026-10-04T11:02:31Z',
        status: 'Completed',
        isDryRun: false,
        createRemoteCount: 0,
        updateRemoteCount: 0,
        skippedCount: 0,
        createLocalCount: 0,
        updateLocalCount: 0,
        deleteLocalCount: 0,
        linkCount: 0,
        unlinkCount: 0,
        renameCount: 0,
        conflictCount: 0,
        updateTimestampCount: 0,
        errorCount: 0,
        repositoryPath: null,
        ...counts,
    };
}

describe('SESSION_COUNTER_SLOTS', () => {
    it('should count every session count field in exactly one sub-slot', () => {
        const countFields = Object.keys(SyncSessionItemSchema.shape).filter((key) => key.endsWith('Count')).sort();
        const slotFields = SESSION_COUNTER_SLOTS.flatMap((slot) => slot.subSlots.map((subSlot) => subSlot.field)).sort();

        expect(countFields).toHaveLength(12);
        expect(slotFields).toEqual(countFields);
    });

    it('should have the six slots in display order', () => {
        expect(SESSION_COUNTER_SLOTS.map((slot) => slot.key)).toEqual(['remote', 'local', 'links', 'conflict', 'error', 'skipped']);
    });
});

describe('buildSessionCounterSlots', () => {
    it('should return six slots without parts when the session has no records', () => {
        const slots = buildSessionCounterSlots(createSession());

        expect(slots).toHaveLength(6);
        for (const slot of slots) {
            expect(slot.total).toBe(0);
            expect(slot.parts).toEqual([]);
        }
    });

    it('should only include the sub-slots that have records', () => {
        const slots = buildSessionCounterSlots(createSession({
            createLocalCount: 3,
            updateLocalCount: 1,
            updateTimestampCount: 6,
            conflictCount: 1,
        }));

        const byKey = Object.fromEntries(slots.map((slot) => [slot.key, slot]));

        expect(byKey.remote.parts).toEqual([]);
        expect(byKey.local.total).toBe(4);
        expect(byKey.local.parts.map((part) => [part.action, part.value])).toEqual([['CreateLocal', 3], ['UpdateLocal', 1]]);
        expect(byKey.links.total).toBe(6);
        expect(byKey.links.parts.map((part) => [part.action, part.value])).toEqual([['UpdateTimestamp', 6]]);
        expect(byKey.conflict.parts.map((part) => [part.action, part.value])).toEqual([['Conflict', 1]]);
        expect(byKey.error.parts).toEqual([]);
        expect(byKey.skipped.parts).toEqual([]);
    });

    it('should give each sub-slot of a slot a different color', () => {
        for (const slot of SESSION_COUNTER_SLOTS) {
            const colorKeys = slot.subSlots.map((subSlot) => subSlot.colorKey);
            expect(new Set(colorKeys).size).toBe(colorKeys.length);
        }
    });

    it('should count renames under the local slot', () => {
        const slots = buildSessionCounterSlots(createSession({ renameCount: 2 }));

        const local = slots.find((slot) => slot.key === 'local')!;
        expect(local.parts.map((part) => [part.action, part.value])).toEqual([['Rename', 2]]);
    });
});

describe('getSessionActionCount', () => {
    const session = createSession({
        createRemoteCount: 1,
        updateRemoteCount: 2,
        skippedCount: 3,
        createLocalCount: 4,
        updateLocalCount: 5,
        deleteLocalCount: 6,
        linkCount: 7,
        unlinkCount: 8,
        renameCount: 9,
        conflictCount: 10,
        updateTimestampCount: 11,
        errorCount: 12,
    });

    it.each([
        ['CreateRemote', 1],
        ['UpdateRemote', 2],
        ['Skipped', 3],
        ['CreateLocal', 4],
        ['UpdateLocal', 5],
        ['DeleteLocal', 6],
        ['Link', 7],
        ['Unlink', 8],
        ['Rename', 9],
        ['Conflict', 10],
        ['UpdateTimestamp', 11],
        ['Error', 12],
    ])('should return the count of %s records', (action, expected) => {
        expect(getSessionActionCount(session, action)).toBe(expected);
    });

    it('should ignore the action casing', () => {
        expect(getSessionActionCount(session, 'deletelocal')).toBe(6);
    });

    it('should return 0 for an unknown action', () => {
        expect(getSessionActionCount(session, 'Delete')).toBe(0);
    });
});
