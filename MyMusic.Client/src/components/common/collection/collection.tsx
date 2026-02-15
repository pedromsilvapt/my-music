import {Box, Flex, LoadingOverlay} from "@mantine/core";
import {useDebouncedValue, useSelection, type UseSelectionHandlers} from '@mantine/hooks';
import type React from "react";
import {useCallback, useMemo, useState} from "react";
import {sortBy} from "../../../utils/sort-by.tsx";
import type {CollectionSchema, CollectionSort} from "./collection-schema.tsx";
import CollectionToolbar, {type CollectionToolbarProps, type CollectionView} from "./collection-toolbar.tsx";
import CollectionGrid from "./views/collection-grid.tsx";
import CollectionList from "./views/collection-list.tsx";
import CollectionTable from "./views/collection-table.tsx";

export type {CollectionSchema, CollectionSort, CollectionSortField, SortDirection} from "./collection-schema";

export type CollectionSelectionHandlers<T> = Omit<UseSelectionHandlers<T>, 'toggle'> & {
    toggle: (toggled: T, event?: React.MouseEvent) => void;
};

interface CollectionProps<T extends { id: string | number }> {
    items: T[],
    schema: CollectionSchema<T>,
    initialView?: CollectionView,
    toolbar?: (props: CollectionToolbarProps<T>) => React.ReactNode | null | undefined;
    isFetching?: boolean | null | undefined;
    sortable?: boolean;
    onReorder?: (fromIndex: number, toIndex: number) => void;
    onReorderBatch?: (reorders: { fromIndex: number; toIndex: number }[]) => void;
}

export default function Collection<T extends { id: string | number }>(props: CollectionProps<T>) {
    const [search, setSearch] = useState('');
    const [view, setView] = useState<CollectionView>(props.initialView ?? 'table');
    const [throttledSearch] = useDebouncedValue(search, 50);
    const [sort, setSort] = useState<CollectionSort<T>>([]);
    const [lastSelectedKey, setLastSelectedKey] = useState<React.Key | null>(null);

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

    const filteredAndSortedItems = useMemo(() => {
        let items = props.items;

        if ((throttledSearch.trim()) != '') {
            const searchLowerCase = throttledSearch.toLowerCase();
            items = items.filter((item) => {
                return props.schema.searchVector(item).toLowerCase().includes(searchLowerCase);
            });
        }

        if (sort.length > 0) {
            items = [...items].sort(sortBy(sort));
        }

        return items;
    }, [props.items, throttledSearch, sort]);

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
    }, [props.items, selectionKeys]);


    const actions = useMemo(() => {
        return props.schema.actions?.(selection) ?? [];
    }, [props.schema, selection]);

    let viewNode: React.ReactNode;

    if (view == 'table') {
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
        />;
    } else if (view == 'list') {
        viewNode = <CollectionList
            schema={props.schema}
            items={filteredAndSortedItems}
            selection={selection}
            selectionHandlers={customSelectionHandlers}
            sortable={props.sortable}
            onReorder={props.onReorder}
            onReorderBatch={props.onReorderBatch}
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
        />;
    } else {
        throw new Error(`Invalid collection view: ${view}`);
    }

    const toolbar = props.toolbar ?? (p => <CollectionToolbar {...p} />);

    return <Flex direction="column" style={{height: `100%`}}>
        {toolbar({
            search: search,
            setSearch: setSearch,
            view: view,
            setView: setView,
            selection: selection,
            onClearSelection: selectionHandlers.resetSelection,
            actions: actions,
            sort: sort,
            onSort: handleSort,
            onSortRemove: handleSortRemove,
            onReorderSort: handleReorderSort,
            sortableFields: sortableFields,
            columns: props.schema.columns,
        })}

        <Box pos="relative">
            <LoadingOverlay visible={props.isFetching ?? false} zIndex={1000} overlayProps={{radius: "sm", blur: 2}}/>
            {viewNode}
        </Box>
    </Flex>;
}
