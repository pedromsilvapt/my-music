import {Box, Flex, LoadingOverlay} from "@mantine/core";
import {useDebouncedValue, useSelection} from '@mantine/hooks';
import {useMemo, useState} from "react";
import type {CollectionSchema} from "./collection-schema.tsx";
import CollectionToolbar, {type CollectionToolbarProps, type CollectionView} from "./collection-toolbar.tsx";
import CollectionGrid from "./views/collection-grid.tsx";
import CollectionList from "./views/collection-list.tsx";
import CollectionTable from "./views/collection-table.tsx";

export type {CollectionSchema} from "./collection-schema";

interface CollectionProps<T extends { id: string | number }> {
    items: T[],
    schema: CollectionSchema<T>,
    initialView?: CollectionView,
    toolbar?: (props: CollectionToolbarProps<T>) => React.ReactNode | null | undefined;
    isFetching?: boolean | null | undefined;
}

export default function Collection<T extends { id: string | number }>(props: CollectionProps<T>) {
    const [search, setSearch] = useState('');
    const [view, setView] = useState<CollectionView>(props.initialView ?? 'table');
    const [throttledSearch] = useDebouncedValue(search, 50);

    const filteredItems = useMemo(() => {
        if ((throttledSearch?.trim() ?? '') == '') {
            return props.items;
        }

        const searchLowerCase = throttledSearch.toLowerCase();

        return props.items.filter((item) => {
            return props.schema.searchVector(item).toLowerCase().includes(searchLowerCase);
        });
    }, [props.items, throttledSearch]);

    const [selectionKeys, selectionHandlers] = useSelection({
        data: props.items.map(item => props.schema.key(item)),
        defaultSelection: [],
        resetSelectionOnDataChange: false,
    });

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
            items={filteredItems}
            selection={selection}
            selectionHandlers={selectionHandlers}
        />;
    } else if (view == 'list') {
        viewNode = <CollectionList
            schema={props.schema}
            items={filteredItems}
            selection={selection}
            selectionHandlers={selectionHandlers}
        />;
    } else if (view === 'grid') {
        viewNode = <CollectionGrid
            schema={props.schema}
            items={filteredItems}
            selection={selection}
            selectionHandlers={selectionHandlers}
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
            actions: actions
        })}

        <Box pos="relative">
            <LoadingOverlay visible={props.isFetching ?? false} zIndex={1000} overlayProps={{radius: "sm", blur: 2}}/>
            {viewNode}
        </Box>
    </Flex>;
}
