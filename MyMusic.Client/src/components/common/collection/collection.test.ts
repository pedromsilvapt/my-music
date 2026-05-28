import { describe, expect, it } from 'vitest';
import type { CollectionSchema, FilterMetadataResponse } from './collection-schema';

interface TestSong {
    id: number;
    title: string;
    artists: Array<{ id: number; name: string }>;
    genres: Array<{ id: number; name: string }>;
    devices: Array<{ id: number; name: string }>;
    album: { id: number; name: string };
    isActive?: boolean;
    isExplicit?: boolean;
    hasLyrics?: boolean;
    playCount?: number;
    description?: string | null;
}

const mockFilterMetadata: FilterMetadataResponse = {
    fields: [
        {
            name: 'artist.name',
            type: 'string',
            description: 'Artist name filter',
            supportedOperators: ['=', '!=', 'contains', 'startsWith', 'endsWith', 'in', 'notIn', 'isNull', 'isNotNull'],
            isCollection: true
        },
        {
            name: 'genre.name',
            type: 'string',
            description: 'Genre name filter',
            supportedOperators: ['=', '!=', 'contains', 'startsWith', 'endsWith'],
            isCollection: true
        },
        {
            name: 'device.name',
            type: 'string',
            description: 'Device name filter',
            supportedOperators: ['=', '!=', 'contains', 'startsWith', 'endsWith'],
            isCollection: true
        },
        {
            name: 'album.name',
            type: 'string',
            description: 'Album name filter',
            supportedOperators: ['=', '!=', 'contains', 'startsWith', 'endsWith'],
            isCollection: false
        },
        {
            name: 'title',
            type: 'string',
            description: 'Song title',
            supportedOperators: ['=', '!=', 'contains', 'startsWith', 'endsWith'],
            isCollection: false
        },
        {
            name: 'playCount',
            type: 'number',
            description: 'Play count',
            supportedOperators: ['=', '!=', '>', '>=', '<', '<=', 'between'],
            isCollection: false
        },
        {
            name: 'isActive',
            type: 'boolean',
            description: 'Whether song is active',
            supportedOperators: ['isTrue', 'isFalse'],
            isCollection: false
        },
        {
            name: 'explicit',
            clientPath: 'isExplicit',
            type: 'boolean',
            description: 'Has explicit content',
            supportedOperators: ['isTrue', 'isFalse'],
            isCollection: false
        },
        {
            name: 'hasLyrics',
            type: 'boolean',
            description: 'Has lyrics',
            supportedOperators: ['isTrue', 'isFalse'],
            isCollection: false
        },
        {
            name: 'description',
            type: 'string',
            description: 'Description',
            supportedOperators: ['=', '!=', 'isNull', 'isNotNull'],
            isCollection: false
        }
    ],
    operators: []
};

function createMockSchema(_songs: TestSong[]): CollectionSchema<TestSong> {
    return {
        key: (song) => song.id,
        columns: [],
        estimateTableRowHeight: () => 40,
        estimateListRowHeight: () => 60,
        renderListArtwork: () => null,
        renderListTitle: () => null,
        renderListSubTitle: () => null,
        searchVector: (song) => song.title,
        filterMetadata: mockFilterMetadata
    };
}

function makeSong(overrides: Partial<TestSong> = {}): TestSong {
    return {
        id: 1,
        title: 'Test Song',
        artists: [{ id: 1, name: 'Artist One' }],
        genres: [{ id: 1, name: 'Rock' }],
        devices: [{ id: 1, name: 'Device One' }],
        album: { id: 1, name: 'Test Album' },
        ...overrides
    };
}

describe('collection field equality filtering', () => {
    it('matches when any artist name equals the filter value (=)', async () => {
        const { tokenizeFilter, evaluateTokens } = await import('./collection-filter.ts');
        
        const song = makeSong({
            artists: [
                { id: 1, name: 'Taylor Swift' },
                { id: 2, name: 'Ed Sheeran' }
            ]
        });
        const schema = createMockSchema([song]);
        
        const tokens = tokenizeFilter('artist.name = "Taylor Swift"');
        const result = evaluateTokens(song, tokens, schema);
        
        expect(result).toBe(true);
    });
    
    it('matches when any genre name equals the filter value (=)', async () => {
        const { tokenizeFilter, evaluateTokens } = await import('./collection-filter.ts');
        
        const song = makeSong({
            genres: [
                { id: 1, name: 'Rock' },
                { id: 2, name: 'Pop' }
            ]
        });
        const schema = createMockSchema([song]);
        
        const tokens = tokenizeFilter('genre.name = "Pop"');
        const result = evaluateTokens(song, tokens, schema);
        
        expect(result).toBe(true);
    });
    
    it('does not match when no artist equals the filter value (=)', async () => {
        const { tokenizeFilter, evaluateTokens } = await import('./collection-filter.ts');
        
        const song = makeSong({
            artists: [
                { id: 1, name: 'Taylor Swift' },
                { id: 2, name: 'Ed Sheeran' }
            ]
        });
        const schema = createMockSchema([song]);
        
        const tokens = tokenizeFilter('artist.name = "Adele"');
        const result = evaluateTokens(song, tokens, schema);
        
        expect(result).toBe(false);
    });
    
    it('excludes song when no artist matches using !=', async () => {
        const { tokenizeFilter, evaluateTokens } = await import('./collection-filter.ts');
        
        const song = makeSong({
            artists: [
                { id: 1, name: 'Taylor Swift' },
                { id: 2, name: 'Ed Sheeran' }
            ]
        });
        const schema = createMockSchema([song]);
        
        const tokens = tokenizeFilter('artist.name != "Taylor Swift"');
        const result = evaluateTokens(song, tokens, schema);
        
        expect(result).toBe(false);
    });
    
    it('includes song when all artists are different using !=', async () => {
        const { tokenizeFilter, evaluateTokens } = await import('./collection-filter.ts');
        
        const song = makeSong({
            artists: [
                { id: 1, name: 'Taylor Swift' },
                { id: 2, name: 'Ed Sheeran' }
            ]
        });
        const schema = createMockSchema([song]);
        
        const tokens = tokenizeFilter('artist.name != "Adele"');
        const result = evaluateTokens(song, tokens, schema);
        
        expect(result).toBe(true);
    });
});

describe('collection field substring operators', () => {
    it('matches when any artist name contains the filter value', async () => {
        const { tokenizeFilter, evaluateTokens } = await import('./collection-filter.ts');
        
        const song = makeSong({
            artists: [
                { id: 1, name: 'Taylor Swift' },
                { id: 2, name: 'Kanye West' }
            ]
        });
        const schema = createMockSchema([song]);
        
        const tokens = tokenizeFilter('artist.name contains "Taylor"');
        const result = evaluateTokens(song, tokens, schema);
        
        expect(result).toBe(true);
    });
    
    it('matches when any artist name starts with the filter value', async () => {
        const { tokenizeFilter, evaluateTokens } = await import('./collection-filter.ts');
        
        const song = makeSong({
            artists: [
                { id: 1, name: 'Taylor Swift' },
                { id: 2, name: 'Kanye West' }
            ]
        });
        const schema = createMockSchema([song]);
        
        const tokens = tokenizeFilter('artist.name startsWith "Tay"');
        const result = evaluateTokens(song, tokens, schema);
        
        expect(result).toBe(true);
    });
    
    it('matches when any artist name ends with the filter value', async () => {
        const { tokenizeFilter, evaluateTokens } = await import('./collection-filter.ts');
        
        const song = makeSong({
            artists: [
                { id: 1, name: 'Taylor Swift' },
                { id: 2, name: 'Kanye West' }
            ]
        });
        const schema = createMockSchema([song]);
        
        const tokens = tokenizeFilter('artist.name endsWith "Swift"');
        const result = evaluateTokens(song, tokens, schema);
        
        expect(result).toBe(true);
    });
    
    it('does not match when no artist satisfies substring condition', async () => {
        const { tokenizeFilter, evaluateTokens } = await import('./collection-filter.ts');
        
        const song = makeSong({
            artists: [
                { id: 1, name: 'Taylor Swift' },
                { id: 2, name: 'Ed Sheeran' }
            ]
        });
        const schema = createMockSchema([song]);
        
        const tokens = tokenizeFilter('artist.name contains "Adele"');
        const result = evaluateTokens(song, tokens, schema);
        
        expect(result).toBe(false);
    });
});

describe('empty collection handling', () => {
    it('does not match when collection is empty and using =', async () => {
        const { tokenizeFilter, evaluateTokens } = await import('./collection-filter.ts');
        
        const song = makeSong({ artists: [] });
        const schema = createMockSchema([song]);
        
        const tokens = tokenizeFilter('artist.name = "Taylor Swift"');
        const result = evaluateTokens(song, tokens, schema);
        
        expect(result).toBe(false);
    });
    
    it('matches when collection is empty and using !=', async () => {
        const { tokenizeFilter, evaluateTokens } = await import('./collection-filter.ts');
        
        const song = makeSong({ artists: [] });
        const schema = createMockSchema([song]);
        
        const tokens = tokenizeFilter('artist.name != "Taylor Swift"');
        const result = evaluateTokens(song, tokens, schema);
        
        expect(result).toBe(true);
    });
    
    it('does not match when collection is empty and using contains', async () => {
        const { tokenizeFilter, evaluateTokens } = await import('./collection-filter.ts');
        
        const song = makeSong({ genres: [] });
        const schema = createMockSchema([song]);
        
        const tokens = tokenizeFilter('genre.name contains "Rock"');
        const result = evaluateTokens(song, tokens, schema);
        
        expect(result).toBe(false);
    });
    
    it('handles undefined collection property', async () => {
        const { tokenizeFilter, evaluateTokens } = await import('./collection-filter.ts');
        
        const song = makeSong();
        const schema = createMockSchema([song]);
        
        const tokens = tokenizeFilter('device.name = "iPhone"');
        const result = evaluateTokens(song, tokens, schema);
        
        expect(result).toBe(false);
    });
});

describe('non-collection fields remain unchanged', () => {
    it('still works for regular string fields like title', async () => {
        const { tokenizeFilter, evaluateTokens } = await import('./collection-filter.ts');
        
        const song = makeSong({ title: 'Shake It Off' });
        const schema = createMockSchema([song]);
        
        const tokens = tokenizeFilter('title = "Shake It Off"');
        const result = evaluateTokens(song, tokens, schema);
        
        expect(result).toBe(true);
    });
    
    it('still works for non-collection object fields like album', async () => {
        const { tokenizeFilter, evaluateTokens } = await import('./collection-filter.ts');
        
        const song = makeSong({ album: { id: 1, name: '1989' } });
        const schema = createMockSchema([song]);
        
        const tokens = tokenizeFilter('album.name = "1989"');
        const result = evaluateTokens(song, tokens, schema);
        
        expect(result).toBe(true);
    });
    
    it('supports all operators for non-collection fields', async () => {
        const { tokenizeFilter, evaluateTokens } = await import('./collection-filter.ts');
        
        const song = makeSong({ title: 'Shake It Off' });
        const schema = createMockSchema([song]);
        
        const testCases = [
            { filter: 'title contains "Shake"', expected: true },
            { filter: 'title startsWith "Shake"', expected: true },
            { filter: 'title endsWith "Off"', expected: true },
            { filter: 'title != "Bad Blood"', expected: true },
        ];
        
        for (const { filter, expected } of testCases) {
            const tokens = tokenizeFilter(filter);
            const result = evaluateTokens(song, tokens, schema);
            expect(result).toBe(expected);
        }
    });
});

describe('combined filter conditions', () => {
    it('handles AND combinator with collection and non-collection fields', async () => {
        const { tokenizeFilter, evaluateTokens } = await import('./collection-filter.ts');
        
        const song = makeSong({
            title: 'Shake It Off',
            artists: [{ id: 1, name: 'Taylor Swift' }],
            album: { id: 1, name: '1989' }
        });
        const schema = createMockSchema([song]);
        
        const tokens = tokenizeFilter('artist.name = "Taylor Swift" and album.name = "1989"');
        const result = evaluateTokens(song, tokens, schema);
        
        expect(result).toBe(true);
    });
    
    it('handles OR combinator with collection fields', async () => {
        const { tokenizeFilter, evaluateTokens } = await import('./collection-filter.ts');
        
        const song = makeSong({
            artists: [{ id: 1, name: 'Taylor Swift' }],
            genres: [{ id: 1, name: 'Rock' }]
        });
        const schema = createMockSchema([song]);
        
        const tokens = tokenizeFilter('artist.name = "Adele" or genre.name = "Rock"');
        const result = evaluateTokens(song, tokens, schema);
        
        expect(result).toBe(true);
    });
});

describe('case insensitivity', () => {
    it('matches artist name case-insensitively', async () => {
        const { tokenizeFilter, evaluateTokens } = await import('./collection-filter.ts');
        
        const song = makeSong({
            artists: [{ id: 1, name: 'Taylor Swift' }]
        });
        const schema = createMockSchema([song]);
        
        const tokens = tokenizeFilter('artist.name = "TAYLOR SWIFT"');
        const result = evaluateTokens(song, tokens, schema);
        
        expect(result).toBe(true);
    });
    
    it('matches substring case-insensitively', async () => {
        const { tokenizeFilter, evaluateTokens } = await import('./collection-filter.ts');
        
        const song = makeSong({
            artists: [{ id: 1, name: 'Taylor Swift' }]
        });
        const schema = createMockSchema([song]);
        
        const tokens = tokenizeFilter('artist.name contains "TAYLOR"');
        const result = evaluateTokens(song, tokens, schema);
        
        expect(result).toBe(true);
    });
});

describe('tokenizer correctly identifies keyword operators', () => {
    it('tokenizes contains as operator', async () => {
        const { tokenizeFilter } = await import('./collection-filter.ts');
        const tokens = tokenizeFilter('title contains "test"');
        expect(tokens[0]).toEqual({ type: 'field', value: 'title' });
        expect(tokens[1]).toEqual({ type: 'operator', value: 'contains' });
        expect(tokens[2]).toEqual({ type: 'value', value: 'test' });
    });

    it('tokenizes startsWith as operator', async () => {
        const { tokenizeFilter } = await import('./collection-filter.ts');
        const tokens = tokenizeFilter('title startsWith "test"');
        expect(tokens[0]).toEqual({ type: 'field', value: 'title' });
        expect(tokens[1]).toEqual({ type: 'operator', value: 'startswith' });
        expect(tokens[2]).toEqual({ type: 'value', value: 'test' });
    });

    it('tokenizes endsWith as operator', async () => {
        const { tokenizeFilter } = await import('./collection-filter.ts');
        const tokens = tokenizeFilter('title endsWith "test"');
        expect(tokens[0]).toEqual({ type: 'field', value: 'title' });
        expect(tokens[1]).toEqual({ type: 'operator', value: 'endswith' });
        expect(tokens[2]).toEqual({ type: 'value', value: 'test' });
    });

    it('tokenizes in as operator', async () => {
        const { tokenizeFilter } = await import('./collection-filter.ts');
        const tokens = tokenizeFilter('title in ["a", "b"]');
        expect(tokens[0]).toEqual({ type: 'field', value: 'title' });
        expect(tokens[1]).toEqual({ type: 'operator', value: 'in' });
    });

    it('tokenizes notIn as operator', async () => {
        const { tokenizeFilter } = await import('./collection-filter.ts');
        const tokens = tokenizeFilter('title notIn ["a", "b"]');
        expect(tokens[0]).toEqual({ type: 'field', value: 'title' });
        expect(tokens[1]).toEqual({ type: 'operator', value: 'notin' });
    });

    it('tokenizes between as operator', async () => {
        const { tokenizeFilter } = await import('./collection-filter.ts');
        const tokens = tokenizeFilter('playCount between 10 and 100');
        expect(tokens[0]).toEqual({ type: 'field', value: 'playCount' });
        expect(tokens[1]).toEqual({ type: 'operator', value: 'between' });
    });

    it('tokenizes isNull as operator', async () => {
        const { tokenizeFilter } = await import('./collection-filter.ts');
        const tokens = tokenizeFilter('description isNull');
        expect(tokens[0]).toEqual({ type: 'field', value: 'description' });
        expect(tokens[1]).toEqual({ type: 'operator', value: 'isnull' });
    });

    it('tokenizes isNotNull as operator', async () => {
        const { tokenizeFilter } = await import('./collection-filter.ts');
        const tokens = tokenizeFilter('description isNotNull');
        expect(tokens[0]).toEqual({ type: 'field', value: 'description' });
        expect(tokens[1]).toEqual({ type: 'operator', value: 'isnotnull' });
    });

    it('tokenizes isTrue as operator', async () => {
        const { tokenizeFilter } = await import('./collection-filter.ts');
        const tokens = tokenizeFilter('isActive isTrue');
        expect(tokens[0]).toEqual({ type: 'field', value: 'isActive' });
        expect(tokens[1]).toEqual({ type: 'operator', value: 'istrue' });
    });

    it('tokenizes isFalse as operator', async () => {
        const { tokenizeFilter } = await import('./collection-filter.ts');
        const tokens = tokenizeFilter('isActive isFalse');
        expect(tokens[0]).toEqual({ type: 'field', value: 'isActive' });
        expect(tokens[1]).toEqual({ type: 'operator', value: 'isfalse' });
    });

    it('does not confuse "in" inside a field name with operator "in"', async () => {
        const { tokenizeFilter } = await import('./collection-filter.ts');
        const tokens = tokenizeFilter('playCount = 5');
        expect(tokens[0]).toEqual({ type: 'field', value: 'playCount' });
        expect(tokens[1]).toEqual({ type: 'operator', value: '=' });
        expect(tokens[2]).toEqual({ type: 'value', value: '5' });
    });
});

describe('in operator', () => {
    it('matches when value is in the list', async () => {
        const { tokenizeFilter, evaluateTokens } = await import('./collection-filter.ts');
        const song = makeSong({ title: 'Rock' });
        const schema = createMockSchema([song]);
        const tokens = tokenizeFilter('title in ["Rock", "Pop", "Jazz"]');
        expect(evaluateTokens(song, tokens, schema)).toBe(true);
    });

    it('does not match when value is not in the list', async () => {
        const { tokenizeFilter, evaluateTokens } = await import('./collection-filter.ts');
        const song = makeSong({ title: 'Metal' });
        const schema = createMockSchema([song]);
        const tokens = tokenizeFilter('title in ["Rock", "Pop", "Jazz"]');
        expect(evaluateTokens(song, tokens, schema)).toBe(false);
    });

    it('matches with single value in list', async () => {
        const { tokenizeFilter, evaluateTokens } = await import('./collection-filter.ts');
        const song = makeSong({ title: 'Rock' });
        const schema = createMockSchema([song]);
        const tokens = tokenizeFilter('title in ["Rock"]');
        expect(evaluateTokens(song, tokens, schema)).toBe(true);
    });

    it('handles in operator case-insensitively', async () => {
        const { tokenizeFilter, evaluateTokens } = await import('./collection-filter.ts');
        const song = makeSong({ title: 'ROCK' });
        const schema = createMockSchema([song]);
        const tokens = tokenizeFilter('title in ["rock", "pop"]');
        expect(evaluateTokens(song, tokens, schema)).toBe(true);
    });
});

describe('notIn operator', () => {
    it('matches when value is not in the list', async () => {
        const { tokenizeFilter, evaluateTokens } = await import('./collection-filter.ts');
        const song = makeSong({ title: 'Metal' });
        const schema = createMockSchema([song]);
        const tokens = tokenizeFilter('title notIn ["Rock", "Pop", "Jazz"]');
        expect(evaluateTokens(song, tokens, schema)).toBe(true);
    });

    it('does not match when value is in the list', async () => {
        const { tokenizeFilter, evaluateTokens } = await import('./collection-filter.ts');
        const song = makeSong({ title: 'Rock' });
        const schema = createMockSchema([song]);
        const tokens = tokenizeFilter('title notIn ["Rock", "Pop", "Jazz"]');
        expect(evaluateTokens(song, tokens, schema)).toBe(false);
    });

    it('matches when list is empty', async () => {
        const { tokenizeFilter, evaluateTokens } = await import('./collection-filter.ts');
        const song = makeSong({ title: 'Rock' });
        const schema = createMockSchema([song]);
        const tokens = tokenizeFilter('title notIn []');
        expect(evaluateTokens(song, tokens, schema)).toBe(true);
    });
});

describe('isNull and isNotNull operators', () => {
    it('isNull matches when field is null', async () => {
        const { tokenizeFilter, evaluateTokens } = await import('./collection-filter.ts');
        const song = makeSong({ description: null });
        const schema = createMockSchema([song]);
        const tokens = tokenizeFilter('description isNull');
        expect(evaluateTokens(song, tokens, schema)).toBe(true);
    });

    it('isNull matches when field is undefined', async () => {
        const { tokenizeFilter, evaluateTokens } = await import('./collection-filter.ts');
        const song = makeSong({});
        const schema = createMockSchema([song]);
        const tokens = tokenizeFilter('description isNull');
        expect(evaluateTokens(song, tokens, schema)).toBe(true);
    });

    it('isNull does not match when field has a value', async () => {
        const { tokenizeFilter, evaluateTokens } = await import('./collection-filter.ts');
        const song = makeSong({ description: 'A great song' });
        const schema = createMockSchema([song]);
        const tokens = tokenizeFilter('description isNull');
        expect(evaluateTokens(song, tokens, schema)).toBe(false);
    });

    it('isNotNull matches when field has a value', async () => {
        const { tokenizeFilter, evaluateTokens } = await import('./collection-filter.ts');
        const song = makeSong({ description: 'A great song' });
        const schema = createMockSchema([song]);
        const tokens = tokenizeFilter('description isNotNull');
        expect(evaluateTokens(song, tokens, schema)).toBe(true);
    });

    it('isNotNull does not match when field is null', async () => {
        const { tokenizeFilter, evaluateTokens } = await import('./collection-filter.ts');
        const song = makeSong({ description: null });
        const schema = createMockSchema([song]);
        const tokens = tokenizeFilter('description isNotNull');
        expect(evaluateTokens(song, tokens, schema)).toBe(false);
    });
});

describe('between operator', () => {
    it('matches when value is within range', async () => {
        const { tokenizeFilter, evaluateTokens } = await import('./collection-filter.ts');
        const song = makeSong({ playCount: 50 });
        const schema = createMockSchema([song]);
        const tokens = tokenizeFilter('playCount between 10 and 100');
        expect(evaluateTokens(song, tokens, schema)).toBe(true);
    });

    it('matches when value equals lower bound', async () => {
        const { tokenizeFilter, evaluateTokens } = await import('./collection-filter.ts');
        const song = makeSong({ playCount: 10 });
        const schema = createMockSchema([song]);
        const tokens = tokenizeFilter('playCount between 10 and 100');
        expect(evaluateTokens(song, tokens, schema)).toBe(true);
    });

    it('matches when value equals upper bound', async () => {
        const { tokenizeFilter, evaluateTokens } = await import('./collection-filter.ts');
        const song = makeSong({ playCount: 100 });
        const schema = createMockSchema([song]);
        const tokens = tokenizeFilter('playCount between 10 and 100');
        expect(evaluateTokens(song, tokens, schema)).toBe(true);
    });

    it('does not match when value is below range', async () => {
        const { tokenizeFilter, evaluateTokens } = await import('./collection-filter.ts');
        const song = makeSong({ playCount: 5 });
        const schema = createMockSchema([song]);
        const tokens = tokenizeFilter('playCount between 10 and 100');
        expect(evaluateTokens(song, tokens, schema)).toBe(false);
    });

    it('does not match when value is above range', async () => {
        const { tokenizeFilter, evaluateTokens } = await import('./collection-filter.ts');
        const song = makeSong({ playCount: 150 });
        const schema = createMockSchema([song]);
        const tokens = tokenizeFilter('playCount between 10 and 100');
        expect(evaluateTokens(song, tokens, schema)).toBe(false);
    });

    it('does not match non-number fields', async () => {
        const { tokenizeFilter, evaluateTokens } = await import('./collection-filter.ts');
        const song = makeSong({ title: 'Test Song' });
        const schema = createMockSchema([song]);
        const tokens = tokenizeFilter('title between "A" and "Z"');
        expect(evaluateTokens(song, tokens, schema)).toBe(false);
    });
});

describe('isTrue and isFalse operators', () => {
    it('isTrue matches when field is true', async () => {
        const { tokenizeFilter, evaluateTokens } = await import('./collection-filter.ts');
        const song = makeSong({ isActive: true });
        const schema = createMockSchema([song]);
        const tokens = tokenizeFilter('isActive isTrue');
        expect(evaluateTokens(song, tokens, schema)).toBe(true);
    });

    it('isTrue does not match when field is false', async () => {
        const { tokenizeFilter, evaluateTokens } = await import('./collection-filter.ts');
        const song = makeSong({ isActive: false });
        const schema = createMockSchema([song]);
        const tokens = tokenizeFilter('isActive isTrue');
        expect(evaluateTokens(song, tokens, schema)).toBe(false);
    });

    it('isTrue does not match when field is undefined', async () => {
        const { tokenizeFilter, evaluateTokens } = await import('./collection-filter.ts');
        const song = makeSong({});
        const schema = createMockSchema([song]);
        const tokens = tokenizeFilter('isActive isTrue');
        expect(evaluateTokens(song, tokens, schema)).toBe(false);
    });

    it('isFalse matches when field is false', async () => {
        const { tokenizeFilter, evaluateTokens } = await import('./collection-filter.ts');
        const song = makeSong({ isActive: false });
        const schema = createMockSchema([song]);
        const tokens = tokenizeFilter('isActive isFalse');
        expect(evaluateTokens(song, tokens, schema)).toBe(true);
    });

    it('isFalse does not match when field is true', async () => {
        const { tokenizeFilter, evaluateTokens } = await import('./collection-filter.ts');
        const song = makeSong({ isActive: true });
        const schema = createMockSchema([song]);
        const tokens = tokenizeFilter('isActive isFalse');
        expect(evaluateTokens(song, tokens, schema)).toBe(false);
    });

    it('isFalse matches when field is undefined', async () => {
        const { tokenizeFilter, evaluateTokens } = await import('./collection-filter.ts');
        const song = makeSong({});
        const schema = createMockSchema([song]);
        const tokens = tokenizeFilter('isActive isFalse');
        expect(evaluateTokens(song, tokens, schema)).toBe(true);
    });
});

describe('clientPath field name mapping', () => {
    it('resolves clientPath when filter field name differs from DTO property', async () => {
        const { tokenizeFilter, evaluateTokens } = await import('./collection-filter.ts');
        const song = makeSong({ isExplicit: true });
        const schema = createMockSchema([song]);
        const tokens = tokenizeFilter('explicit isTrue');
        expect(evaluateTokens(song, tokens, schema)).toBe(true);
    });

    it('resolves clientPath to false when DTO property is false', async () => {
        const { tokenizeFilter, evaluateTokens } = await import('./collection-filter.ts');
        const song = makeSong({ isExplicit: false });
        const schema = createMockSchema([song]);
        const tokens = tokenizeFilter('explicit isTrue');
        expect(evaluateTokens(song, tokens, schema)).toBe(false);
    });

    it('resolves clientPath isFalse correctly', async () => {
        const { tokenizeFilter, evaluateTokens } = await import('./collection-filter.ts');
        const song = makeSong({ isExplicit: false });
        const schema = createMockSchema([song]);
        const tokens = tokenizeFilter('explicit isFalse');
        expect(evaluateTokens(song, tokens, schema)).toBe(true);
    });

    it('uses field name directly when clientPath is not set', async () => {
        const { tokenizeFilter, evaluateTokens } = await import('./collection-filter.ts');
        const song = makeSong({ isActive: true });
        const schema = createMockSchema([song]);
        const tokens = tokenizeFilter('isActive isTrue');
        expect(evaluateTokens(song, tokens, schema)).toBe(true);
    });

    it('resolves hasLyrics isTrue when field has no clientPath', async () => {
        const { tokenizeFilter, evaluateTokens } = await import('./collection-filter.ts');
        const song = makeSong({ hasLyrics: true });
        const schema = createMockSchema([song]);
        const tokens = tokenizeFilter('hasLyrics isTrue');
        expect(evaluateTokens(song, tokens, schema)).toBe(true);
    });

    it('resolves hasLyrics isFalse when field has no clientPath', async () => {
        const { tokenizeFilter, evaluateTokens } = await import('./collection-filter.ts');
        const song = makeSong({ hasLyrics: false });
        const schema = createMockSchema([song]);
        const tokens = tokenizeFilter('hasLyrics isFalse');
        expect(evaluateTokens(song, tokens, schema)).toBe(true);
    });

    it('hasLyrics isTrue does not match when hasLyrics is false', async () => {
        const { tokenizeFilter, evaluateTokens } = await import('./collection-filter.ts');
        const song = makeSong({ hasLyrics: false });
        const schema = createMockSchema([song]);
        const tokens = tokenizeFilter('hasLyrics isTrue');
        expect(evaluateTokens(song, tokens, schema)).toBe(false);
    });
});

describe('combined conditions with new operators', () => {
    it('handles AND with isNull and = operator', async () => {
        const { tokenizeFilter, evaluateTokens } = await import('./collection-filter.ts');
        const song = makeSong({ description: null, title: 'Hello' });
        const schema = createMockSchema([song]);
        const tokens = tokenizeFilter('description isNull and title = "Hello"');
        expect(evaluateTokens(song, tokens, schema)).toBe(true);
    });

    it('handles OR with isTrue and between', async () => {
        const { tokenizeFilter, evaluateTokens } = await import('./collection-filter.ts');
        const song = makeSong({ isActive: true, playCount: 5 });
        const schema = createMockSchema([song]);
        const tokens = tokenizeFilter('isActive isTrue or playCount between 10 and 100');
        expect(evaluateTokens(song, tokens, schema)).toBe(true);
    });

    it('handles isFalse AND between (both true)', async () => {
        const { tokenizeFilter, evaluateTokens } = await import('./collection-filter.ts');
        const song = makeSong({ isActive: false, playCount: 50 });
        const schema = createMockSchema([song]);
        const tokens = tokenizeFilter('isActive isFalse and playCount between 10 and 100');
        expect(evaluateTokens(song, tokens, schema)).toBe(true);
    });

    it('handles isFalse AND between (between false)', async () => {
        const { tokenizeFilter, evaluateTokens } = await import('./collection-filter.ts');
        const song = makeSong({ isActive: false, playCount: 5 });
        const schema = createMockSchema([song]);
        const tokens = tokenizeFilter('isActive isFalse and playCount between 10 and 100');
        expect(evaluateTokens(song, tokens, schema)).toBe(false);
    });

    it('handles in combined with equals using AND', async () => {
        const { tokenizeFilter, evaluateTokens } = await import('./collection-filter.ts');
        const song = makeSong({ title: 'Rock', playCount: 50 });
        const schema = createMockSchema([song]);
        const tokens = tokenizeFilter('title in ["Rock", "Pop"] and playCount = 50');
        expect(evaluateTokens(song, tokens, schema)).toBe(true);
    });

    it('handles between "and" not confused with combinator "and"', async () => {
        const { tokenizeFilter, evaluateTokens } = await import('./collection-filter.ts');
        const song = makeSong({ title: 'Hello', playCount: 50 });
        const schema = createMockSchema([song]);
        const tokens = tokenizeFilter('playCount between 10 and 100 and title = "Hello"');
        expect(evaluateTokens(song, tokens, schema)).toBe(true);
    });

    it('handles notIn with OR combinator', async () => {
        const { tokenizeFilter, evaluateTokens } = await import('./collection-filter.ts');
        const song = makeSong({ title: 'Jazz' });
        const schema = createMockSchema([song]);
        const tokens = tokenizeFilter('title = "Rock" or title notIn ["Rock", "Pop"]');
        expect(evaluateTokens(song, tokens, schema)).toBe(true);
    });
});

describe('tokenizer bracket and value parsing', () => {
    it('parses in operator with bracket list correctly', async () => {
        const { tokenizeFilter } = await import('./collection-filter.ts');
        const tokens = tokenizeFilter('title in ["Rock", "Pop", "Jazz"]');
        expect(tokens).toEqual([
            { type: 'field', value: 'title' },
            { type: 'operator', value: 'in' },
            { type: 'bracket', value: '[' },
            { type: 'value', value: 'Rock' },
            { type: 'comma', value: ',' },
            { type: 'value', value: 'Pop' },
            { type: 'comma', value: ',' },
            { type: 'value', value: 'Jazz' },
            { type: 'bracket', value: ']' },
        ]);
    });

    it('parses between with numeric values', async () => {
        const { tokenizeFilter } = await import('./collection-filter.ts');
        const tokens = tokenizeFilter('playCount between 10 and 100');
        expect(tokens).toEqual([
            { type: 'field', value: 'playCount' },
            { type: 'operator', value: 'between' },
            { type: 'value', value: '10' },
            { type: 'combinator', value: 'and' },
            { type: 'value', value: '100' },
        ]);
    });

    it('parses unary operators with no value token', async () => {
        const { tokenizeFilter } = await import('./collection-filter.ts');
        const tokens = tokenizeFilter('description isNull');
        expect(tokens).toEqual([
            { type: 'field', value: 'description' },
            { type: 'operator', value: 'isnull' },
        ]);
    });

    it('parses multiple conditions with and combinator', async () => {
        const { tokenizeFilter } = await import('./collection-filter.ts');
        const tokens = tokenizeFilter('a = "1" and b isNull');
        expect(tokens).toEqual([
            { type: 'field', value: 'a' },
            { type: 'operator', value: '=' },
            { type: 'value', value: '1' },
            { type: 'combinator', value: 'and' },
            { type: 'field', value: 'b' },
            { type: 'operator', value: 'isnull' },
        ]);
    });
});

describe('in operator with collection fields', () => {
    it('matches when any artist name is in the list', async () => {
        const { tokenizeFilter, evaluateTokens } = await import('./collection-filter.ts');
        const song = makeSong({
            artists: [
                { id: 1, name: 'Taylor Swift' },
                { id: 2, name: 'Ed Sheeran' }
            ]
        });
        const schema = createMockSchema([song]);
        const tokens = tokenizeFilter('artist.name in ["Taylor Swift", "Adele"]');
        expect(evaluateTokens(song, tokens, schema)).toBe(true);
    });

    it('does not match when no artist name is in the list', async () => {
        const { tokenizeFilter, evaluateTokens } = await import('./collection-filter.ts');
        const song = makeSong({
            artists: [
                { id: 1, name: 'Taylor Swift' },
                { id: 2, name: 'Ed Sheeran' }
            ]
        });
        const schema = createMockSchema([song]);
        const tokens = tokenizeFilter('artist.name in ["Adele", "Beyonce"]');
        expect(evaluateTokens(song, tokens, schema)).toBe(false);
    });
});

describe('notIn operator with collection fields', () => {
    it('matches when all artist names are not in the list', async () => {
        const { tokenizeFilter, evaluateTokens } = await import('./collection-filter.ts');
        const song = makeSong({
            artists: [
                { id: 1, name: 'Taylor Swift' },
                { id: 2, name: 'Ed Sheeran' }
            ]
        });
        const schema = createMockSchema([song]);
        const tokens = tokenizeFilter('artist.name notIn ["Adele", "Beyonce"]');
        expect(evaluateTokens(song, tokens, schema)).toBe(true);
    });

    it('does not match when any artist name is in the list', async () => {
        const { tokenizeFilter, evaluateTokens } = await import('./collection-filter.ts');
        const song = makeSong({
            artists: [
                { id: 1, name: 'Taylor Swift' },
                { id: 2, name: 'Ed Sheeran' }
            ]
        });
        const schema = createMockSchema([song]);
        const tokens = tokenizeFilter('artist.name notIn ["Taylor Swift", "Adele"]');
        expect(evaluateTokens(song, tokens, schema)).toBe(false);
    });
});