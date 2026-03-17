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
import {Box, Group, SimpleGrid, Stack, Text} from "@mantine/core";
import {useElementSize} from "@mantine/hooks";
import {useVirtualizer, type VirtualItem, Virtualizer} from "@tanstack/react-virtual";
import {useCallback, useEffect, useMemo, useRef, useState} from "react";
import {DRAG_ACTIVATION_DISTANCE, GRID_ELEM_SIZE, GRID_GAP, GRID_ROW_HEIGHT} from "../../../../consts.ts";
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
import styles from './collection-grid.module.css';

export interface CollectionGridProps<M> {
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

export default function CollectionGrid<M>(props: CollectionGridProps<M>) {
    const {ref: parentRef, width: tableWidth} = useElementSize<HTMLDivElement>();

    const lanes = useMemo(() => {
        return Math.max(1, Math.floor((tableWidth + GRID_GAP) / (GRID_ELEM_SIZE + GRID_GAP)));
    }, [tableWidth]);

    return <CollectionGridInternal {...props}
        // We must separate this into two components
        // to force React to render the component from scratch when the user resizes the window
        // Which would change the lanes count. This is to overcome a bug with tanstack virtualizer, which
        // only looks at the initial lanes value, and ignores any changes to it (keep the first item, and virtualizer.scrollToIndex)
        // TODO Because we lose the virtualizer's state, we must ensure we scroll to the same element that was visible previously
                                   key={"internal-grid-" + lanes}
                                   items={props.items}
                                   lanes={lanes}
                                   parentRef={parentRef}
                                   tableWidth={tableWidth}
                                   elemSize={GRID_ELEM_SIZE}
                                   gap={GRID_GAP}/>
}

interface CollectionGridPropsInternal<M> extends CollectionGridProps<M> {
    lanes: number;
    parentRef: React.RefObject<HTMLDivElement | null>;
    tableWidth: number;
    elemSize: number;
    gap: number;
}

function CollectionGridInternal<M>(props: CollectionGridPropsInternal<M>) {
    const {onContextMenuTrigger, items: propItems, schema: propSchema, selection: propSelection, selectionHandlers: propSelectionHandlers, onScrollPositionChange, initialScrollPosition, scrollToIndex, highlightRequestId, sortable, setItemElementRef, actions, height, onReorderBatch, onReorder} = props;
    const {lanes, parentRef, elemSize, gap} = props;
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
        estimateSize: () => GRID_ROW_HEIGHT,
        overscan: 0,
        lanes: lanes,
        horizontal: false,
    });

    const virtualItems = virtualizer.getVirtualItems();

    const hasRestoredScrollRef = useRef(false);

    useEffect(() => {
        if (initialScrollPosition != null && !hasRestoredScrollRef.current &&         propItems.length > 0) {
            hasRestoredScrollRef.current = true;
            requestAnimationFrame(() => {
                virtualizer.scrollToIndex(initialScrollPosition!.index, {align: 'start'});
                const scrollElement = parentRef.current;
                if (scrollElement) {
                    scrollElement.scrollTop += initialScrollPosition!.offset;
                }
            });
        }
    }, [initialScrollPosition,         propItems.length, virtualizer, parentRef]);

    useEffect(() => {
        if (scrollToIndex != null &&         propItems.length > 0) {
            const virtualItems = virtualizer.getVirtualItems();
            const isVisible = virtualItems.some(item => item.index === scrollToIndex);
            
            if (!isVisible) {
                requestAnimationFrame(() => {
                    virtualizer.scrollToIndex(scrollToIndex!, {align: 'center'});
                });
            }
        }
    }, [scrollToIndex,         propItems.length, virtualizer]);

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
    }, [onScrollPositionChange, virtualizer, parentRef]);

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

        return <CollectionGridItem
            key={itemId}
            virtualItem={virtualItem}
            virtualizer={virtualizer}
            item={item}
            width={elemSize}
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

    const gridContent = (
        <Box style={{height: `${Math.max(height, virtualizer.getTotalSize())}px`}}>
            {sortable ? (
                <SortableContext
                    items={itemIds}
                    strategy={verticalListSortingStrategy}
                >
                    <SimpleGrid cols={lanes} spacing={gap} style={{
                        transform: `translateY(${virtualItems[0]?.start ?? 0}px)`
                    }}>
                        {items}
                    </SimpleGrid>
                </SortableContext>
            ) : (
                <SimpleGrid cols={lanes} spacing={gap} style={{
                    transform: `translateY(${virtualItems[0]?.start ?? 0}px)`
                }}>
                    {items}
                </SimpleGrid>
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
                <Box ref={parentRef} style={{width: "100%", height: height, overflowY: "auto"}}>
                    {gridContent}
                </Box>
                <DragOverlay>
                    {activeId && (
                        <Box className={styles.item} style={{opacity: 0.8}} w={elemSize} h={elemSize + 54}>
                            <Stack gap="sm">
                                {propItems.find(item => propSchema.key(item) === activeId) &&
                                    propSchema.renderListArtwork(
                                        propItems.find(item => propSchema.key(item) === activeId)!,
                                        elemSize - 20
                                    )
                                }
                                <Group>
                                    <Box flex={1}>
                                        <Text size="md" lineClamp={1}>
                                            {selectedIds.size > 1 ? `Dragging ${selectedIds.size} items` : 'Dragging'}
                                        </Text>
                                    </Box>
                                </Group>
                            </Stack>
                        </Box>
                    )}
                </DragOverlay>
            </DndContext>
        );
    }

    return (
        <Box ref={parentRef} style={{width: "100%", height: height, overflowY: "auto"}}>
            {gridContent}
        </Box>
    );
}

export interface CollectionGridItemProps<M> {
    virtualItem: VirtualItem;
    virtualizer: Virtualizer<HTMLDivElement, Element>;
    schema: CollectionSchema<M>;
    item: M;
    selection: M[];
    selectionHandlers: CollectionSelectionHandlers<React.Key>;
    width: number;
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

export function CollectionGridItem<M>(props: CollectionGridItemProps<M>) {
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
        width,
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
        if (event.button === 2) {
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
        data-lane={virtualItem.lane}
        data-sortable-item={sortable || undefined}
        style={sortable ? {...style, width: width, height: width + 54} : {
            width: width,
            height: width + 54
        }}
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
        <Stack gap="sm">
            {schema.renderListArtwork(item, width - 20)}
            <Group>
                <Box flex={1}>
                    <Text size="md" lineClamp={1}>{schema.renderListTitle(item, 1)}</Text>
                    <Text size="sm" opacity={0.5} lineClamp={1}>
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
        </Stack>
    </Box>
        </>
    );
}
