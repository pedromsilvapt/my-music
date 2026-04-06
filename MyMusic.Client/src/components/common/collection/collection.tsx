import {Box, Flex, LoadingOverlay} from "@mantine/core";
import {useDebouncedValue, useElementSize} from '@mantine/hooks';
import type React from "react";
import {useCallback, useEffect, useMemo, useRef, useState} from "react";
import {useShallow} from "zustand/react/shallow";
import {SCROLL_DEBOUNCE_MS, ZINDEX_MODAL} from "../../../consts.ts";
import type {ScrollPosition} from "../../../contexts/collection-context.tsx";
import {useCollectionActions} from "../../../contexts/collection-context.tsx";
import useFilter from "../../../hooks/useFilter.ts";
import {useContextMenuTrigger} from "../../../hooks/use-context-menu-trigger";
import {sortBy} from "../../../utils/sort-by.tsx";
import {ContextMenuPortal} from "../context-menu-portal.tsx";
import type {CollectionFilterMode, CollectionSchema, CollectionSchemaAction, CollectionSort} from "./collection-schema.tsx";
import CollectionToolbar, {type CollectionToolbarProps, type CollectionView} from "./collection-toolbar.tsx";
import {CollectionActionMenu} from "./collection-actions.tsx";
import {createSelectionStore, SelectionStoreProvider} from "./selection-store.ts";
import SelectionFloatingBar from "./selection-floating-bar.tsx";
import CollectionGrid from "./views/collection-grid.tsx";
import CollectionList from "./views/collection-list.tsx";
import CollectionTable from "./views/collection-table.tsx";

export type {
    CollectionSchema, CollectionSort, CollectionSortField, SortDirection, CollectionFilterMode
} from "./collection-schema";

interface CollectionProps<T extends { id: string | number }> {
    items: T[],
    schema: CollectionSchema<T>,
    initialView?: CollectionView,
    stateKey?: string,
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
    scrollToSongId?: number | string;
    scrollRequestId?: number;
}

const MIN_VIEW_HEIGHT = 200;

export default function Collection<T extends { id: string | number }>(props: CollectionProps<T>) {
    const filterMode = props.filterMode ?? 'client';
    const stateKey = props.stateKey;

    const {ref: containerRef, height: containerHeight} = useElementSize<HTMLDivElement>();
    const {ref: toolbarRef, height: toolbarHeight} = useElementSize<HTMLDivElement>();
    const collectionContainerRef = useRef<HTMLDivElement>(null);
    const floatingBarPortalTargetRef = useRef<HTMLDivElement>(null);

    const viewHeight = Math.max(MIN_VIEW_HEIGHT, containerHeight - toolbarHeight);

    const {
        getCollectionState,
        setCollectionView,
        setCollectionSort,
        setCollectionFilter,
        setCollectionScrollPosition
    } =
        useCollectionActions(useShallow(state => ({
            getCollectionState: state.getCollectionState,
            setCollectionView: state.setCollectionView,
            setCollectionSort: state.setCollectionSort,
            setCollectionFilter: state.setCollectionFilter,
            setCollectionScrollPosition: state.setCollectionScrollPosition,
        })));

    const storeState = stateKey ? getCollectionState(stateKey) : null;
    const initialView = props.initialView ?? 'table';

    // Memoize onChange callbacks for useFilter to prevent unnecessary re-renders
    const handleServerSearchChange = useCallback((value: string) => {
        props.onServerFilterChange?.(value, props.serverFilter ?? '');
    }, [props.onServerFilterChange, props.serverFilter]);

    const handleServerFilterChange = useCallback((value: string) => {
        props.onServerFilterChange?.(props.serverSearch ?? '', value);
    }, [props.onServerFilterChange, props.serverSearch]);

    // Unified filter state using useFilter hook
    // For client mode: internal state
    // For server mode: controlled by parent
    const searchFilter = useFilter({
        value: filterMode === 'server' ? props.serverSearch : undefined,
        defaultValue: storeState?.filter?.search ?? '',
        onChange: filterMode === 'server' ? handleServerSearchChange : undefined,
        debounceMs: filterMode === 'server' ? 300 : 0,
    });

    const filterExpression = useFilter({
        value: filterMode === 'server' ? props.serverFilter : undefined,
        defaultValue: storeState?.filter?.expression ?? '',
        onChange: filterMode === 'server' ? handleServerFilterChange : undefined,
        debounceMs: 0, // Filter expression applies on demand, not on every keystroke
    });

    const [view, setView] = useState<CollectionView>(storeState?.view ?? initialView);
    const [sort, setSort] = useState<CollectionSort<T>>((storeState?.sort as CollectionSort<T>) ?? []);
    const [scrollPosition, setScrollPosition] = useState<ScrollPosition | null>(storeState?.scrollPosition ?? null);

    const [debouncedScrollPosition] = useDebouncedValue(scrollPosition, SCROLL_DEBOUNCE_MS);

    const contextMenuId = useMemo(() => `collection-${stateKey ?? 'default'}`, [stateKey]);
    const {trigger: onContextMenuTrigger} = useContextMenuTrigger(contextMenuId);
    const contextMenuSelectionRef = useRef<T[]>([]);
    const contextMenuActionsRef = useRef<CollectionSchemaAction<T>[]>([]);

    const initialScrollPositionRef = useRef<ScrollPosition | null>(null);
    if (initialScrollPositionRef.current === null && storeState?.scrollPosition != null) {
        initialScrollPositionRef.current = storeState.scrollPosition;
    }

    const selectionStore = useMemo(() => createSelectionStore(), []);

    // Persist state to store
    useEffect(() => {
        if (stateKey) {
            setCollectionView(stateKey, view);
        }
    }, [stateKey, view, setCollectionView]);

    useEffect(() => {
        if (stateKey) {
            setCollectionSort(stateKey, sort as CollectionSort<unknown>);
        }
    }, [stateKey, sort, setCollectionSort]);

    useEffect(() => {
        if (stateKey) {
            setCollectionFilter(stateKey, {
                search: searchFilter.value,
                expression: filterExpression.value,
            });
        }
    }, [stateKey, searchFilter.value, filterExpression.value, setCollectionFilter]);

    useEffect(() => {
        if (stateKey && debouncedScrollPosition != null && debouncedScrollPosition !== storeState?.scrollPosition) {
            setCollectionScrollPosition(stateKey, debouncedScrollPosition);
        }
    }, [stateKey, debouncedScrollPosition, setCollectionScrollPosition, storeState?.scrollPosition]);

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
            // For client mode, use the (optionally debounced) search value
            const searchValue = searchFilter.debouncedValue;
            if (searchValue.trim() !== '') {
                const searchLowerCase = searchValue.toLowerCase();
                items = items.filter((item) => {
                    return props.schema.searchVector(item).toLowerCase().includes(searchLowerCase);
                });
            }

        if (filterExpression.value.trim() !== '') {
            items = items.filter((item) => evaluateClientFilter(item, filterExpression.value));
        }
        }

        if (sort.length > 0) {
            items = [...items].sort(sortBy(sort));
        }

        return items;
    }, [props.items, searchFilter.debouncedValue, filterExpression.value, sort, filterMode, evaluateClientFilter, props.schema]);

    const handleItemClick = useCallback((clickedKey: React.Key, event: React.MouseEvent) => {
        const isCtrlPressed = event.ctrlKey || event.metaKey;
        const isShiftPressed = event.shiftKey;
        const isTouchDevice = window.matchMedia('(pointer: coarse)').matches;
        const currentSelectionKeys = selectionStore.getState().selectedKeys;
        const lastSelectedKey = selectionStore.getState().lastSelectedKey;

        if (event.button === 2) {
            if (!currentSelectionKeys.has(clickedKey)) {
                selectionStore.getState().setSelection([clickedKey]);
                selectionStore.getState().setLastSelectedKey(clickedKey);
            }
            return;
        }

        if (isShiftPressed && lastSelectedKey !== null) {
            const itemsList = filteredAndSortedItems.map(item => props.schema.key(item));
            const lastIndex = itemsList.indexOf(lastSelectedKey);
            const clickedIndex = itemsList.indexOf(clickedKey);

            if (lastIndex !== -1 && clickedIndex !== -1) {
                const start = Math.min(lastIndex, clickedIndex);
                const end = Math.max(lastIndex, clickedIndex);
                const rangeKeys = itemsList.slice(start, end + 1);

                const allInRangeSelected = rangeKeys.every(key => currentSelectionKeys.has(key));

                if (allInRangeSelected) {
                    selectionStore.getState().setSelection(Array.from(currentSelectionKeys).filter(key => !rangeKeys.includes(key)));
                } else {
                    const newSelection = new Set([...Array.from(currentSelectionKeys), ...rangeKeys]);
                    selectionStore.getState().setSelection(Array.from(newSelection));
                }
            }
        } else if (isCtrlPressed || isTouchDevice) {
            const newKeys = new Set(currentSelectionKeys);
            if (newKeys.has(clickedKey)) {
                newKeys.delete(clickedKey);
            } else {
                newKeys.add(clickedKey);
            }
            selectionStore.getState().setSelection(Array.from(newKeys));
        } else {
            if (currentSelectionKeys.size === 1 && currentSelectionKeys.has(clickedKey)) {
                selectionStore.getState().reset();
            } else {
                selectionStore.getState().setSelection([clickedKey]);
            }
        }

        selectionStore.getState().setLastSelectedKey(clickedKey);
    }, [filteredAndSortedItems, props.schema, selectionStore]);

    const handleSelectAll = useCallback(() => {
        const allKeys = filteredAndSortedItems.map(item => props.schema.key(item));
        selectionStore.getState().setSelection(allKeys);
    }, [filteredAndSortedItems, props.schema, selectionStore]);

    const handleClearSelection = useCallback(() => {
        selectionStore.getState().reset();
    }, [selectionStore]);

    const handleScrollPositionChange = useCallback((position: ScrollPosition) => {
        setScrollPosition(position);
    }, []);

    const handleContextMenuTrigger = useCallback((
        event: React.MouseEvent | React.TouchEvent,
        rowActions: CollectionSchemaAction<T>[],
        rowSelection: T[]
    ) => {
        contextMenuSelectionRef.current = rowSelection;
        contextMenuActionsRef.current = rowActions;
        onContextMenuTrigger(event);
    }, [onContextMenuTrigger]);

    const scrollToIndex = useMemo(() => {
        if (props.scrollToSongId == null) return undefined;
        return filteredAndSortedItems.findIndex(item => props.schema.key(item) === props.scrollToSongId);
    }, [filteredAndSortedItems, props.scrollToSongId, props.schema]);

    let viewNode: React.ReactNode;

    if (view === 'table') {
        viewNode = <CollectionTable
            schema={props.schema}
            items={filteredAndSortedItems}
            selectionStore={selectionStore}
            onToggle={handleItemClick}
            sort={sort}
            onSort={handleSort}
            sortable={props.sortable}
            onReorder={props.onReorder}
            onReorderBatch={props.onReorderBatch}
            initialScrollPosition={initialScrollPositionRef.current ?? undefined}
            onScrollPositionChange={handleScrollPositionChange}
            scrollToIndex={scrollToIndex}
            scrollRequestId={props.scrollRequestId}
            height={viewHeight}
            onContextMenuTrigger={handleContextMenuTrigger}
        />;
    } else if (view === 'list') {
        viewNode = <CollectionList
            schema={props.schema}
            items={filteredAndSortedItems}
            selectionStore={selectionStore}
            onToggle={handleItemClick}
            sortable={props.sortable}
            onReorder={props.onReorder}
            onReorderBatch={props.onReorderBatch}
            initialScrollPosition={initialScrollPositionRef.current ?? undefined}
            onScrollPositionChange={handleScrollPositionChange}
            scrollToIndex={scrollToIndex}
            scrollRequestId={props.scrollRequestId}
            height={viewHeight}
            onContextMenuTrigger={handleContextMenuTrigger}
        />;
    } else if (view === 'grid') {
        viewNode = <CollectionGrid
            schema={props.schema}
            items={filteredAndSortedItems}
            selectionStore={selectionStore}
            onToggle={handleItemClick}
            sortable={props.sortable}
            onReorder={props.onReorder}
            onReorderBatch={props.onReorderBatch}
            initialScrollPosition={initialScrollPositionRef.current ?? undefined}
            onScrollPositionChange={handleScrollPositionChange}
            scrollToIndex={scrollToIndex}
            scrollRequestId={props.scrollRequestId}
            height={viewHeight}
            onContextMenuTrigger={handleContextMenuTrigger}
        />;
    } else {
        throw new Error(`Invalid collection view: ${view}`);
    }

    // Unified handlers that work for both client and server modes
    const handleSearchChange = useCallback((value: string) => {
        searchFilter.setValue(value);
    }, [searchFilter]);

    const handleFilterChange = useCallback((value: string) => {
        filterExpression.setValue(value);
    }, [filterExpression]);

    const handleApplyFilter = useCallback((value: string) => {
        filterExpression.setValue(value);
    }, [filterExpression]);

    const toolbar = props.toolbar ?? (p => <CollectionToolbar {...p} />);

    return <Flex ref={containerRef} direction="column" style={{height: `100%`}}>
        <Box ref={toolbarRef}>
            {toolbar({
                search: searchFilter.value,
                setSearch: handleSearchChange,
                filter: filterExpression.value,
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
                selectionStore: selectionStore,
                totalItems: filteredAndSortedItems.length,
                onSelectAll: handleSelectAll,
                onClearSelection: handleClearSelection,
            })}
        </Box>

        <Box ref={collectionContainerRef} pos="relative" flex={1}
             style={{minHeight: MIN_VIEW_HEIGHT, overflow: 'hidden'}}>
            <LoadingOverlay visible={props.isFetching ?? false} zIndex={ZINDEX_MODAL}
                            overlayProps={{radius: "sm", blur: 2}}/>
            <SelectionStoreProvider store={selectionStore}>
                {viewNode}
            </SelectionStoreProvider>
            <div ref={floatingBarPortalTargetRef} />
        </Box>

        <ContextMenuPortal menuId={contextMenuId} content={() => {
            let dividerCount = 0;
            return contextMenuActionsRef.current.map((action) => {
                // Handle divider
                if ('divider' in action) {
                    dividerCount++;
                    return <div key={`divider-${dividerCount}`} className="context-menu-divider" />;
                }
                // Handle group
                if ('group' in action) {
                    return <div key={`group-${action.group}`} className="context-menu-group">{action.group}</div>;
                }
                // Handle action button
                return <CollectionActionMenu key={`action-${action.name}`} action={action} selection={contextMenuSelectionRef.current} />;
            });
        }} />

        <SelectionFloatingBar
            items={props.items}
            itemKey={props.schema.key}
            selectionStore={selectionStore}
            actionsFn={props.schema.actions}
            containerRef={collectionContainerRef}
            portalTarget={floatingBarPortalTargetRef}
            onClearSelection={handleClearSelection}
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
