import {SyncRecordAction, type SyncSessionItem} from "../../model";

export const SYNC_RECORD_ACTIONS = Object.values(SyncRecordAction);

export function getActionColor(action: string): string {
    switch (action) {
        case 'CreateRemote':
            return 'green';
        case 'UpdateRemote':
            return 'blue';
        case 'CreateLocal':
            return 'teal';
        case 'UpdateLocal':
            return 'cyan';
        case 'DeleteLocal':
            return 'red';
        case 'Link':
            return 'lime';
        case 'Unlink':
            return 'orange';
        case 'Rename':
            return 'violet';
        case 'Skipped':
            return 'gray';
        case 'Conflict':
            return 'yellow';
        case 'UpdateTimestamp':
            return 'grape';
        case 'Error':
            return 'red';
        default:
            return 'gray';
    }
}

const ACTION_COUNT_FIELDS: Record<SyncRecordAction, keyof SyncSessionItem> = {
    CreateRemote: 'createRemoteCount',
    UpdateRemote: 'updateRemoteCount',
    CreateLocal: 'createLocalCount',
    UpdateLocal: 'updateLocalCount',
    DeleteLocal: 'deleteLocalCount',
    Link: 'linkCount',
    Unlink: 'unlinkCount',
    Rename: 'renameCount',
    Skipped: 'skippedCount',
    Conflict: 'conflictCount',
    UpdateTimestamp: 'updateTimestampCount',
    Error: 'errorCount',
};

export function getActionCount(session: SyncSessionItem, action: SyncRecordAction): number {
    return session[ACTION_COUNT_FIELDS[action]] as number;
}

/**
 * Whether the action is `Skipped` and the session counted its skipped files without keeping their
 * records: a completed session only keeps them when it was started with the record skipped option.
 */
export function areSkippedRecordsDeleted(session: SyncSessionItem, action: SyncRecordAction): boolean {
    return action === SyncRecordAction.Skipped
        && session.status === 'Completed'
        && !session.recordSkipped
        && session.skippedCount > 0;
}

/**
 * Whether filtering the session's records by the given action can return any.
 * The conflict count only covers the unresolved conflicts, while the resolved `Conflict` records
 * are kept, so a session can have them even when the count is zero.
 */
export function isActionFilterable(session: SyncSessionItem, action: SyncRecordAction): boolean {
    if (areSkippedRecordsDeleted(session, action)) return false;
    return action === SyncRecordAction.Conflict || getActionCount(session, action) > 0;
}

/**
 * Builds the advanced filter expression selecting records of the given actions.
 * Actions are emitted in enum order so the expression is stable regardless of click order.
 */
export function buildActionFilter(actions: readonly SyncRecordAction[]): string {
    const ordered = SYNC_RECORD_ACTIONS.filter(a => actions.includes(a));
    if (ordered.length === 0) return '';
    if (ordered.length === 1) return `action = "${ordered[0]}"`;
    return `action in [${ordered.map(a => `"${a}"`).join(', ')}]`;
}

const SINGLE_ACTION_FILTER = /^\s*action\s*=\s*"([^"]*)"\s*$/i;
const MULTI_ACTION_FILTER = /^\s*action\s+in\s*\[([^\]]*)]\s*$/i;

function toAction(value: string): SyncRecordAction | null {
    return SYNC_RECORD_ACTIONS.find(a => a.toLowerCase() === value.trim().toLowerCase()) ?? null;
}

/**
 * Parses an advanced filter expression produced by {@link buildActionFilter} back into the selected actions.
 * Returns an empty list for an empty expression, and `null` when the expression is anything else
 * (a custom filter the pills cannot represent).
 */
export function parseActionFilter(expression: string): SyncRecordAction[] | null {
    if (expression.trim() === '') return [];

    const single = SINGLE_ACTION_FILTER.exec(expression);
    if (single) {
        const action = toAction(single[1]);
        return action ? [action] : null;
    }

    const multi = MULTI_ACTION_FILTER.exec(expression);
    if (multi) {
        const items = multi[1].split(',');
        const actions: SyncRecordAction[] = [];
        for (const item of items) {
            const quoted = /^\s*"([^"]*)"\s*$/.exec(item);
            const action = quoted ? toAction(quoted[1]) : null;
            if (!action) return null;
            if (!actions.includes(action)) actions.push(action);
        }
        return actions;
    }

    return null;
}
