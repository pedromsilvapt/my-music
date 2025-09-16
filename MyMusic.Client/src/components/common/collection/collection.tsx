import {Flex} from "@mantine/core";
import {useDebouncedValue, useSelection} from '@mantine/hooks';
import {useMemo, useState} from "react";
import type {CollectionSchema} from "./collection-schema.tsx";
import CollectionToolbar from "./collection-toolbar.tsx";
import CollectionTable from "./views/collection-table.tsx";

export type {CollectionSchema} from "./collection-schema";

interface CollectionProps<T extends { id: string | number }> {
    items: T[],
    schema: CollectionSchema<T>,
}

export default function Collection<T extends { id: string | number }>(props: CollectionProps<T>) {
    const [search, setSearch] = useState('')
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

    const [selection, selectionHandlers] = useSelection({
        data: props.items,
        defaultSelection: [],
    });

    const actions = useMemo(() => {
        return props.schema.actions?.(selection) ?? [];
    }, [props.schema, selection]);

    return <Flex direction="column" style={{height: `100%`}}>
        <CollectionToolbar
            search={search}
            setSearch={setSearch}
            selection={selection}
            onClearSelection={selectionHandlers.resetSelection}
            actions={actions}/>

        <CollectionTable
            schema={props.schema}
            items={filteredItems}
            selection={selection}
            selectionHandlers={selectionHandlers}
        />
    </Flex>;
}
