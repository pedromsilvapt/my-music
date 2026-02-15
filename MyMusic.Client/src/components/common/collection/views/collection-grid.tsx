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
import {useCallback, useMemo, useState} from "react";
import {cls} from "../../../../utils/react-utils.tsx";
import CollectionActions from "../collection-actions.tsx";
import {type CollectionSchema} from "../collection-schema.tsx";
import type {CollectionSelectionHandlers} from "../collection.tsx";
import styles from './collection-grid.module.css';

export interface CollectionGridProps<M> {
    schema: CollectionSchema<M>;
    items: M[];
    selection: M[];
    selectionHandlers: CollectionSelectionHandlers<React.Key>;
    sortable?: boolean;
    onReorder?: (fromIndex: number, toIndex: number) => void;
    onReorderBatch?: (reorders: { fromIndex: number; toIndex: number }[]) => void;
}

export default function CollectionGrid<M>(props: CollectionGridProps<M>) {
    const {ref: parentRef, width: tableWidth} = useElementSize<HTMLDivElement>();

    const elemSize = 226;
    const gap = 12;

    const lanes = useMemo(() => {
        return Math.max(1, Math.floor((tableWidth + gap) / (elemSize + gap)));
    }, [elemSize, gap, tableWidth]);

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
                                   elemSize={elemSize}
                                   gap={gap}/>
}

interface CollectionGridPropsInternal<M> extends CollectionGridProps<M> {
    lanes: number;
    parentRef: React.RefObject<HTMLDivElement | null>;
    tableWidth: number;
    elemSize: number;
    gap: number;
}

function CollectionGridInternal<M>(props: CollectionGridPropsInternal<M>) {
    const {lanes, parentRef, elemSize, gap} = props;
    const [activeId, setActiveId] = useState<string | number | null>(null);
    const [isDragging, setIsDragging] = useState(false);

    const sensors = useSensors(
        useSensor(PointerSensor, {
            activationConstraint: {
                distance: 8,
            },
        }),
        useSensor(KeyboardSensor),
    );

    const virtualizer = useVirtualizer({
        count: props.items.length,
        getScrollElement: () => parentRef.current,
        estimateSize: () => 280, // TODO props.schema.estimateListRowHeight,
        overscan: 0,
        lanes: lanes,
        horizontal: false,
    });

    const virtualItems = virtualizer.getVirtualItems();

    const itemIds = useMemo(() => props.items.map(item => props.schema.key(item)) as string[], [props.items, props.schema.key]);

    const selectedIds = useMemo(() =>
            new Set(props.selection.map(item => props.schema.key(item))),
        [props.selection, props.schema.key]
    );

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

    const items = virtualItems.map((virtualItem) => {
        const item = props.items[virtualItem.index];
        const itemId = props.schema.key(item);
        const isSelected = selectedIds.has(itemId);
        const isDragOverlay = activeId === itemId;
        const isCollapsed = isDragging && isSelected && !isDragOverlay;

        return <CollectionGridItem
            key={itemId}
            virtualItem={virtualItem}
            virtualizer={virtualizer}
            item={item}
            width={elemSize}
            schema={props.schema}
            selection={props.selection}
            selectionHandlers={props.selectionHandlers}
            sortable={props.sortable}
            isSelected={isSelected}
            isDragOverlay={isDragOverlay}
            isCollapsed={isCollapsed}
            isDraggingActive={isDragging}
        />;
    });

    const gridContent = (
        <Box style={{height: `${virtualizer.getTotalSize()}px`}}>
            {props.sortable ? (
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

    if (props.sortable) {
        return (
            <DndContext
                sensors={sensors}
                collisionDetection={closestCenter}
                onDragStart={handleDragStart}
                onDragEnd={handleDragEnd}
            >
                <Box ref={parentRef} style={{width: "100%", maxHeight: "4000px", overflowY: "auto"}}>
                    {gridContent}
                </Box>
                <DragOverlay>
                    {activeId && (
                        <Box className={styles.item} style={{opacity: 0.8}} w={elemSize} h={elemSize + 54}>
                            <Stack gap="sm">
                                {props.items.find(item => props.schema.key(item) === activeId) &&
                                    props.schema.renderListArtwork(
                                        props.items.find(item => props.schema.key(item) === activeId)!,
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
        <Box ref={parentRef} style={{width: "100%", maxHeight: "4000px", overflowY: "auto"}}>
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
    } = props;

    const [isDropdownOpen, setIsDropdownOpen] = useState(false);

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
    }, [setNodeRef, virtualizer]);

    const handleMouseDown = (event: React.MouseEvent) => {
        if (event.shiftKey || event.ctrlKey || event.metaKey) {
            event.preventDefault();
        }
    };

    const handleMouseUp = (event: React.MouseEvent) => {
        if (!isDraggingActive) {
            selectionHandlers.toggle(schema.key(item), event);
        }
    };

    return <Box
        ref={itemRef}
        data-index={virtualItem.index}
        data-lane={virtualItem.lane}
        style={sortable ? {...style, width: props.width, height: props.width + 54} : {
            width: props.width,
            height: props.width + 54
        }}
        onMouseDown={handleMouseDown}
        onMouseUp={handleMouseUp}
        {...(sortable ? attributes : {})}
        {...(sortable && !isDragOverlay ? listeners : {})}
                className={cls(
                    styles.item,
                    isSelected && styles.selected,
                    isDragOverlay && styles.selected,
                )}>
        <Stack gap="sm">
            {schema.renderListArtwork(item, props.width - 20)}
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
                    <CollectionActions selection={selection} actions={itemActions} opened={isDropdownOpen}
                                       setOpened={setIsDropdownOpen}/>
                </Box>
            </Group>
        </Stack>
    </Box>
}
