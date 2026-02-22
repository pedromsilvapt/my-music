import {Box, Flex, LoadingOverlay} from "@mantine/core";
import {useDebouncedValue, useSelection, type UseSelectionHandlers} from '@mantine/hooks';
import type React from "react";
import {useCallback, useEffect, useMemo, useState} from "react";
import {SEARCH_DEBOUNCE_MS, ZINDEX_MODAL} from "../../../consts.ts";
import {sortBy} from "../../../utils/sort-by.tsx";
import type {CollectionFilterMode, CollectionSchema, CollectionSort} from "./collection-schema.tsx";
import CollectionToolbar, {type CollectionToolbarProps, type CollectionView} from "./collection-toolbar.tsx";
import SelectionFloatingBar from "./selection-floating-bar.tsx";
import CollectionGrid from "./views/collection-grid.tsx";
import CollectionList from "./views/collection-list.tsx";
import CollectionTable from "./views/collection-table.tsx";

export type {
    CollectionSchema, CollectionSort, CollectionSortField, SortDirection, CollectionFilterMode
} from "./collection-schema";

export type CollectionSelectionHandlers<T> = Omit<UseSelectionHandlers<T>, 'toggle'> & {
    toggle: (toggled: T, event?: React.MouseEvent) => void;
};

export type ItemElementRefCallback<M> = (item: M, element: HTMLElement | null) => void;

interface CollectionProps<T extends { id: string | number }> {
    items: T[],
    schema: CollectionSchema<T>,
    initialView?: CollectionView,
    toolbar?: (props: CollectionToolbarProps<T>) => React.ReactNode | null | undefined;
    isFetching?: boolean | null | undefined;
    sortable?: boolean;
    onReorder?: (fromIndex: number, toIndex: number) => void;
    onReorderBatch?: (reorders: { fromIndex: number; toIndex: number }[]) => void;
    filterMode?: CollectionFilterMode;
    serverSearch?: string;
    serverFilter?: string;
    onServerFilterChange?: (search: string, filter: string) => void;
    searchPlaceholder?: string;
}

export default function Collection<T extends { id: string | number }>(props: CollectionProps<T>) {
    const filterMode = props.filterMode ?? 'client';

    const [clientSearch, setClientSearch] = useState('');
    const [clientFilter, setClientFilter] = useState('');
    const [view, setView] = useState<CollectionView>(props.initialView ?? 'table');
    const [throttledSearch] = useDebouncedValue(clientSearch, SEARCH_DEBOUNCE_MS);
    const [sort, setSort] = useState<CollectionSort<T>>([]);
    const [lastSelectedKey, setLastSelectedKey] = useState<React.Key | null>(null);
    const [lastSelectedElement, setLastSelectedElement] = useState<HTMLElement | null>(null);

    const setItemElementRef = useCallback((_item: T, element: HTMLElement | null) => {
        if (element) {
            setLastSelectedElement(element);
        }
    }, []);

    const sortableFields = useMemo(() => {
        return props.schema.columns
            .filter(c => c.sortable)
            .map(c => c.name) as (keyof T & string)[];
    }, [props.schema.columns]);

    const handleSort = (field: string) => {
        setSort(current => {
            const existingIndex = current.findIndex(s => s.field === field);

            if (existingIndex === -1) {
                const column = props.schema.columns.find(c => c.name === field);
                const getValue = column?.getValue;
                return [...current, {field: field as keyof T & string, direction: 'asc', getValue}];
            }

            return current.map((existing, index) => index === existingIndex
                ? {...existing, direction: existing.direction === 'asc' ? 'desc' : 'asc'}
                : existing);
        });
    };

    const handleSortRemove = (field: string) => {
        setSort(current => {
            const existingIndex = current.findIndex(s => s.field === field);

            if (existingIndex === -1) {
                return current;
            }

            return current.filter((_, index) => index !== existingIndex);
        });
    };

    const handleReorderSort = (fromIndex: number, toIndex: number) => {
        setSort(current => {
            const newSort = [...current];
            const [moved] = newSort.splice(fromIndex, 1);
            newSort.splice(toIndex, 0, moved);
            return newSort;
        });
    };

    const evaluateClientFilter = useCallback((item: T, filterDsl: string): boolean => {
        if (!filterDsl.trim()) return true;

        try {
            const tokens = tokenizeFilter(filterDsl);
            return evaluateTokens(item, tokens, props.schema);
        } catch {
            return true;
        }
    }, [props.schema]);

    const filteredAndSortedItems = useMemo(() => {
        let items = props.items;

        if (filterMode === 'client') {
            if (throttledSearch.trim() !== '') {
                const searchLowerCase = throttledSearch.toLowerCase();
                items = items.filter((item) => {
                    return props.schema.searchVector(item).toLowerCase().includes(searchLowerCase);
                });
            }

            if (clientFilter.trim() !== '') {
                items = items.filter((item) => evaluateClientFilter(item, clientFilter));
            }
        }

        if (sort.length > 0) {
            items = [...items].sort(sortBy(sort));
        }

        return items;
    }, [props.items, throttledSearch, clientFilter, sort, filterMode, evaluateClientFilter, props.schema]);

    const [selectionKeys, selectionHandlers] = useSelection({
        data: props.items.map(item => props.schema.key(item)),
        defaultSelection: [],
        resetSelectionOnDataChange: false,
    });

    const handleItemClick = useCallback((clickedKey: React.Key, event: React.MouseEvent) => {
        const isCtrlPressed = event.ctrlKey || event.metaKey;
        const isShiftPressed = event.shiftKey;

        if (isShiftPressed && lastSelectedKey !== null) {
            const itemsList = filteredAndSortedItems.map(item => props.schema.key(item));
            const lastIndex = itemsList.indexOf(lastSelectedKey);
            const clickedIndex = itemsList.indexOf(clickedKey);

            if (lastIndex !== -1 && clickedIndex !== -1) {
                const start = Math.min(lastIndex, clickedIndex);
                const end = Math.max(lastIndex, clickedIndex);
                const rangeKeys = itemsList.slice(start, end + 1);

                const currentlySelected = new Set(selectionKeys);
                const allInRangeSelected = rangeKeys.every(key => currentlySelected.has(key));

                if (allInRangeSelected) {
                    selectionHandlers.setSelection(selectionKeys.filter(key => !rangeKeys.includes(key)));
                } else {
                    const newSelection = new Set([...selectionKeys, ...rangeKeys]);
                    selectionHandlers.setSelection(Array.from(newSelection));
                }
            }
        } else if (isCtrlPressed) {
            selectionHandlers.toggle(clickedKey);
        } else {
            if (selectionKeys.length === 1 && selectionKeys[0] === clickedKey) {
                selectionHandlers.resetSelection();
            } else {
                selectionHandlers.setSelection([clickedKey]);
            }
        }

        setLastSelectedKey(clickedKey);
    }, [filteredAndSortedItems, lastSelectedKey, props.schema.key, selectionKeys, selectionHandlers]);

    const customSelectionHandlers = useMemo(() => ({
        ...selectionHandlers,
        toggle: handleItemClick,
    }) as CollectionSelectionHandlers<React.Key>, [selectionHandlers, handleItemClick]);

    const selection = useMemo(() => {
        return props.items.filter((item) => selectionKeys.includes(props.schema.key(item)));
    }, [props.items, selectionKeys, props.schema]);

    useEffect(() => {
        if (selection.length === 0) {
            setLastSelectedElement(null);
        }
    }, [selection.length]);

    const actions = useMemo(() => {
        return props.schema.actions?.(selection) ?? [];
    }, [props.schema, selection]);

    let viewNode: React.ReactNode;

    if (view === 'table') {
        viewNode = <CollectionTable
            schema={props.schema}
            items={filteredAndSortedItems}
            selection={selection}
            selectionHandlers={customSelectionHandlers}
            sort={sort}
            onSort={handleSort}
            sortable={props.sortable}
            onReorder={props.onReorder}
            onReorderBatch={props.onReorderBatch}
            setItemElementRef={setItemElementRef}
            actions={actions}
        />;
    } else if (view === 'list') {
        viewNode = <CollectionList
            schema={props.schema}
            items={filteredAndSortedItems}
            selection={selection}
            selectionHandlers={customSelectionHandlers}
            sortable={props.sortable}
            onReorder={props.onReorder}
            onReorderBatch={props.onReorderBatch}
            setItemElementRef={setItemElementRef}
            actions={actions}
        />;
    } else if (view === 'grid') {
        viewNode = <CollectionGrid
            schema={props.schema}
            items={filteredAndSortedItems}
            selection={selection}
            selectionHandlers={customSelectionHandlers}
            sortable={props.sortable}
            onReorder={props.onReorder}
            onReorderBatch={props.onReorderBatch}
            setItemElementRef={setItemElementRef}
            actions={actions}
        />;
    } else {
        throw new Error(`Invalid collection view: ${view}`);
    }

    const handleSearchChange = useCallback((value: string) => {
        if (filterMode === 'client') {
            setClientSearch(value);
        } else if (filterMode === 'server') {
            props.onServerFilterChange?.(value, props.serverFilter ?? '');
        }
    }, [filterMode, props]);

    const handleFilterChange = useCallback((value: string) => {
        if (filterMode === 'client') {
            setClientFilter(value);
        } else if (filterMode === 'server') {
            props.onServerFilterChange?.(props.serverSearch ?? '', value);
        }
    }, [filterMode, props]);

    const handleApplyFilter = useCallback((filterValue: string) => {
        if (filterMode === 'client') {
            setClientFilter(filterValue);
        } else if (filterMode === 'server') {
            props.onServerFilterChange?.(props.serverSearch ?? '', filterValue);
        }
    }, [filterMode, props]);

    const currentSearch = filterMode === 'server' ? (props.serverSearch ?? '') : clientSearch;
    const currentFilter = filterMode === 'server' ? (props.serverFilter ?? '') : clientFilter;

    const toolbar = props.toolbar ?? (p => <CollectionToolbar {...p} />);

    return <Flex direction="column" style={{height: `100%`}}>
        {toolbar({
            search: currentSearch,
            setSearch: handleSearchChange,
            filter: currentFilter,
            setFilter: handleFilterChange,
            onApplyFilter: handleApplyFilter,
            filterMode: filterMode,
            searchPlaceholder: props.searchPlaceholder,
            view: view,
            setView: setView,
            sort: sort,
            onSort: handleSort,
            onSortRemove: handleSortRemove,
            onReorderSort: handleReorderSort,
            sortableFields: sortableFields,
            columns: props.schema.columns,
            filterMetadata: props.schema.filterMetadata,
            fetchFilterValues: props.schema.fetchFilterValues,
        })}

        <Box pos="relative">
            <LoadingOverlay visible={props.isFetching ?? false} zIndex={ZINDEX_MODAL}
                            overlayProps={{radius: "sm", blur: 2}}/>
            {viewNode}
        </Box>

        <SelectionFloatingBar
            selection={selection}
            actions={actions}
            anchorElement={lastSelectedElement}
            onClearSelection={selectionHandlers.resetSelection}
        />
    </Flex>;
}

interface FilterToken {
    type: 'field' | 'operator' | 'value' | 'combinator';
    value: string;
}

function tokenizeFilter(dsl: string): FilterToken[] {
    const tokens: FilterToken[] = [];
    const regex = /(\w+(?:\.\w+)*)|("[^"]*")|(\d+)|(and|or)|([=<>!~]+)/gi;
    let match: RegExpExecArray | null;

    match = regex.exec(dsl);
    while (match !== null) {
        if (match[1] && !['and', 'or'].includes(match[1].toLowerCase())) {
            tokens.push({type: 'field', value: match[1]});
        } else if (match[2]) {
            tokens.push({type: 'value', value: match[2].slice(1, -1)});
        } else if (match[3]) {
            tokens.push({type: 'value', value: match[3]});
        } else if (match[4]) {
            tokens.push({type: 'combinator', value: match[4].toLowerCase()});
        } else if (match[5]) {
            tokens.push({type: 'operator', value: match[5]});
        }
        match = regex.exec(dsl);
    }

    return tokens;
}

function evaluateTokens<T>(item: T, tokens: FilterToken[], schema: CollectionSchema<T>): boolean {
    if (tokens.length === 0) return true;

    let result = true;
    let currentCombinator: 'and' | 'or' = 'and';

    for (let i = 0; i < tokens.length; i += 3) {
        const fieldToken = tokens[i];
        const operatorToken = tokens[i + 1];
        const valueToken = tokens[i + 2];

        if (!fieldToken || !operatorToken || !valueToken) continue;
        if (fieldToken.type === 'combinator') {
            currentCombinator = fieldToken.value as 'and' | 'or';
            i -= 2;
            continue;
        }

        const fieldValue = getFieldValue(item, fieldToken.value, schema);
        const conditionResult = evaluateCondition(fieldValue, operatorToken.value, valueToken.value);

        if (currentCombinator === 'and') {
            result = result && conditionResult;
        } else {
            result = result || conditionResult;
        }

        const nextToken = tokens[i + 3];
        if (nextToken?.type === 'combinator') {
            currentCombinator = nextToken.value as 'and' | 'or';
            i++;
        }
    }

    return result;
}

function getFieldValue<T>(item: T, fieldPath: string, schema: CollectionSchema<T>): unknown {
    const searchVector = schema.searchVector(item).toLowerCase();

    if (fieldPath.toLowerCase().includes('searchabletext') || fieldPath.toLowerCase() === 'search') {
        return searchVector;
    }

    const parts = fieldPath.split('.');
    let value: unknown = item;

    for (const part of parts) {
        if (value && typeof value === 'object') {
            value = (value as Record<string, unknown>)[part];
        } else {
            return undefined;
        }
    }

    return value;
}

function evaluateCondition(fieldValue: unknown, operator: string, compareValue: string): boolean {
    const compareNum = parseFloat(compareValue);
    const compareStr = compareValue.toLowerCase();
    const fieldStr = String(fieldValue ?? '').toLowerCase();

    switch (operator) {
        case '=':
        case '==':
            return fieldStr === compareStr || (typeof fieldValue === 'number' && fieldValue === compareNum);
        case '!=':
        case '<>':
            return fieldStr !== compareStr && (typeof fieldValue !== 'number' || fieldValue !== compareNum);
        case '>':
            return typeof fieldValue === 'number' && fieldValue > compareNum;
        case '>=':
            return typeof fieldValue === 'number' && fieldValue >= compareNum;
        case '<':
            return typeof fieldValue === 'number' && fieldValue < compareNum;
        case '<=':
            return typeof fieldValue === 'number' && fieldValue <= compareNum;
        case '~':
        case 'contains':
            return fieldStr.includes(compareStr);
        case 'startsWith':
            return fieldStr.startsWith(compareStr);
        case 'endsWith':
            return fieldStr.endsWith(compareStr);
        default:
            return true;
    }
}
