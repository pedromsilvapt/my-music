import type {Property} from "csstype";

export interface CollectionSchema<M> {
    key: (elem: M) => React.Key;
    actions?: (elems: M[]) => CollectionSchemaAction<M>[];

    //#region Table

    estimateRowHeight: (index: number) => number;
    columns: CollectionSchemaColumn<M>[];

    //#endregion

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