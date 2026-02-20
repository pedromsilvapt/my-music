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
import {useContextMenu} from "mantine-contextmenu";
import {useCallback, useMemo, useRef, useState} from "react";
import {DRAG_ACTIVATION_DISTANCE, LIST_ARTWORK_SIZE, LIST_GAP, VIRTUALIZER_OVERSCAN} from "../../../../consts.ts";
import {isInteractiveElement} from "../../../../utils/event-utils.ts";
import {cls} from "../../../../utils/react-utils.tsx";
import CollectionActions from "../collection-actions.tsx";
import {type CollectionSchema, type CollectionSchemaAction} from "../collection-schema.tsx";
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
}

export default function CollectionList<M>(props: CollectionListProps<M>) {
    const parentRef = useRef<HTMLDivElement>(null)
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

    const virtualizer = useVirtualizer({
        count: props.items.length,
        getScrollElement: () => parentRef.current,
        estimateSize: props.schema.estimateListRowHeight,
        gap: LIST_GAP,
        overscan: VIRTUALIZER_OVERSCAN,
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

        return <CollectionListItem
            key={itemId}
            virtualItem={virtualItem}
            virtualizer={virtualizer}
            item={item}
            schema={props.schema}
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

    const listContent = (
        <Box style={{height: `${virtualizer.getTotalSize()}px`}}>
            {props.sortable ? (
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

    if (props.sortable) {
        return (
            <DndContext
                sensors={sensors}
                collisionDetection={closestCenter}
                onDragStart={handleDragStart}
                onDragEnd={handleDragEnd}
            >
                <Box ref={parentRef} flex={1} style={{overflowY: "auto", maxHeight: "4000px"}}>
                    {listContent}
                </Box>
                <DragOverlay>
                    {activeId && (
                        <Box className={styles.item} style={{opacity: 0.8}}>
                            <Group gap="sm">
                                {props.items.find(item => props.schema.key(item) === activeId) &&
                                    props.schema.renderListArtwork(
                                        props.items.find(item => props.schema.key(item) === activeId)!,
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

    return <Box ref={parentRef} flex={1} style={{overflowY: "auto", maxHeight: "4000px"}}>
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
        if (isInteractiveElement(event.target)) {
            return;
        }
        if (!isDraggingActive) {
            selectionHandlers.toggle(schema.key(item), event);
        }
    };

    const handleContextMenu = (event: React.MouseEvent) => {
        if (isInteractiveElement(event.target)) {
            return;
        }
        const contextActions = isSelected ? actions : itemActions;
        const contextSelection = isSelected ? selection : [item];

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

    return <Box
        ref={itemRef}
        data-index={virtualItem.index}
        data-sortable-item={sortable || undefined}
        style={sortable ? style : undefined}
        onMouseDown={handleMouseDown}
        onMouseUp={handleMouseUp}
        onContextMenu={handleContextMenu}
        {...(sortable ? attributes : {})}
        {...(sortable && !isDragOverlay ? listeners : {})}
        className={cls(
            styles.item,
            isSelected && styles.selected,
            isDragOverlay && styles.selected,
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
}
