import {describe, expect, it} from 'vitest';
import type {SyncSessionItem} from '../../model';
import {buildActionFilter, isActionFilterable, parseActionFilter} from './sync-record-action';

describe('buildActionFilter', () => {
    it('returns an empty expression when no actions are selected', () => {
        expect(buildActionFilter([])).toBe('');
    });

    it('uses equality for a single action', () => {
        expect(buildActionFilter(['Conflict'])).toBe('action = "Conflict"');
    });

    it('uses "in" for multiple actions, in enum order', () => {
        expect(buildActionFilter(['Error', 'CreateRemote'])).toBe('action in ["CreateRemote", "Error"]');
    });
});

describe('parseActionFilter', () => {
    it('returns an empty list for an empty expression', () => {
        expect(parseActionFilter('  ')).toEqual([]);
    });

    it('parses a single action equality, ignoring case and whitespace', () => {
        expect(parseActionFilter('  ACTION="conflict" ')).toEqual(['Conflict']);
    });

    it('parses an "in" list of actions', () => {
        expect(parseActionFilter('action in ["CreateRemote", "Error"]')).toEqual(['CreateRemote', 'Error']);
    });

    it('round-trips expressions produced by buildActionFilter', () => {
        expect(parseActionFilter(buildActionFilter(['Link', 'Skipped', 'Rename']))).toEqual(['Link', 'Rename', 'Skipped']);
    });

    it('returns null for unknown actions', () => {
        expect(parseActionFilter('action = "Nope"')).toBeNull();
        expect(parseActionFilter('action in ["Error", "Nope"]')).toBeNull();
    });

    it('returns null for custom expressions', () => {
        expect(parseActionFilter('filePath contains "abc"')).toBeNull();
        expect(parseActionFilter('action = "Error" and filePath contains "abc"')).toBeNull();
        expect(parseActionFilter('action != "Error"')).toBeNull();
    });
});

describe('isActionFilterable', () => {
    const session = {createRemoteCount: 2, skippedCount: 0, conflictCount: 0} as SyncSessionItem;

    it('is true for an action with records', () => {
        expect(isActionFilterable(session, 'CreateRemote')).toBe(true);
    });

    it('is false for an action with no records', () => {
        expect(isActionFilterable(session, 'Skipped')).toBe(false);
    });

    it('is false for Skipped when a completed session did not keep its skipped records', () => {
        const completed = {status: 'Completed', skippedCount: 3, recordSkipped: false} as SyncSessionItem;
        expect(isActionFilterable(completed, 'Skipped')).toBe(false);
        expect(isActionFilterable({...completed, recordSkipped: true}, 'Skipped')).toBe(true);
        expect(isActionFilterable({...completed, status: 'Committed'}, 'Skipped')).toBe(true);
    });

    it('is true for Conflict with a zero count, as resolved conflicts are not counted', () => {
        expect(isActionFilterable(session, 'Conflict')).toBe(true);
    });
});
