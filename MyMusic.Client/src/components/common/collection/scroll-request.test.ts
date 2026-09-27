import {describe, expect, it} from 'vitest';
import {
    getScrollTargetIndex,
    initialScrollRequestState,
    type ScrollRequestAction,
    scrollRequestReducer,
    type ScrollRequestState,
} from './scroll-request';

function run(...actions: ScrollRequestAction[]): ScrollRequestState {
    return actions.reduce(scrollRequestReducer, initialScrollRequestState);
}

const items = [{id: 1}, {id: 2}, {id: 3}];
const keyOf = (item: { id: number }) => item.id;

describe('scrollRequestReducer', () => {
    it('starts a pending request when one is issued', () => {
        const state = run({type: 'issue', key: 2});

        expect(state.active).toEqual({key: 2, id: 1, phase: 'pending'});
    });

    it('lets the latest request win', () => {
        const state = run({type: 'issue', key: 2}, {type: 'issue', key: 3});

        expect(state.active).toEqual({key: 3, id: 2, phase: 'pending'});
    });

    it('starts flashing once the view has scrolled', () => {
        const state = run({type: 'issue', key: 2}, {type: 'scrolled', id: 1});

        expect(state.active?.phase).toBe('flashing');
    });

    it('ignores a scroll reported for an older request', () => {
        const state = run({type: 'issue', key: 2}, {type: 'issue', key: 3}, {type: 'scrolled', id: 1});

        expect(state.active).toEqual({key: 3, id: 2, phase: 'pending'});
    });

    it('clears the request when the flash ends', () => {
        const state = run({type: 'issue', key: 2}, {type: 'scrolled', id: 1}, {type: 'flashEnded', id: 1});

        expect(state.active).toBeNull();
    });

    it('ignores the end of an older flash', () => {
        const state = run(
            {type: 'issue', key: 2}, {type: 'scrolled', id: 1},
            {type: 'issue', key: 3}, {type: 'flashEnded', id: 1});

        expect(state.active).toEqual({key: 3, id: 2, phase: 'pending'});
    });

    it('keeps ids unique after a request is cleared', () => {
        const state = run(
            {type: 'issue', key: 2}, {type: 'scrolled', id: 1}, {type: 'flashEnded', id: 1},
            {type: 'issue', key: 2});

        expect(state.active?.id).toBe(2);
    });
});

describe('getScrollTargetIndex', () => {
    it('returns nothing without a request', () => {
        expect(getScrollTargetIndex(initialScrollRequestState, items, keyOf)).toBeUndefined();
    });

    it('stays pending until the target is in the items', () => {
        const state = run({type: 'issue', key: 3});

        expect(getScrollTargetIndex(state, [], keyOf)).toBeUndefined();
        expect(getScrollTargetIndex(state, items, keyOf)).toBe(2);
    });

    it('follows the target when the items are reordered', () => {
        const state = run({type: 'issue', key: 3});

        expect(getScrollTargetIndex(state, [...items].reverse(), keyOf)).toBe(0);
    });

    it('returns nothing once the request is flashing', () => {
        const state = run({type: 'issue', key: 3}, {type: 'scrolled', id: 1});

        expect(getScrollTargetIndex(state, items, keyOf)).toBeUndefined();
    });
});
