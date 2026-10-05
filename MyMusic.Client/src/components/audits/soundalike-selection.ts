import {SecondaryAction} from "../../model/secondaryAction.ts";

export type SongAction = typeof SecondaryAction.Delete | typeof SecondaryAction.Merge | typeof SecondaryAction.Ignore;

export interface GroupSelection {
    primaryId: number | null;
    actions: Map<number, SongAction>;
}

/**
 * Picks the song to keep in a group. Every other song gets `defaultAction` unless it already has an action;
 * picking the kept song again clears the selection (returns null).
 */
export function selectPrimary(
    existing: GroupSelection | undefined,
    songIds: number[],
    songId: number,
    defaultAction: SongAction,
): GroupSelection | null {
    if (!existing || existing.primaryId == null) {
        const actions = new Map<number, SongAction>();
        for (const id of songIds) {
            if (id !== songId) actions.set(id, defaultAction);
        }
        return {primaryId: songId, actions};
    }

    if (existing.primaryId === songId) return null;

    const actions = new Map(existing.actions);
    actions.delete(songId);
    actions.set(existing.primaryId, defaultAction);
    return {primaryId: songId, actions};
}

/** Chooses what happens to a song that is not kept. */
export function setAction(existing: GroupSelection, songId: number, action: SongAction): GroupSelection {
    const actions = new Map(existing.actions);
    actions.set(songId, action);
    return {...existing, actions};
}

/** The selection as the list of secondary actions the resolve endpoint expects. */
export function toSecondaryActions(selection: GroupSelection) {
    return Array.from(selection.actions.entries()).map(([songId, action]) => ({songId, action}));
}
