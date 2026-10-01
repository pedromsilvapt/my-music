import {describe, expect, it} from 'vitest';
import {buildActionFilter, parseActionFilter} from './sync-record-action';

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
