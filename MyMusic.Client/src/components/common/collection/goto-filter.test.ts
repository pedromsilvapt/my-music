import {describe, expect, it} from 'vitest';
import {buildGoToSearchVectors, filterGoToItems} from './goto-filter';

interface TestItem {
    id: number;
    title: string;
    artist: string;
}

const schema = {searchVector: (item: TestItem) => `${item.title} - ${item.artist}`};

const items: TestItem[] = [
    {id: 1, title: 'Bohemian Rhapsody', artist: 'Queen'},
    {id: 2, title: 'Stairway to Heaven', artist: 'Led Zeppelin'},
    {id: 3, title: 'Under Pressure', artist: 'Queen & David Bowie'},
];

const vectors = buildGoToSearchVectors(items, schema);

describe('buildGoToSearchVectors', () => {
    it('lowercases each item search vector, preserving order', () => {
        expect(vectors).toEqual([
            'bohemian rhapsody - queen',
            'stairway to heaven - led zeppelin',
            'under pressure - queen & david bowie',
        ]);
    });
});

describe('filterGoToItems', () => {
    it('returns all items in their original order when the search is empty', () => {
        expect(filterGoToItems(items, vectors, '').map(i => i.id)).toEqual([1, 2, 3]);
        expect(filterGoToItems(items, vectors, '   ').map(i => i.id)).toEqual([1, 2, 3]);
    });

    it('matches case-insensitively against the search vector', () => {
        expect(filterGoToItems(items, vectors, 'queen').map(i => i.id)).toEqual([1, 3]);
        expect(filterGoToItems(items, vectors, 'HEAVEN').map(i => i.id)).toEqual([2]);
    });

    it('returns an empty list when nothing matches', () => {
        expect(filterGoToItems(items, vectors, 'nonexistent')).toEqual([]);
    });

    it('does not cap the number of results', () => {
        const many = Array.from({length: 10_000}, (_, i) => ({id: i, title: `Song ${i}`, artist: 'Artist'}));
        const manyVectors = buildGoToSearchVectors(many, schema);

        expect(filterGoToItems(many, manyVectors, 'song')).toHaveLength(10_000);
        expect(filterGoToItems(many, manyVectors, 'song 999').map(i => i.id)).toEqual([999, 9990, 9991, 9992, 9993, 9994, 9995, 9996, 9997, 9998, 9999]);
    });
});
