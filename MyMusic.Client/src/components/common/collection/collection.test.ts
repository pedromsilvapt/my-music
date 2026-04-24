import { describe, expect, it } from 'vitest';
import type { CollectionSchema, FilterMetadataResponse } from './collection-schema';

interface TestSong {
    id: number;
    title: string;
    artists: Array<{ id: number; name: string }>;
    genres: Array<{ id: number; name: string }>;
    devices: Array<{ id: number; name: string }>;
    album: { id: number; name: string };
}

const mockFilterMetadata: FilterMetadataResponse = {
    fields: [
        {
            name: 'artist.name',
            type: 'string',
            description: 'Artist name filter',
            supportedOperators: ['=', '!=', 'contains', 'startsWith', 'endsWith'],
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
