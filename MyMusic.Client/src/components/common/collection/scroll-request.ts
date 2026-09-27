import type React from "react";
import {useCallback, useEffect, useMemo, useReducer} from "react";
import {COLLECTION_HIGHLIGHT_MS} from "../../../consts.ts";

/**
 * A request to scroll a collection to the item with the given key. A new request is issued
 * whenever either the key or the id changes.
 */
export interface ScrollRequest {
    key: React.Key;
    id: number;
}

/**
 * The resolved position of the active request in the rendered items, handed to the views.
 * The id identifies the request, so views scroll once per request even when the index moves.
 */
export interface ScrollTarget {
    index: number;
    id: number;
}

/**
 * - `pending`: waiting for the target to appear in the items, or for the view to finish scrolling to it
 * - `flashing`: the scroll has finished and the target is highlighted
 */
export type ScrollRequestPhase = 'pending' | 'flashing';

export interface ActiveScrollRequest {
    key: React.Key;
    id: number;
    phase: ScrollRequestPhase;
}

export interface ScrollRequestState {
    /** Only ever grows, so ids stay unique even after a request is cleared */
    lastId: number;
    active: ActiveScrollRequest | null;
}

export type ScrollRequestAction =
    | { type: 'issue'; key: React.Key }
    | { type: 'scrolled'; id: number }
    | { type: 'flashEnded'; id: number };

export const initialScrollRequestState: ScrollRequestState = {lastId: 0, active: null};

/**
 * Lifecycle of a collection scroll request: pending → flashing → cleared. The latest request
 * always wins, and events for older requests are ignored.
 */
export function scrollRequestReducer(state: ScrollRequestState, action: ScrollRequestAction): ScrollRequestState {
    switch (action.type) {
        case 'issue': {
            const id = state.lastId + 1;
            return {lastId: id, active: {key: action.key, id, phase: 'pending'}};
        }
        case 'scrolled':
            if (state.active?.id !== action.id || state.active.phase !== 'pending') return state;
            return {...state, active: {...state.active, phase: 'flashing'}};
        case 'flashEnded':
            if (state.active?.id !== action.id) return state;
            return {...state, active: null};
    }
}

/**
 * Index of the active request's target in `items`, or `undefined` when there is nothing to
 * scroll to: no request, the request is already flashing, or the target isn't in the items yet.
 */
export function getScrollTargetIndex<T>(
    state: ScrollRequestState,
    items: T[],
    keyOf: (item: T) => React.Key
): number | undefined {
    const active = state.active;
    if (active == null || active.phase !== 'pending') return undefined;

    const index = items.findIndex(item => keyOf(item) === active.key);
    return index >= 0 ? index : undefined;
}

/**
 * Holds the collection's single scroll request and drives it through its lifecycle.
 * The view scrolls to `scrollTarget` and reports back through `onScrolled`, the target is then
 * highlighted through `highlightKey`, and the request is cleared once the highlight ends.
 */
export function useScrollRequestLifecycle<T>(items: T[], keyOf: (item: T) => React.Key) {
    const [state, dispatch] = useReducer(scrollRequestReducer, initialScrollRequestState);

    const issue = useCallback((key: React.Key) => dispatch({type: 'issue', key}), []);
    const onScrolled = useCallback((id: number) => dispatch({type: 'scrolled', id}), []);

    const targetIndex = getScrollTargetIndex(state, items, keyOf);
    const activeId = state.active?.id;
    const scrollTarget = useMemo<ScrollTarget | undefined>(
        () => targetIndex != null && activeId != null ? {index: targetIndex, id: activeId} : undefined,
        [targetIndex, activeId]);

    const flashingId = state.active?.phase === 'flashing' ? state.active.id : null;
    useEffect(() => {
        if (flashingId == null) return;
        const timeout = setTimeout(() => dispatch({type: 'flashEnded', id: flashingId}), COLLECTION_HIGHLIGHT_MS);
        return () => clearTimeout(timeout);
    }, [flashingId]);

    const highlightKey = state.active?.phase === 'flashing' ? state.active.key : null;

    return {issue, scrollTarget, onScrolled, highlightKey};
}
