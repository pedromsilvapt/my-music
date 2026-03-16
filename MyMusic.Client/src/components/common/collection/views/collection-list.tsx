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
import {Box, Group, Stack, Text} from "@mantine/core";
import {useVirtualizer, type VirtualItem, Virtualizer} from "@tanstack/react-virtual";
import {useCallback, useEffect, useMemo, useRef, useState} from "react";
import {DRAG_ACTIVATION_DISTANCE, LIST_ARTWORK_SIZE, LIST_GAP, VIRTUALIZER_OVERSCAN} from "../../../../consts.ts";
import type {ScrollPosition} from "../../../../contexts/collection-context.tsx";
import {useLongPress} from "../../../../hooks/use-long-press.ts";
import {isArtworkPreviewElement, isInteractiveElement} from "../../../../utils/event-utils.ts";
import {cls} from "../../../../utils/react-utils.tsx";
import CollectionActions from "../collection-actions.tsx";
import {
    type CollectionSchema,
    type CollectionSchemaAction
} from "../collection-schema.tsx";
import type {CollectionSelectionHandlers, ItemElementRefCallback} from "../collection.tsx";
import styles from './collection-list.module.css';

export interface CollectionListProps<M> {
    schema: CollectionSchema<M>;
    items: M[];
    selection: M[];
    selectionHandlers: CollectionSelectionHandlers<React.Key>;
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

export default function CollectionList<M>(props: CollectionListProps<M>) {
    const {onContextMenuTrigger, items: propItems, schema: propSchema, selection: propSelection, selectionHandlers: propSelectionHandlers, onScrollPositionChange, initialScrollPosition, sortable, setItemElementRef, actions, height, onReorderBatch, onReorder, scrollToIndex, highlightRequestId} = props;
    const parentRef = useRef<HTMLDivElement>(null)
    const [activeId, setActiveId] = useState<string | number | null>(null);
    const [isDragging, setIsDragging] = useState(false);

    const handleContextMenuTrigger = useCallback((
        event: React.MouseEvent | React.TouchEvent,
        rowActions: CollectionSchemaAction<M>[],
        selection: M[]
    ) => {
        onContextMenuTrigger?.(event, rowActions, selection);
    }, [onContextMenuTrigger]);

    const sensors = useSensors(
        useSensor(PointerSensor, {
            activationConstraint: {
                distance: DRAG_ACTIVATION_DISTANCE,
            },
        }),
        useSensor(KeyboardSensor),
    );

    const virtualizer = useVirtualizer({
        count: propItems.length,
        getScrollElement: () => parentRef.current,
        estimateSize: propSchema.estimateListRowHeight,
        gap: LIST_GAP,
        overscan: VIRTUALIZER_OVERSCAN,
    });

    const virtualItems = virtualizer.getVirtualItems();

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
            const items = virtualizer.getVirtualItems();
            if (items.length > 0) {
                const firstItem = items[0];
                const offset = scrollElement.scrollTop - firstItem.start;
                onScrollPositionChange?.({index: firstItem.index, offset});
            }
        };

        scrollElement.addEventListener('scroll', handleScroll, {passive: true});
        return () => scrollElement.removeEventListener('scroll', handleScroll);
    }, [onScrollPositionChange, virtualizer]);

    const itemIds = useMemo(() => propItems.map(item => propSchema.key(item)) as string[], [propItems, propSchema]);

    const selectedIds = useMemo(() =>
            new Set(propSelection.map(item => propSchema.key(item))),
        [propSelection, propSchema]
    );

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

    const items = virtualItems.map((virtualItem) => {
        const item = propItems[virtualItem.index];
        const itemId = propSchema.key(item);
        const isSelected = selectedIds.has(itemId);
        const isDragOverlay = activeId === itemId;
        const isCollapsed = isDragging && isSelected && !isDragOverlay;

        return <CollectionListItem
            key={itemId}
            virtualItem={virtualItem}
            virtualizer={virtualizer}
            item={item}
            schema={propSchema}
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

    const listContent = (
        <Box style={{height: `${Math.max(height, virtualizer.getTotalSize())}px`}}>
            {sortable ? (
                <SortableContext
                    items={itemIds}
                    strategy={verticalListSortingStrategy}
                >
                    <Stack gap={LIST_GAP} style={{
                        transform: `translateY(${virtualItems[0]?.start ?? 0}px)`
                    }}>
                        {items}
                    </Stack>
                </SortableContext>
            ) : (
                <Stack gap={LIST_GAP} style={{
                    transform: `translateY(${virtualItems[0]?.start ?? 0}px)`
                }}>
                    {items}
                </Stack>
            )}
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
                    {listContent}
                </Box>
                <DragOverlay>
                    {activeId && (
                        <Box className={styles.item} style={{opacity: 0.8}}>
                            <Group gap="sm">
                                {propItems.find(item => propSchema.key(item) === activeId) &&
                                    propSchema.renderListArtwork(
                                        propItems.find(item => propSchema.key(item) === activeId)!,
                                        LIST_ARTWORK_SIZE
                                    )
                                }
                                <Box flex={1}>
                                    <Text size="md">
                                        {selectedIds.size > 1 ? `Dragging ${selectedIds.size} items` : 'Dragging'}
                                    </Text>
                                </Box>
                            </Group>
                        </Box>
                    )}
                </DragOverlay>
            </DndContext>
        );
    }

    return <Box ref={parentRef} style={{height: height, overflowY: "auto"}}>
        {listContent}
    </Box>;
}

export interface CollectionListItemProps<M> {
    virtualItem: VirtualItem;
    virtualizer: Virtualizer<HTMLDivElement, Element>;
    schema: CollectionSchema<M>;
    item: M;
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

export function CollectionListItem<M>(props: CollectionListItemProps<M>) {
    const {
        schema,
        item,
        selection,
        selectionHandlers,
        virtualItem,
        virtualizer,
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
            if (virtualItem.index === scrollToIndex) {
                prevHighlightIdRef.current = highlightRequestId;
                setIsHighlighted(true);
                setTimeout(() => setIsHighlighted(false), 1500);
            }
        }
    }, [highlightRequestId, scrollToIndex, virtualItem.index]);

    const {
        attributes,
        listeners,
        setNodeRef,
        transform,
        transition,
        isDragging,
    } = useSortable({
        id: schema.key(item) as string,
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

    const itemActions = useMemo(() => {
        return schema.actions?.([item]) ?? [];
    }, [schema, item]);

    const itemRef = useCallback((node: HTMLDivElement | null) => {
        setNodeRef(node);
        virtualizer.measureElement(node);
        if (isSelected && node) {
            setItemElementRef?.(item, node);
        }
    }, [setNodeRef, virtualizer, isSelected, setItemElementRef, item]);

    const handleMouseDown = (event: React.MouseEvent) => {
        if (event.shiftKey || event.ctrlKey || event.metaKey) {
            event.preventDefault();
        }
    };

    const handleMouseUp = (event: React.MouseEvent) => {
        console.log('[CL-MouseUp] triggered', { 
            target: event.target,
            isInteractive: isInteractiveElement(event.target),
            isDraggingActive,
            button: event.button
        });
        
        if (event.button === 2) {
            console.log('[CL-MouseUp] skipping - right click');
            return;
        }
        
        if (isInteractiveElement(event.target)) {
            return;
        }
        if (!isDraggingActive) {
            selectionHandlers.toggle(schema.key(item), event);
        }
    };

    const handleContextMenu = (event: React.MouseEvent) => {
        if (isInteractiveElement(event.target) || isArtworkPreviewElement(event.target)) {
            return;
        }

        const itemKey = schema.key(item);

        selectionHandlers.toggle(itemKey, event);

        const isNowSelected = !isSelected;
        const contextActions = isNowSelected ? itemActions : actions;
        const contextSelection = isNowSelected ? [item] : selection;

        onContextMenuTrigger(event, contextActions, contextSelection);
    };

    const longPressHandlers = useLongPress(handleContextMenu as (e: React.SyntheticEvent) => void);

    return (
        <>
            <Box
                ref={itemRef}
                data-index={virtualItem.index}
                data-sortable-item={sortable || undefined}
                style={sortable ? style : undefined}
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
                    styles.item,
                    isSelected && styles.selected,
                    isDragOverlay && styles.selected,
                    isHighlighted && styles.highlighted,
                )}>
                <Group gap="sm">
                    {schema.renderListArtwork(item, LIST_ARTWORK_SIZE)}
                    <Box flex={1}>
                        <Text size="md">{schema.renderListTitle(item, 1)}</Text>
                        <Text size="sm">
                            {schema.renderListSubTitle(item, 1)}
                        </Text>
                    </Box>
                    <Box className={cls(
                        styles.itemActions,
                        isDropdownOpen && styles.opened,
                        selection.length > 0 && styles.hidden
                    )}>
                        <CollectionActions selection={[item]} actions={itemActions} opened={isDropdownOpen}
                                           setOpened={setIsDropdownOpen}/>
                    </Box>
                </Group>
            </Box>
        </>
    );
}
