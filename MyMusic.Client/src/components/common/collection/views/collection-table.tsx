import {Box, Table} from "@mantine/core";
import {useElementSize, type UseSelectionHandlers} from "@mantine/hooks";
import {useVirtualizer, type VirtualItem, Virtualizer} from "@tanstack/react-virtual";
import {useMemo, useRef, useState} from "react";
import {cls} from "../../../../utils/react-utils.tsx";
import CollectionActions from "../collection-actions.tsx";
import {
    type CollectionSchema,
    type CollectionSchemaColumn,
    getColumnWidthFractions,
    getColumnWidthPixels
} from "../collection-schema.tsx";
import styles from './collection-table.module.css';

export interface CollectionTableProps<M> {
    schema: CollectionSchema<M>;
    items: M[];
    selection: M[];
    selectionHandlers: UseSelectionHandlers<React.Key>;
}

export default function CollectionTable<M>(props: CollectionTableProps<M>) {
    const {ref: tableRef, width: tableWidth} = useElementSize();
    const {ref: tableHeaderRef, height: tableHeaderHeight} = useElementSize();
    
    const columns = useMemo(() => {
        const columns = props.schema.columns.filter(col => !col.hidden);

        const fixedWidth = columns
            .map(c => getColumnWidthPixels(c.width))
            .filter(width => width != null)
            .reduce((sum, width) => sum + width, 0);

        // We will force our table to have at least this size
        const totalWidth = Math.max(tableWidth, fixedWidth);
        const freeWidth = totalWidth - fixedWidth - 100; // NOTE Also remove the size of additional columns (like the actions column)

        const freeFractions = columns
            .map(c => getColumnWidthFractions(c.width))
            .filter(width => width != null)
            .reduce((sum, width) => sum + width, 0);

        return columns.map(col => ({
            ...col,
            width: getActualColumnWidth(col.width, freeWidth, freeFractions),
        }) as CollectionSchemaColumnCalculated<M>);
    }, [props.schema.columns, tableWidth]);

    const parentRef = useRef<HTMLDivElement>(null)

    const virtualizer = useVirtualizer({
        count: props.items.length,
        getScrollElement: () => parentRef.current,
        estimateSize: props.schema.estimateTableRowHeight,
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

    return <Box ref={parentRef} flex={1} style={{overflowY: "auto", maxHeight: "813px"}}>
        <Box style={{height: `${virtualizer.getTotalSize() + tableHeaderHeight}px`}}>
            <Table highlightOnHover ref={tableRef} style={{
                borderCollapse: 'separate',
            }}>
                <Table.Thead ref={tableHeaderRef}>
                    <Table.Tr>
                        {columns.map(col =>
                            <Table.Th style={{width: col.width, textAlign: col.align ?? 'left'}}
                                      key={col.name}>{col.displayName}
                            </Table.Th>
                        )}
                        <Table.Th key="__actions" style={{width: "60px"}}>{/* Actions Menu */}</Table.Th>
                    </Table.Tr>
                </Table.Thead>
                <Table.Tbody style={{
                    transform: `translateY(${virtualRows[0]?.start ?? 0}px)`
                }}>
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
    selectionHandlers: UseSelectionHandlers<React.Key>;
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
            onClick={() => selectionHandlers.toggle(schema.key(row))}
            className={cls(
                styles.row,
                selection.includes(row) && styles.selected,
            )}
        >
            {columns.map(col =>
                <Table.Td key={col.name}
                          style={{
                              borderBottom: 'calc(0.0625rem * var(--mantine-scale)) solid var(--table-border-color)',
                              textAlign: col.align ?? 'left'
                          }}>
                    {col.render(row)}
                </Table.Td>
            )}
            <Table.Td
                key="__actions"
                style={{
                    borderBottom: 'calc(0.0625rem * var(--mantine-scale)) solid var(--table-border-color)'
                }}
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

export interface CollectionSchemaColumnCalculated<M> extends CollectionSchemaColumn<M> {
    width?: number;
}

export function getActualColumnWidth(width: unknown, freeWidth: number, freeFractions: number) {
    const fixedWidth = getColumnWidthPixels(width);

    if (fixedWidth != null) {
        return fixedWidth;
    }

    const fractions = getColumnWidthFractions(width);

    if (fractions != null) {
        if (freeFractions === 0) {
            // Should never happen: if `freeFractions === 0`, it should mean that all `fixedWidth != null` as well, so we should never reach here
            // If we do, it is a bug in whatever function is calling this one
            throw new Error(`Invalid state: no free fractions for column widths, but this column does not have a fixed size!`);
        }

        return freeWidth / freeFractions * fractions;
    }

    return null;
}