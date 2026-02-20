import {
    closestCenter,
    DndContext,
    type DragEndEvent,
    DragOverlay,
    type DragStartEvent,
    KeyboardSensor,
    PointerSensor,
    useSensor,
    useSensors,
} from "@dnd-kit/core";
import {SortableContext, useSortable, verticalListSortingStrategy} from "@dnd-kit/sortable";
import {CSS} from "@dnd-kit/utilities";
import {Box, Group, Table, Text} from "@mantine/core";
import {useElementSize} from "@mantine/hooks";
import {IconArrowDown, IconArrowUp, IconSelector} from "@tabler/icons-react";
import {useVirtualizer, type VirtualItem, Virtualizer} from "@tanstack/react-virtual";
import {useContextMenu} from "mantine-contextmenu";
import {useCallback, useMemo, useRef, useState} from "react";
import {DRAG_ACTIVATION_DISTANCE, VIRTUALIZER_OVERSCAN} from "../../../../consts.ts";
import {isInteractiveElement} from "../../../../utils/event-utils.ts";
import {cls} from "../../../../utils/react-utils.tsx";
import CollectionActions from "../collection-actions.tsx";
import {
    type CollectionSchema,
    type CollectionSchemaAction,
    type CollectionSchemaColumn,
    type CollectionSortField,
    getColumnWidthFractions,
    getColumnWidthPixels
} from "../collection-schema.tsx";
import type {CollectionSelectionHandlers, ItemElementRefCallback} from "../collection.tsx";
import styles from './collection-table.module.css';

export interface CollectionTableProps<M> {
    schema: CollectionSchema<M>;
    items: M[];
    selection: M[];
    selectionHandlers: CollectionSelectionHandlers<React.Key>;
    sort?: CollectionSortField<M>[];
    onSort?: (field: string) => void;
    sortableFields?: (keyof M & string)[];
    sortable?: boolean;
    onReorder?: (fromIndex: number, toIndex: number) => void;
    onReorderBatch?: (reorders: { fromIndex: number; toIndex: number }[]) => void;
    setItemElementRef?: ItemElementRefCallback<M>;
    actions: CollectionSchemaAction<M>[];
}

export default function CollectionTable<M>(props: CollectionTableProps<M>) {
    const {ref: tableRef, width: tableWidth} = useElementSize();
    const {ref: tableHeaderRef, height: tableHeaderHeight} = useElementSize();
    const [activeId, setActiveId] = useState<string | number | null>(null);
    const [isDragging, setIsDragging] = useState(false);

    const sensors = useSensors(
        useSensor(PointerSensor, {
            activationConstraint: {
                distance: DRAG_ACTIVATION_DISTANCE,
            },
        }),
        useSensor(KeyboardSensor),
    );

    const columns = useMemo(() => {
        const columns = props.schema.columns.filter(col => !col.hidden);

        const fixedWidth = columns
            .map(c => getColumnWidthPixels(c.width))
            .filter(width => width != null)
            .reduce((sum, width) => sum + width, 0);

        const totalWidth = Math.max(tableWidth, fixedWidth);
        const freeWidth = totalWidth - fixedWidth - 100;

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
        overscan: VIRTUALIZER_OVERSCAN,
    });

    const virtualRows = virtualizer.getVirtualItems();

    const itemIds = useMemo(() => props.items.map(item => props.schema.key(item)) as string[], [props.items, props.schema.key]);

    const selectedIds = useMemo(() =>
            new Set(props.selection.map(item => props.schema.key(item))),
        [props.selection, props.schema.key]
    );

    const draggedItem = activeId != null ? props.items.find(item => props.schema.key(item) === activeId) : null;
    const isDraggingMultiple = selectedIds.has(activeId as React.Key) && selectedIds.size > 1;

    const handleDragStart = (event: DragStartEvent) => {
        setActiveId(event.active.id);
        setIsDragging(true);
    };

    const handleDragEnd = (event: DragEndEvent) => {
        const {active, over} = event;

        if (over && active.id !== over.id) {
            const fromIndex = props.items.findIndex(item => props.schema.key(item) === active.id);
            let toIndex = props.items.findIndex(item => props.schema.key(item) === over.id);

            if (fromIndex !== -1 && toIndex !== -1) {
                const selectedKeys = Array.from(selectedIds);

                if (selectedKeys.length > 1) {
                    const selectedIndices = selectedKeys
                        .map(key => props.items.findIndex(item => props.schema.key(item) === key))
                        .filter(i => i !== -1)
                        .sort((a, b) => a - b);

                    const selectedCount = selectedIndices.length;

                    if (fromIndex < toIndex) {
                        toIndex = toIndex - selectedCount + 1;
                    }

                    const reorders: { fromIndex: number; toIndex: number }[] = [];
                    for (let i = 0; i < selectedCount; i++) {
                        const moveFromIndex = selectedIndices[i];
                        const moveToIndex = toIndex + i;
                        if (moveFromIndex !== moveToIndex) {
                            reorders.push({fromIndex: moveFromIndex, toIndex: moveToIndex});
                        }
                    }

                    if (reorders.length > 0) {
                        props.onReorderBatch?.(reorders);
                    }
                } else {
                    props.onReorder?.(fromIndex, toIndex);
                }
            }
        }

        setActiveId(null);
        setTimeout(() => setIsDragging(false), 0);
    };

    const rows = virtualRows.map((virtualRow) => {
        const row = props.items[virtualRow.index];
        const itemId = props.schema.key(row);
        const isSelected = selectedIds.has(itemId);
        const isDragOverlay = activeId === itemId;
        const isCollapsed = isDragging && isSelected && !isDragOverlay;

        return <CollectionTableRow
            key={itemId}
            virtualRow={virtualRow}
            virtualizer={virtualizer}
            schema={props.schema}
            row={row}
            columns={columns}
            selection={props.selection}
            selectionHandlers={props.selectionHandlers}
            sortable={props.sortable}
            isSelected={isSelected}
            isDragOverlay={isDragOverlay}
            isCollapsed={isCollapsed}
            isDraggingActive={isDragging}
            setItemElementRef={props.setItemElementRef}
            actions={props.actions}
        />;
    });

    const tableContent = (
        <Box style={{height: `${virtualizer.getTotalSize() + tableHeaderHeight}px`}}>
            <Table highlightOnHover ref={tableRef} style={{
                borderCollapse: 'separate',
            }}>
                <Table.Thead ref={tableHeaderRef}>
                    <Table.Tr>
                        {columns.map(col => {
                            const isSortable = col.sortable && props.onSort && props.sortableFields?.includes(col.name as keyof M & string);
                            const sortIndex = props.sort?.findIndex(s => s.field === col.name);
                            const isSorted = sortIndex !== undefined && sortIndex >= 0;

                            return (
                                <Table.Th
                                    style={{
                                        width: col.width,
                                        textAlign: col.align ?? 'left',
                                        cursor: isSortable ? 'pointer' : 'default'
                                    }}
                                    key={col.name}
                                    onClick={isSortable ? () => props.onSort?.(col.name) : undefined}
                                >
                                    <Group gap={4} wrap="nowrap">
                                        <span>{col.displayName}</span>
                                        {isSortable && (
                                            isSorted ? (
                                                <>
                                                    <Text size="xs" c="blue" fw="bold">{sortIndex! + 1}</Text>
                                                    {props.sort![sortIndex!].direction === 'asc' ?
                                                        <IconArrowUp size={14}/> : <IconArrowDown size={14}/>}
                                                </>
                                            ) : (
                                                <IconSelector size={14} style={{opacity: 0.5}}/>
                                            )
                                        )}
                                    </Group>
                                </Table.Th>
                            );
                        })}
                        <Table.Th key="__actions" style={{width: "60px"}}>{/* Actions Menu */}</Table.Th>
                    </Table.Tr>
                </Table.Thead>
                {props.sortable ? (
                    <SortableContext
                        items={itemIds}
                        strategy={verticalListSortingStrategy}
                    >
                        <Table.Tbody style={{
                            transform: `translateY(${virtualRows[0]?.start ?? 0}px)`
                        }}>
                            {rows}
                        </Table.Tbody>
                    </SortableContext>
                ) : (
                    <Table.Tbody style={{
                        transform: `translateY(${virtualRows[0]?.start ?? 0}px)`
                    }}>
                        {rows}
                    </Table.Tbody>
                )}
            </Table>
        </Box>
    );

    if (props.sortable) {
        return (
            <DndContext
                sensors={sensors}
                collisionDetection={closestCenter}
                onDragStart={handleDragStart}
                onDragEnd={handleDragEnd}
            >
                <Box ref={parentRef} flex={1} style={{overflowY: "auto", maxHeight: "813px"}}>
                    {tableContent}
                </Box>
                <DragOverlay>
                    {draggedItem && !isDraggingMultiple && (
                        <Box style={{
                            display: 'table-row',
                            opacity: 0.8,
                            backgroundColor: 'var(--mantine-color-gray-0)',
                            borderRadius: 4,
                        }}>
                            {columns.map(col => (
                                <Box key={col.name} style={{
                                    display: 'table-cell',
                                    padding: '8px 12px',
                                    textAlign: col.align ?? 'left',
                                    borderBottom: '1px solid var(--table-border-color)',
                                }}>
                                    {col.render(draggedItem)}
                                </Box>
                            ))}
                            <Box style={{display: 'table-cell', width: '60px'}}/>
                        </Box>
                    )}
                    {isDraggingMultiple && (
                        <Box bg="blue.1" p="xs" style={{borderRadius: 4}}>
                            Dragging {selectedIds.size} items
                        </Box>
                    )}
                </DragOverlay>
            </DndContext>
        );
    }

    return <Box ref={parentRef} flex={1} style={{overflowY: "auto", maxHeight: "813px"}}>
        {tableContent}
    </Box>;
}

interface CollectionTableRowProps<M> {
    virtualRow: VirtualItem;
    virtualizer: Virtualizer<HTMLDivElement, Element>;
    schema: CollectionSchema<M>;
    row: M;
    columns: CollectionSchemaColumn<M>[];
    selection: M[];
    selectionHandlers: CollectionSelectionHandlers<React.Key>;
    sortable?: boolean;
    isSelected?: boolean;
    isDragOverlay?: boolean;
    isCollapsed?: boolean;
    isDraggingActive?: boolean;
    setItemElementRef?: ItemElementRefCallback<M>;
    actions: CollectionSchemaAction<M>[];
}

function CollectionTableRow<M>(props: CollectionTableRowProps<M>) {
    const {
        virtualRow,
        virtualizer,
        schema,
        row,
        columns,
        selection,
        selectionHandlers,
        sortable,
        isSelected,
        isDragOverlay,
        isCollapsed,
        isDraggingActive,
        setItemElementRef,
        actions,
    } = props;

    const [isDropdownOpen, setIsDropdownOpen] = useState(false);
    const {showContextMenu} = useContextMenu();

    const {
        attributes,
        listeners,
        setNodeRef,
        transform,
        transition,
        isDragging,
    } = useSortable({
        id: schema.key(row) as string,
        disabled: !sortable,
    });

    const style: React.CSSProperties = {
        transform: CSS.Transform.toString(transform),
        transition,
        opacity: isDragging ? 0.5 : 1,
        height: isCollapsed ? 0 : undefined,
        overflow: isCollapsed ? 'hidden' : undefined,
        marginTop: isCollapsed ? 0 : undefined,
        marginBottom: isCollapsed ? 0 : undefined,
        paddingTop: isCollapsed ? 0 : undefined,
        paddingBottom: isCollapsed ? 0 : undefined,
    };

    const rowActions = useMemo(() => {
        return schema.actions?.([row]) ?? [];
    }, [schema, row]);

    const rowRef = useCallback((node: HTMLTableRowElement | null) => {
        setNodeRef(node);
        virtualizer.measureElement(node);
        if (isSelected && node) {
            setItemElementRef?.(row, node);
        }
    }, [setNodeRef, virtualizer, isSelected, setItemElementRef, row]);

    const handleMouseDown = (event: React.MouseEvent) => {
        if (event.shiftKey || event.ctrlKey || event.metaKey) {
            event.preventDefault();
        }
    };

    const handleMouseUp = (event: React.MouseEvent) => {
        if (isInteractiveElement(event.target)) {
            return;
        }
        if (!isDraggingActive) {
            selectionHandlers.toggle(schema.key(row), event);
        }
    };

    const handleContextMenu = (event: React.MouseEvent) => {
        if (isInteractiveElement(event.target)) {
            return;
        }
        const contextActions = isSelected ? actions : rowActions;
        const contextSelection = isSelected ? selection : [row];

        showContextMenu(
            contextActions
                .filter((a): a is CollectionSchemaAction<M> & { onClick: (elems: M[]) => void } =>
                    !('divider' in a) && !('group' in a)
                )
                .map(action => ({
                    key: action.name,
                    icon: action.renderIcon(),
                    title: action.renderLabel(),
                    onClick: () => action.onClick(contextSelection),
                }))
        )(event);
    };

    return (
        <Table.Tr
            ref={rowRef}
            style={sortable ? style : undefined}
            data-index={virtualRow.index}
            data-sortable-item={sortable || undefined}
            onMouseDown={handleMouseDown}
            onMouseUp={handleMouseUp}
            onContextMenu={handleContextMenu}
            {...(sortable ? attributes : {})}
            {...(sortable && !isDragOverlay ? listeners : {})}
            className={cls(
                styles.row,
                isSelected && styles.selected,
                isDragOverlay && styles.selected,
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
            throw new Error(`Invalid state: no free fractions for column widths, but this column does not have a fixed size!`);
        }

        return freeWidth / freeFractions * fractions;
    }

    return null;
}
