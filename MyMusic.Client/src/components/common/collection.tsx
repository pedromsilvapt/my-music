import * as React from "react";
import {Table} from "@mantine/core";
import {useVirtualizer} from "@tanstack/react-virtual";
import type {Property} from "csstype";

interface CollectionProps<T extends {id: string | number}> {
    items: T[],
    schema: CollectionSchema<T>,
}

export default function Collection<T extends {id: string | number}>(props: CollectionProps<T>) {
    // row selected: bg={'var(--mantine-color-blue-light)'}
    const columns = props.schema.columns.filter(col => !col.hidden);
    
    const parentRef = React.useRef<HTMLDivElement>(null)

    const virtualizer = useVirtualizer({
        count: props.items.length,
        getScrollElement: () => parentRef.current,
        estimateSize: props.schema.estimateRowHeight,
        
        overscan: 5,
    })

    const virtualRows = virtualizer.getVirtualItems();
    
    const rows = virtualRows.map((virtualRow) => {
        const row = props.items[virtualRow.index];
        
        return <Table.Tr 
            data-index={virtualRow.index} 
            ref={virtualizer.measureElement}
            key={props.schema.key(row)}>
            {columns.map(col => <Table.Td style={{borderBottom: 'calc(0.0625rem * var(--mantine-scale)) solid var(--table-border-color)'}}>{col.render(row)}</Table.Td>)}
        </Table.Tr>;
    });

    return <>
        <div ref={parentRef} style={{ height: `100%`, overflowY: "auto" }}>
            <div style={{ height: `${virtualizer.getTotalSize()}px` }}>
                <Table highlightOnHover style={{borderCollapse: 'separate'}}>
                    <Table.Thead>
                        <Table.Tr>
                            {columns.map(col => <Table.Th style={{width: col.width}}>{col.displayName}</Table.Th>)}
                        </Table.Tr>
                    </Table.Thead>
                    <Table.Tbody style={{transform: `translateY(${virtualRows[0]?.start ?? 0}px)`}}>
                        {rows}
                    </Table.Tbody>
                </Table>
            </div>
        </div>
    </>;
}

export interface CollectionSchema<M> {
    key: (elem: M) => React.Key;
    estimateRowHeight: (index: number) => number;
    columns: CollectionSchemaColumn<M>[];
}

export interface CollectionSchemaColumn<M> {
    name: string;
    displayName: string;
    render: (elem: M) => React.ReactNode;
    sortable?: boolean;
    hidden?: boolean;
    width?: Property.Width<string | number> | undefined;
}