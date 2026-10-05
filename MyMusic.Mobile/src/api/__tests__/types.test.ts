import {SyncRecordItemSchema} from '../types';

describe('SyncRecordItemSchema', () => {
    const deleteLocal = (data: unknown) => ({
        id: 1,
        filePath: '2018/Song - Artist.mp3',
        action: 'DeleteLocal',
        songId: null,
        data,
        reason: 'Song marked for removal or deleted on server',
        acknowledged: false,
        processedAt: '2026-10-05T18:02:56Z',
    });

    test('parses a DeleteLocal created by the check, which carries the song data', () => {
        const parsed = SyncRecordItemSchema.parse(deleteLocal({SongId: null, ModifiedAt: null}));

        expect(parsed.action).toBe('DeleteLocal');
    });

    test('parses a DeleteLocal created as a pending action, which carries no data', () => {
        const parsed = SyncRecordItemSchema.parse(deleteLocal(null));

        expect(parsed.action).toBe('DeleteLocal');
    });
});
