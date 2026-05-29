/* eslint-disable react-refresh/only-export-components */
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
import React, {useCallback, useEffect, useMemo, useRef, useState} from "react";
import {DRAG_ACTIVATION_DISTANCE, VIRTUALIZER_OVERSCAN} from "../../../../consts.ts";
import type {ScrollPosition} from "../../../../contexts/collection-context.tsx";
import {useLongPress} from "../../../../hooks/use-long-press.ts";
import {isArtworkPreviewElement, isInteractiveElement} from "../../../../utils/event-utils.ts";
import {cls} from "../../../../utils/react-utils.tsx";
import {RowActionsContainer} from "../collection-actions.tsx";
import {
    type CollectionSchema,
    type CollectionSchemaAction,
    type CollectionSchemaColumn,
    type CollectionSortField,
    getColumnWidthFractions,
    getColumnWidthPixels
} from "../collection-schema.tsx";
import type {SelectionStore} from "../selection-store.ts";
import styles from './collection-table.module.css';

export interface CollectionTableProps<M> {
    schema: CollectionSchema<M>;
    items: M[];
    selectionStore: SelectionStore;
    onToggle: (key: React.Key, event: React.MouseEvent) => void;
    sort?: CollectionSortField<M>[];
    onSort?: (field: string) => void;
    sortableFields?: (keyof M & string)[];
    sortable?: boolean;
    onReorder?: (fromIndex: number, toIndex: number) => void;
    onReorderBatch?: (reorders: { fromIndex: number; toIndex: number }[]) => void;
    initialScrollPosition?: ScrollPosition;
    onScrollPositionChange?: (position: ScrollPosition) => void;
    scrollToIndex?: number;
    scrollRequestId?: number;
    height: number | undefined;
    autoHeight?: boolean;
    onContextMenuTrigger?: (event: React.MouseEvent | React.TouchEvent, rowActions: CollectionSchemaAction<M>[], rowSelection: M[]) => void;
}

export default function CollectionTable<M>(props: CollectionTableProps<M>) {
    const {onContextMenuTrigger, items: propItems, schema: propSchema, selectionStore, onToggle, onScrollPositionChange, initialScrollPosition, sortable, sortableFields, height, onReorderBatch, onReorder, scrollToIndex, scrollRequestId, onSort, sort: propSort, autoHeight} = props;
    const {ref: tableRef, width: tableWidth} = useElementSize();
    const {ref: tableHeaderRef, height: tableHeaderHeight} = useElementSize();
    const [activeId, setActiveId] = useState<string | number | null>(null);
    const [isDragging, setIsDragging] = useState(false);

    const handleContextMenuTrigger = useCallback((
        event: React.MouseEvent | React.TouchEvent,
        rowActions: CollectionSchemaAction<M>[],
        rowSelection: M[]
    ) => {
        onContextMenuTrigger?.(event, rowActions, rowSelection);
    }, [onContextMenuTrigger]);

    const toggleRef = useRef(onToggle);
    toggleRef.current = onToggle;

    const handleRowToggle = useCallback((key: React.Key, event?: React.MouseEvent | React.TouchEvent) => {
        toggleRef.current(key, event as React.MouseEvent);
    }, []);

    const sensors = useSensors(
        useSensor(PointerSensor, {
            activationConstraint: {
                distance: DRAG_ACTIVATION_DISTANCE,
            },
        }),
        useSensor(KeyboardSensor),
    );

    const columns = useMemo(() => {
        const columns = propSchema.columns.filter(col => !col.hidden);

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
    }, [propSchema.columns, tableWidth]);

    const parentRef = useRef<HTMLDivElement>(null)

    const virtualizer = useVirtualizer({
        count: propItems.length,
        getScrollElement: () => parentRef.current,
        estimateSize: propSchema.estimateTableRowHeight,
        overscan: VIRTUALIZER_OVERSCAN,
    });

    const virtualRows = virtualizer.getVirtualItems();

    const hasRestoredScrollRef = useRef(false);

    useEffect(() => {
        if (initialScrollPosition != null && !hasRestoredScrollRef.current && propItems.length > 0) {
            hasRestoredScrollRef.current = true;
            requestAnimationFrame(() => {
                virtualizer.scrollToIndex(initialScrollPosition!.index, {align: 'start'});
                const scrollElement = parentRef.current;
                if (scrollElement) {
                    scrollElement.scrollTop += initialScrollPosition!.offset;
                }
            });
        }
    }, [initialScrollPosition, propItems.length, virtualizer]);

    useEffect(() => {
        if (scrollRequestId != null && scrollToIndex != null && scrollToIndex >= 0) {
            const virtualItems = virtualizer.getVirtualItems();
            const isVisible = virtualItems.some(item => item.index === scrollToIndex);
            
            if (!isVisible) {
                requestAnimationFrame(() => {
                    virtualizer.scrollToIndex(scrollToIndex!, {align: 'center'});
                });
            }
        }
    }, [scrollRequestId, scrollToIndex, virtualizer]);

    useEffect(() => {
        const scrollElement = parentRef.current;
        if (!scrollElement || !onScrollPositionChange) return;

        const handleScroll = () => {
            const virtualItems = virtualizer.getVirtualItems();
            if (virtualItems.length > 0) {
                const firstItem = virtualItems[0];
                const offset = scrollElement.scrollTop - firstItem.start;
                onScrollPositionChange?.({index: firstItem.index, offset});
            }
        };

        scrollElement.addEventListener('scroll', handleScroll, {passive: true});
        return () => scrollElement.removeEventListener('scroll', handleScroll);
    }, [onScrollPositionChange, virtualizer]);

    const itemIds = useMemo(() => propItems.map(item => propSchema.key(item)) as string[], [propItems, propSchema]);

    const draggedItem = activeId != null ? propItems.find(item => propSchema.key(item) === activeId) : null;
    const isDraggingMultiple = selectionStore.getState().selectedKeys.has(activeId as React.Key) && selectionStore.getState().selectedKeys.size > 1;

    const handleDragStart = (event: DragStartEvent) => {
        setActiveId(event.active.id);
        setIsDragging(true);
    };

    const handleDragEnd = (event: DragEndEvent) => {
        const {active, over} = event;

        if (over && active.id !== over.id) {
            const fromIndex = propItems.findIndex(item => propSchema.key(item) === active.id);
            let toIndex = propItems.findIndex(item => propSchema.key(item) === over.id);

            if (fromIndex !== -1 && toIndex !== -1) {
                const selectedKeys = Array.from(selectionStore.getState().selectedKeys);

                if (selectedKeys.length > 1) {
                    const selectedIndices = selectedKeys
                        .map(key => propItems.findIndex(item => propSchema.key(item) === key))
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
                        onReorderBatch?.(reorders);
                    }
                } else {
                    onReorder?.(fromIndex, toIndex);
                }
            }
        }

        setActiveId(null);
        setTimeout(() => setIsDragging(false), 0);
    };

    const rows = virtualRows.map((virtualRow) => {
        const row = propItems[virtualRow.index];
        const itemId = propSchema.key(row);
        const isDragOverlay = activeId === itemId;

        return <CollectionTableRow
            key={itemId}
            virtualRow={virtualRow}
            virtualizer={virtualizer}
            schema={propSchema}
            row={row}
            columns={columns}
            items={propItems}
            selectionStore={selectionStore}
            itemId={itemId}
            onToggle={handleRowToggle}
            sortable={sortable}
            isDragOverlay={isDragOverlay}
            isDraggingActive={isDragging}
            scrollToIndex={scrollToIndex}
            scrollRequestId={scrollRequestId}
            onContextMenuTrigger={handleContextMenuTrigger}
        />;
    });

    const tableContent = (
        <Box style={autoHeight ? undefined : {height: `${Math.max(height ?? 0, virtualizer.getTotalSize() + tableHeaderHeight)}px`}}>
            <Table highlightOnHover ref={tableRef} style={{
                borderCollapse: 'separate',
            }}>
                <Table.Thead ref={tableHeaderRef}>
                    <Table.Tr>
                        {columns.map(col => {
                            const isSortable = col.sortable && onSort && sortableFields?.includes(col.name as keyof M & string);
                            const sortIndex = propSort?.findIndex(s => s.field === col.name);
                            const isSorted = sortIndex !== undefined && sortIndex >= 0;

                            return (
                                <Table.Th
                                    style={{
                                        width: col.width,
                                        textAlign: col.align ?? 'left',
                                        cursor: isSortable ? 'pointer' : 'default'
                                    }}
                                    key={col.name}
                                    onClick={isSortable ? () => onSort?.(col.name) : undefined}
                                >
                                    <Group gap={4} wrap="nowrap">
                                        <span>{col.displayName}</span>
                                        {isSortable && (
                                            isSorted ? (
                                                <>
                                                    <Text size="xs" c="blue" fw="bold">{sortIndex! + 1}</Text>
                                                    {propSort![sortIndex!].direction === 'asc' ?
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
                {sortable ? (
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

    if (sortable) {
        return (
            <DndContext
                sensors={sensors}
                collisionDetection={closestCenter}
                onDragStart={handleDragStart}
                onDragEnd={handleDragEnd}
            >
                <Box ref={parentRef} style={autoHeight ? undefined : {height: height, overflowY: "auto"}}>
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
                                    {col.render(draggedItem, propItems.findIndex(item => propSchema.key(item) === activeId), propItems)}
                                </Box>
                            ))}
                            <Box style={{display: 'table-cell', width: '60px'}}/>
                        </Box>
                    )}
                    {isDraggingMultiple && (
                        <Box bg="blue.1" p="xs" style={{borderRadius: 4}}>
                            Dragging {selectionStore.getState().selectedKeys.size} items
                        </Box>
                    )}
                </DragOverlay>
            </DndContext>
        );
    }

    return <Box ref={parentRef} style={autoHeight ? undefined : {height: height, overflowY: "auto"}}>
        {tableContent}
    </Box>;
}

interface CollectionTableRowProps<M> {
    virtualRow: VirtualItem;
    virtualizer: Virtualizer<HTMLDivElement, Element>;
    schema: CollectionSchema<M>;
    row: M;
    columns: CollectionSchemaColumn<M>[];
    items: M[];
    selectionStore: SelectionStore;
    itemId: React.Key;
    onToggle: (key: React.Key, event?: React.MouseEvent | React.TouchEvent) => void;
    sortable?: boolean;
    isDragOverlay?: boolean;
    isDraggingActive?: boolean;
    scrollToIndex?: number;
    scrollRequestId?: number;
    onContextMenuTrigger: (event: React.MouseEvent | React.TouchEvent, rowActions: CollectionSchemaAction<M>[], rowSelection: M[]) => void;
}

function areTableRowPropsEqual<M>(
    prevProps: CollectionTableRowProps<M>,
    nextProps: CollectionTableRowProps<M>
): boolean {
    return (
        prevProps.itemId === nextProps.itemId &&
        prevProps.row === nextProps.row &&
        prevProps.virtualRow.index === nextProps.virtualRow.index &&
        prevProps.selectionStore === nextProps.selectionStore &&
        prevProps.schema === nextProps.schema &&
        prevProps.virtualizer === nextProps.virtualizer &&
        prevProps.columns === nextProps.columns &&
        prevProps.items === nextProps.items &&
        prevProps.sortable === nextProps.sortable &&
        prevProps.isDragOverlay === nextProps.isDragOverlay &&
        prevProps.isDraggingActive === nextProps.isDraggingActive &&
        prevProps.scrollToIndex === nextProps.scrollToIndex &&
        prevProps.scrollRequestId === nextProps.scrollRequestId &&
        prevProps.onToggle === nextProps.onToggle &&
        prevProps.onContextMenuTrigger === nextProps.onContextMenuTrigger
    );
}

function CollectionTableRowInner<M>(props: CollectionTableRowProps<M>) {
    const {
        virtualRow,
        virtualizer,
        schema,
        row,
        columns,
        items,
        selectionStore,
        itemId,
        onToggle,
        sortable,
        isDragOverlay,
        isDraggingActive,
        scrollToIndex,
        scrollRequestId,
        onContextMenuTrigger,
    } = props;

    const isSelected = selectionStore(state => state.selectedKeys.has(itemId));
    const isContextMenuHovered = selectionStore(state => state.contextMenuHoverKey) === itemId;

    const [isDropdownOpen, setIsDropdownOpen] = useState(false);
    const prevScrollRequestIdRef = useRef<number | undefined>(undefined);
    const [isHighlighted, setIsHighlighted] = useState(false);

    useEffect(() => {
        if (scrollRequestId !== undefined && scrollRequestId !== prevScrollRequestIdRef.current) {
            if (virtualRow.index === scrollToIndex) {
                prevScrollRequestIdRef.current = scrollRequestId;
                setIsHighlighted(true);
                setTimeout(() => setIsHighlighted(false), 1500);
            }
        }
    }, [scrollRequestId, scrollToIndex, virtualRow.index]);

    const isCollapsed = isDraggingActive && isSelected && !isDragOverlay;

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
            selectionStore.getState().setLastSelectedElement(node);
        }
    }, [setNodeRef, virtualizer, isSelected, selectionStore]);

    const handleMouseDown = (event: React.MouseEvent) => {
        if (event.shiftKey || event.ctrlKey || event.metaKey) {
            event.preventDefault();
        }
    };

    const handleMouseUp = (event: React.MouseEvent) => {
        if (event.button === 2) {
            return;
        }
        
        if (isInteractiveElement(event.target)) {
            return;
        }
        if (!isDraggingActive) {
            onToggle(schema.key(row), event);
        }
    };

    const handleContextMenu = (event: React.MouseEvent) => {
        if (isInteractiveElement(event.target) || isArtworkPreviewElement(event.target)) {
            return;
        }

        const itemKey = schema.key(row);

        onToggle(itemKey, event);

        const isNowSelected = !isSelected;

        if (isNowSelected && event.type === 'contextmenu') {
            selectionStore.getState().setContextMenuHoverKey(itemKey);
        }

        const contextActions = isNowSelected ? rowActions : (schema.actions?.(items.filter(item => selectionStore.getState().selectedKeys.has(schema.key(item)))) ?? []);
        const contextSelection = isNowSelected ? [row] : items.filter(item => selectionStore.getState().selectedKeys.has(schema.key(item)));

        onContextMenuTrigger(event, contextActions, contextSelection);
    };

    const longPressHandlers = useLongPress(handleContextMenu as (e: React.SyntheticEvent) => void);

    return (
        <>
            <Table.Tr
                ref={rowRef}
                style={sortable ? style : undefined}
                data-index={virtualRow.index}
                data-sortable-item={sortable || undefined}
                onMouseDown={handleMouseDown}
                onMouseUp={handleMouseUp}
                onContextMenu={longPressHandlers.onContextMenu}
                onTouchStart={longPressHandlers.onTouchStart}
                onTouchEnd={longPressHandlers.onTouchEnd}
                onTouchMove={longPressHandlers.onTouchMove}
                onTouchCancel={longPressHandlers.onTouchCancel}
                {...(sortable ? attributes : {})}
                {...(sortable && !isDragOverlay ? listeners : {})}
                className={cls(
                    styles.row,
                    (isSelected || isContextMenuHovered) && styles.selected,
                    isDragOverlay && styles.selected,
                    isHighlighted && styles.highlighted,
                )}
            >
                {columns.map(col =>
                    <Table.Td key={col.name}
                              data-testid={`collection-cell-${col.name}-${itemId}`}
                              style={{
                                  borderBottom: 'calc(0.0625rem * var(--mantine-scale)) solid var(--table-border-color)',
                                  textAlign: col.align ?? 'left'
                              }}>
                        {col.render(row, virtualRow.index, items)}
                    </Table.Td>
                )}
                <Table.Td
                    key="__actions"
                    style={{
                        borderBottom: 'calc(0.0625rem * var(--mantine-scale)) solid var(--table-border-color)'
                    }}
                >
                    <RowActionsContainer 
                        item={row} 
                        actions={rowActions} 
                        opened={isDropdownOpen} 
                        setOpened={setIsDropdownOpen}
                        containerClassName={styles.rowActions}
                        openedClassName={styles.opened}
                        hiddenClassName={styles.hidden}
                    />
                </Table.Td>
            </Table.Tr>
        </>
    );
}

const CollectionTableRow = React.memo(CollectionTableRowInner, areTableRowPropsEqual) as (<M>(props: CollectionTableRowProps<M>) => React.ReactNode);

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