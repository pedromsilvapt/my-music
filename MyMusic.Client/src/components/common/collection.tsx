import * as React from "react";
import {Table} from "@mantine/core";

interface CollectionProps<T extends {id: string | number}> {
    items: T[],
    columnHeaders: string[],
    columnCells: ((row: T) => React.ReactNode)[],
}

export default function Collection<T extends {id: string | number}>(props: CollectionProps<T>) {
    // row selected: bg={'var(--mantine-color-blue-light)'}
    const rows = props.items.map((row) => (
        <Table.Tr 
            key={row.id}>
            {props.columnCells.map(col => <Table.Td>{col(row)}</Table.Td>)}
        </Table.Tr>
    ));

    return <>
        <Table highlightOnHover>
            <Table.Thead>
                <Table.Tr>
                    {props.columnHeaders.map(col => <Table.Th>{col}</Table.Th>)}
                </Table.Tr>
            </Table.Thead>
            <Table.Tbody>{rows}</Table.Tbody>
        </Table>
    </>;
}