import type {Property} from "csstype";

export type SortDirection = 'asc' | 'desc';

export interface CollectionSortField<M> {
    field: keyof M & string;
    direction: SortDirection;
    getValue?: (elem: M) => string | number | null | undefined;
}

export type CollectionSort<M> = CollectionSortField<M>[];

export interface CollectionSchema<M> {
    key: (elem: M) => React.Key;
    actions?: (elems: M[]) => CollectionSchemaAction<M>[];

    //#region Table

    estimateTableRowHeight: (index: number) => number;
    columns: CollectionSchemaColumn<M>[];

    //#endregion Table

    //#region List

    estimateListRowHeight: (index: number) => number;
    renderListArtwork: (elem: M, size: number) => React.ReactNode;
    renderListTitle: (elem: M, lineClamp: number) => React.ReactNode;
    renderListSubTitle: (elem: M, lineClamp: number) => React.ReactNode;
    
    //#endregion List

    //#region Search

    searchVector: (elem: M) => string;

    //#endregion Search
}

export type CollectionSchemaAction<M> =
    { divider: boolean }
    | { group: string }
    | CollectionSchemaActionButton<M>;

export interface CollectionSchemaActionButton<M> {
    name: string,
    renderIcon: () => React.ReactNode,
    renderLabel: () => React.ReactNode,
    onClick: (elems: M[]) => void, // TODO Event args
    primary?: boolean;
}

export interface CollectionSchemaColumn<M> {
    name: string;
    displayName: string;
    render: (elem: M) => React.ReactNode;
    sortable?: boolean;
    hidden?: boolean;
    width?: Property.Width<string | number> | undefined;
    align?: Property.TextAlign | undefined;
    getValue?: (elem: M) => string | number | null | undefined;
}

export function getColumnWidthPixels(width: unknown): number | null {
    if (typeof width === 'number') {
        return width;
    }

    if (typeof width === 'string' && width.endsWith('px')) {
        return parseFloat(width.substring(0, width.length - 2));
    }

    return null;
}

export function getColumnWidthFractions(width: unknown): number | null {
    if (typeof width === 'number') {
        return null;
    }

    if (typeof width === 'string' && width.endsWith('fr')) {
        return parseFloat(width.substring(0, width.length - 2));
    }

    return null;
}