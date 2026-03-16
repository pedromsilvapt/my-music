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
import {useCallback, useEffect, useMemo, useRef, useState} from "react";
import {DRAG_ACTIVATION_DISTANCE, VIRTUALIZER_OVERSCAN} from "../../../../consts.ts";
import type {ScrollPosition} from "../../../../contexts/collection-context.tsx";
import {useLongPress} from "../../../../hooks/use-long-press.ts";
import {isArtworkPreviewElement, isInteractiveElement} from "../../../../utils/event-utils.ts";
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
    initialScrollPosition?: ScrollPosition;
    onScrollPositionChange?: (position: ScrollPosition) => void;
    scrollToIndex?: number;
    highlightRequestId?: number;
    height: number;
    onContextMenuTrigger?: (event: React.MouseEvent | React.TouchEvent, rowActions: CollectionSchemaAction<M>[], rowSelection: M[]) => void;
}

export default function CollectionTable<M>(props: CollectionTableProps<M>) {
    const {onContextMenuTrigger, items: propItems, schema: propSchema, selection: propSelection, selectionHandlers: propSelectionHandlers, onScrollPositionChange, initialScrollPosition, sortable, sortableFields, setItemElementRef, actions, height, onReorderBatch, onReorder, scrollToIndex, highlightRequestId, onSort, sort: propSort} = props;
    const {ref: tableRef, width: tableWidth} = useElementSize();
    const {ref: tableHeaderRef, height: tableHeaderHeight} = useElementSize();
    const [activeId, setActiveId] = useState<string | number | null>(null);
    const [isDragging, setIsDragging] = useState(false);

    const handleContextMenuTrigger = useCallback((
        event: React.MouseEvent | React.TouchEvent,
        actions: CollectionSchemaAction<M>[],
        selection: M[]
    ) => {
        onContextMenuTrigger?.(event, actions, selection);
    }, [onContextMenuTrigger]);

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
        if (scrollToIndex != null && propItems.length > 0) {
            const virtualItems = virtualizer.getVirtualItems();
            const isVisible = virtualItems.some(item => item.index === scrollToIndex);
            
            if (!isVisible) {
                requestAnimationFrame(() => {
                    virtualizer.scrollToIndex(scrollToIndex!, {align: 'center'});
                });
            }
        }
    }, [scrollToIndex, propItems.length, virtualizer]);

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

    const selectedIds = useMemo(() =>
            new Set(propSelection.map(item => propSchema.key(item))),
        [propSelection, propSchema]
    );

    const itemIds = useMemo(() => propItems.map(item => propSchema.key(item)) as string[], [propItems, propSchema]);

    const draggedItem = activeId != null ? propItems.find(item => propSchema.key(item) === activeId) : null;
    const isDraggingMultiple = selectedIds.has(activeId as React.Key) && selectedIds.size > 1;

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
                const selectedKeys = Array.from(selectedIds);

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
        const isSelected = selectedIds.has(itemId);
        const isDragOverlay = activeId === itemId;
        const isCollapsed = isDragging && isSelected && !isDragOverlay;

        return <CollectionTableRow
            key={itemId}
            virtualRow={virtualRow}
            virtualizer={virtualizer}
            schema={propSchema}
            row={row}
            columns={columns}
            selection={propSelection}
            selectionHandlers={propSelectionHandlers}
            sortable={sortable}
            isSelected={isSelected}
            isDragOverlay={isDragOverlay}
            isCollapsed={isCollapsed}
            isDraggingActive={isDragging}
            setItemElementRef={setItemElementRef}
            actions={actions}
            scrollToIndex={scrollToIndex}
            highlightRequestId={highlightRequestId}
            onContextMenuTrigger={handleContextMenuTrigger}
        />;
    });

    const tableContent = (
        <Box style={{height: `${Math.max(height, virtualizer.getTotalSize() + tableHeaderHeight)}px`}}>
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
                <Box ref={parentRef} style={{height: height, overflowY: "auto"}}>
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

    return <Box ref={parentRef} style={{height: height, overflowY: "auto"}}>
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
    scrollToIndex?: number;
    highlightRequestId?: number;
    onContextMenuTrigger: (event: React.MouseEvent | React.TouchEvent, rowActions: CollectionSchemaAction<M>[], rowSelection: M[]) => void;
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
        scrollToIndex,
        highlightRequestId,
        onContextMenuTrigger,
    } = props;

    const [isDropdownOpen, setIsDropdownOpen] = useState(false);
    const prevHighlightIdRef = useRef<number | undefined>(undefined);
    const [isHighlighted, setIsHighlighted] = useState(false);

    useEffect(() => {
        if (highlightRequestId !== undefined && highlightRequestId !== prevHighlightIdRef.current) {
            if (virtualRow.index === scrollToIndex) {
                prevHighlightIdRef.current = highlightRequestId;
                setIsHighlighted(true);
                setTimeout(() => setIsHighlighted(false), 1500);
            }
        }
    }, [highlightRequestId, scrollToIndex, virtualRow.index]);

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
        console.log('[CT-MouseUp] triggered', {
            target: event.target,
            isInteractive: isInteractiveElement(event.target),
            isDraggingActive,
            button: event.button
        });
        
        if (event.button === 2) {
            console.log('[CT-MouseUp] skipping - right click');
            return;
        }
        
        if (isInteractiveElement(event.target)) {
            return;
        }
        if (!isDraggingActive) {
            selectionHandlers.toggle(schema.key(row), event);
        }
    };

    const handleContextMenu = (event: React.MouseEvent) => {
        if (isInteractiveElement(event.target) || isArtworkPreviewElement(event.target)) {
            return;
        }

        const itemKey = schema.key(row);

        selectionHandlers.toggle(itemKey, event);

        const isNowSelected = !isSelected;
        const contextActions = isNowSelected ? rowActions : actions;
        const contextSelection = isNowSelected ? [row] : selection;

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
                    isSelected && styles.selected,
                    isDragOverlay && styles.selected,
                    isHighlighted && styles.highlighted,
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
        </>
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
