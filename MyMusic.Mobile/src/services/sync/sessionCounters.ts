import type {SyncSessionItem} from '../../api/types';

export type SessionCountField = {
    [K in keyof SyncSessionItem]-?: K extends `${string}Count` ? K : never
}[keyof SyncSessionItem];

export type SessionCounterColorKey = 'success' | 'info' | 'warning' | 'error' | 'textMuted' | 'syncDownload' | 'syncUpload';

export interface SessionCounterSubSlot {
    /** Sync record action counted by this sub-slot (`SyncRecordAction` on the server) */
    action: string;
    field: SessionCountField;
    /** Ionicons glyph name */
    icon: string;
    colorKey: SessionCounterColorKey;
}

export interface SessionCounterSlotDefinition {
    key: string;
    label: string;
    subSlots: SessionCounterSubSlot[];
}

/**
 * The counters shown on a session card. Every sync record action belongs to exactly one sub-slot,
 * so a session never shows only zeros while having records.
 */
export const SESSION_COUNTER_SLOTS: SessionCounterSlotDefinition[] = [
    {
        key: 'remote',
        label: 'Remote',
        subSlots: [
            {action: 'CreateRemote', field: 'createRemoteCount', icon: 'add-circle', colorKey: 'success'},
            {action: 'UpdateRemote', field: 'updateRemoteCount', icon: 'sync-circle', colorKey: 'info'},
        ],
    },
    {
        key: 'local',
        label: 'Local',
        subSlots: [
            {action: 'CreateLocal', field: 'createLocalCount', icon: 'add-circle', colorKey: 'syncDownload'},
            {action: 'UpdateLocal', field: 'updateLocalCount', icon: 'sync-circle', colorKey: 'syncUpload'},
            {action: 'DeleteLocal', field: 'deleteLocalCount', icon: 'trash', colorKey: 'error'},
            {action: 'Rename', field: 'renameCount', icon: 'pencil', colorKey: 'warning'},
        ],
    },
    {
        key: 'links',
        label: 'Links',
        subSlots: [
            {action: 'Link', field: 'linkCount', icon: 'link', colorKey: 'success'},
            {action: 'Unlink', field: 'unlinkCount', icon: 'unlink', colorKey: 'error'},
            {action: 'UpdateTimestamp', field: 'updateTimestampCount', icon: 'time', colorKey: 'info'},
        ],
    },
    {
        key: 'conflict',
        label: 'Conflict',
        subSlots: [
            {action: 'Conflict', field: 'conflictCount', icon: 'git-compare', colorKey: 'warning'},
        ],
    },
    {
        key: 'error',
        label: 'Error',
        subSlots: [
            {action: 'Error', field: 'errorCount', icon: 'alert-circle', colorKey: 'error'},
        ],
    },
    {
        key: 'skipped',
        label: 'Skipped',
        subSlots: [
            {action: 'Skipped', field: 'skippedCount', icon: 'play-skip-forward', colorKey: 'textMuted'},
        ],
    },
];

export interface SessionCounterPart {
    action: string;
    icon: string;
    colorKey: SessionCounterColorKey;
    value: number;
}

export interface SessionCounterSlot {
    key: string;
    label: string;
    total: number;
    /** The sub-slots with records. Empty when the whole slot is zero */
    parts: SessionCounterPart[];
}

export function buildSessionCounterSlots(session: SyncSessionItem): SessionCounterSlot[] {
    return SESSION_COUNTER_SLOTS.map((slot) => {
        const parts = slot.subSlots
            .map((subSlot) => ({
                action: subSlot.action,
                icon: subSlot.icon,
                colorKey: subSlot.colorKey,
                value: session[subSlot.field],
            }))
            .filter((part) => part.value > 0);

        return {
            key: slot.key,
            label: slot.label,
            total: parts.reduce((sum, part) => sum + part.value, 0),
            parts,
        };
    });
}

/**
 * Returns how many records of `action` the session has (case-insensitive), or 0 for an unknown action.
 */
export function getSessionActionCount(session: SyncSessionItem, action: string): number {
    const normalized = action.toLowerCase();

    for (const slot of SESSION_COUNTER_SLOTS) {
        for (const subSlot of slot.subSlots) {
            if (subSlot.action.toLowerCase() === normalized) {
                return session[subSlot.field];
            }
        }
    }

    return 0;
}
