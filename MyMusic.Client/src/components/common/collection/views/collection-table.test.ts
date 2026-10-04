import { describe, expect, it } from 'vitest';
import type { CollectionSchemaColumn } from '../collection-schema';
import { ACTIONS_COLUMN_WIDTH, computeColumnWidths } from './collection-table';

interface TestRow {
    id: number;
}

function column(name: string, overrides: Partial<CollectionSchemaColumn<TestRow>> = {}): CollectionSchemaColumn<TestRow> {
    return {
        name,
        displayName: name,
        render: () => null,
        ...overrides
    } as CollectionSchemaColumn<TestRow>;
}

function widths(columns: CollectionSchemaColumn<TestRow>[], tableWidth: number) {
    return computeColumnWidths(columns, tableWidth).map(col => col.width);
}

describe('computeColumnWidths', () => {
    it('splits the space left by fixed columns between fractional ones', () => {
        const result = widths([
            column('cover', { width: 40 }),
            column('title', { width: '2fr' }),
            column('album', { width: '1fr' }),
            column('year', { width: '100px' }),
        ], 800);

        // 800 - 40 - 100 - actions (60) = 600 free, split 2:1
        expect(result).toEqual([40, 400, 200, 100]);
    });

    it('fills the table width together with the actions column', () => {
        const result = widths([
            column('cover', { width: 52 }),
            column('title', { width: '2fr' }),
            column('album', { width: '1.5fr' }),
        ], 1000);

        const total = result.reduce<number>((sum, width) => sum + (width ?? 0), 0);
        expect(total + ACTIONS_COLUMN_WIDTH).toBeCloseTo(1000);
    });

    it('treats columns without a width as one fraction', () => {
        const result = widths([
            column('title', { width: '1fr' }),
            column('album'),
        ], 460);

        expect(result).toEqual([200, 200]);
    });

    it('ignores hidden columns', () => {
        const result = computeColumnWidths([
            column('title', { width: '1fr' }),
            column('createdAt', { width: 300, hidden: true }),
        ], 460);

        expect(result.map(col => col.name)).toEqual(['title']);
        expect(result[0].width).toBe(400);
    });

    it('keeps fractional columns visible when fixed columns exceed the table width', () => {
        const result = widths([
            column('cover', { width: 300 }),
            column('title', { width: '2fr' }),
            column('album', { width: '1fr' }),
        ], 200);

        expect(result).toEqual([300, 120, 60]);
    });
});
