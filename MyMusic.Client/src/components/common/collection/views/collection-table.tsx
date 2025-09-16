import {Box, Table} from "@mantine/core";
import type {UseSelectionHandlers} from "@mantine/hooks";
import {useVirtualizer, type VirtualItem, Virtualizer} from "@tanstack/react-virtual";
import {useMemo, useRef, useState} from "react";
import {cls} from "../../../../utils/react-utils.tsx";
import CollectionActions from "../collection-actions.tsx";
import type {CollectionSchema, CollectionSchemaColumn} from "../collection-schema.tsx";
import styles from './collection-table.module.css';

export interface CollectionTableProps<M> {
    schema: CollectionSchema<M>;
    items: M[];
    selection: M[];
    selectionHandlers: UseSelectionHandlers<M>;
}

export default function CollectionTable<M>(props: CollectionTableProps<M>) {
    const columns = useMemo(() => {
        return props.schema.columns.filter(col => !col.hidden);
    }, [props.schema]);

    const parentRef = useRef<HTMLDivElement>(null)

    const virtualizer = useVirtualizer({
        count: props.items.length,
        getScrollElement: () => parentRef.current,
        estimateSize: props.schema.estimateRowHeight,
        overscan: 5,
    });

    const virtualRows = virtualizer.getVirtualItems();

    const rows = virtualRows.map((virtualRow) => {
        const row = props.items[virtualRow.index];

        return <CollectionTableRow
            key={props.schema.key(row)}
            virtualRow={virtualRow}
            virtualizer={virtualizer}
            schema={props.schema}
            row={row}
            columns={columns}
            selection={props.selection}
            selectionHandlers={props.selectionHandlers}
        />;
    });

    return <Box ref={parentRef} flex={1} style={{overflowY: "auto", maxWidth: "4000px"}}>
        <Box style={{height: `${virtualizer.getTotalSize()}px`}}>
            <Table highlightOnHover style={{borderCollapse: 'separate'}}>
                <Table.Thead>
                    <Table.Tr>
                        {columns.map(col =>
                            <Table.Th style={{width: col.width}}
                                      key={col.name}>{col.displayName}</Table.Th>
                        )}
                        <Table.Th key="__actions" style={{width: "30px"}}>{/* Actions Menu */}</Table.Th>
                    </Table.Tr>
                </Table.Thead>
                <Table.Tbody style={{transform: `translateY(${virtualRows[0]?.start ?? 0}px)`}}>
                    {rows}
                </Table.Tbody>
            </Table>
        </Box>
    </Box>;
}

interface CollectionTableRowProps<M> {
    virtualRow: VirtualItem;
    virtualizer: Virtualizer<HTMLDivElement, Element>;
    schema: CollectionSchema<M>;
    row: M;
    columns: CollectionSchemaColumn<M>[];
    selection: M[];
    selectionHandlers: UseSelectionHandlers<M>;
}

function CollectionTableRow<M>(props: CollectionTableRowProps<M>) {
    const {
        virtualRow,
        virtualizer,
        schema,
        row,
        columns,
        selection,
        selectionHandlers
    } = props;

    const [isDropdownOpen, setIsDropdownOpen] = useState(false);

    const rowActions = useMemo(() => {
        return schema.actions?.([row]) ?? [];
    }, [schema, row]);

    return (
        <Table.Tr
            data-index={virtualRow.index}
            ref={virtualizer.measureElement}
            onClick={() => selectionHandlers.toggle(row)}
            className={styles.row}
            style={selection.includes(row) ? {backgroundColor: 'var(--mantine-color-blue-light)'} : {}}
        >
            {columns.map(col =>
                <Table.Td key={col.name}
                          style={{borderBottom: 'calc(0.0625rem * var(--mantine-scale)) solid var(--table-border-color)'}}>
                    {col.render(row)}
                </Table.Td>
            )}
            <Table.Td
                key="__actions"
                style={{borderBottom: 'calc(0.0625rem * var(--mantine-scale)) solid var(--table-border-color)'}}
                className={cls(
                    styles.rowActions,
                    isDropdownOpen && styles.opened,
                    selection.length > 0 && styles.hidden
                )}
            >
                <CollectionActions selection={[row]} actions={rowActions} opened={isDropdownOpen}
                                   setOpened={setIsDropdownOpen}/>
            </Table.Td>
        </Table.Tr>
    );
}