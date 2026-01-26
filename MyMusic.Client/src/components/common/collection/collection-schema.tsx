import type {Property} from "csstype";

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
    | {
    name: string,
    renderIcon: () => React.ReactNode,
    renderLabel: () => React.ReactNode,
    onClick: (elems: M[]) => void, // TODO Event args
}

export interface CollectionSchemaColumn<M> {
    name: string;
    displayName: string;
    render: (elem: M) => React.ReactNode;
    sortable?: boolean;
    hidden?: boolean;
    width?: Property.Width<string | number> | undefined;
    align?: Property.TextAlign | undefined;
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