import * as React from "react";
import {Table} from "@mantine/core";

interface CollectionProps<T extends {id: string | number}> {
    items: T[],
    schema: CollectionSchema<T>,
}

export default function Collection<T extends {id: string | number}>(props: CollectionProps<T>) {
    // row selected: bg={'var(--mantine-color-blue-light)'}
    const columns = props.schema.columns.filter(col => !col.hidden);
    
    const rows = props.items.map((row) => (
        <Table.Tr 
            key={props.schema.key(row)}>
            {columns.map(col => <Table.Td>{col.render(row)}</Table.Td>)}
        </Table.Tr>
    ));

    return <>
        <Table highlightOnHover>
            <Table.Thead>
                <Table.Tr>
                    {columns.map(col => <Table.Th>{col.displayName}</Table.Th>)}
                </Table.Tr>
            </Table.Thead>
            <Table.Tbody>{rows}</Table.Tbody>
        </Table>
    </>;
}

export interface CollectionSchema<M> {
    key: (elem: M) => React.Key,
    columns: CollectionSchemaColumn<M>[];
}

export interface CollectionSchemaColumn<M> {
    name: string;
    displayName: string;
    render: (elem: M) => React.ReactNode;
    sortable?: boolean;
    hidden?: boolean;
}