import {describe, expect, it} from 'vitest';
import type {CollectionSchemaAction} from '../common/collection/collection-schema.tsx';
import {getFooterSongActions} from './player-info-actions';

function makeAction(name: string): CollectionSchemaAction<number> {
    return {name, renderIcon: () => null, renderLabel: () => name, onClick: () => {}};
}

function names(actions: CollectionSchemaAction<number>[]): string[] {
    return actions.flatMap(a => 'name' in a ? [a.name] : 'group' in a ? [`group:${a.group}`] : []);
}

describe('getFooterSongActions', () => {
    const actions: CollectionSchemaAction<number>[] = [
        {group: 'Manage'},
        makeAction('favorite'),
        makeAction('manage-playlists'),
        makeAction('manage-devices'),
        makeAction('download'),
        makeAction('edit'),
        makeAction('delete'),
        {group: 'Queue'},
        makeAction('play'),
        makeAction('play-next'),
        makeAction('play-last'),
        makeAction('stop-after-playback'),
        makeAction('skip-next-playback'),
        makeAction('shuffle'),
        makeAction('remove-from-queue'),
    ];

    it('keeps manage actions and stop-after-playback, hiding the other queue actions', () => {
        expect(names(getFooterSongActions(actions))).toEqual([
            'group:Manage',
            'favorite',
            'manage-playlists',
            'manage-devices',
            'download',
            'edit',
            'delete',
            'group:Queue',
            'stop-after-playback',
        ]);
    });

    it('promotes favorite and manage-playlists to primary actions', () => {
        const primary = getFooterSongActions(actions)
            .filter(a => 'name' in a && a.primary)
            .map(a => 'name' in a ? a.name : '');

        expect(primary).toEqual(['favorite', 'manage-playlists']);
    });
});
