import {Box, Group, Stack, Text} from "@mantine/core";
import {useVirtualizer, type VirtualItem, Virtualizer} from "@tanstack/react-virtual";
import {useMemo, useRef, useState} from "react";
import {cls} from "../../../../utils/react-utils.tsx";
import CollectionActions from "../collection-actions.tsx";
import {type CollectionSchema} from "../collection-schema.tsx";
import type {CollectionSelectionHandlers} from "../collection.tsx";
import styles from './collection-list.module.css';

export interface CollectionListProps<M> {
    schema: CollectionSchema<M>;
    items: M[];
    selection: M[];
    selectionHandlers: CollectionSelectionHandlers<React.Key>;
}

export default function CollectionList<M>(props: CollectionListProps<M>) {
    const parentRef = useRef<HTMLDivElement>(null)

    const gap = 10;

    const virtualizer = useVirtualizer({
        count: props.items.length,
        getScrollElement: () => parentRef.current,
        estimateSize: props.schema.estimateListRowHeight,
        gap: gap,
        overscan: 5,
    });

    const virtualItems = virtualizer.getVirtualItems();

    const items = virtualItems.map((virtualItem) => {
        const item = props.items[virtualItem.index];

        return <CollectionListItem
            key={props.schema.key(item)}
            virtualItem={virtualItem}
            virtualizer={virtualizer}
            item={item}
            schema={props.schema}
            selection={props.selection}
            selectionHandlers={props.selectionHandlers}
        />;
    });

    return <Box ref={parentRef} flex={1} style={{overflowY: "auto", maxHeight: "4000px"}}>
        <Box style={{height: `${virtualizer.getTotalSize()}px`}}>
            <Stack gap={gap} style={{
                transform: `translateY(${virtualItems[0]?.start ?? 0}px)`
            }}>
                {items}
            </Stack>
        </Box>
    </Box>;
}

export interface CollectionListItemProps<M> {
    virtualItem: VirtualItem;
    virtualizer: Virtualizer<HTMLDivElement, Element>;
    schema: CollectionSchema<M>;
    item: M;
    selection: M[];
    selectionHandlers: CollectionSelectionHandlers<React.Key>;
}

export function CollectionListItem<M>(props: CollectionListItemProps<M>) {
    const {
        schema,
        item,
        selection,
        selectionHandlers,
        virtualItem,
        virtualizer,
    } = props;

    const [isDropdownOpen, setIsDropdownOpen] = useState(false);

    const itemActions = useMemo(() => {
        return schema.actions?.([item]) ?? [];
    }, [schema, item]);

    return <Box key={schema.key(item)}
                data-index={virtualItem.index}
                ref={virtualizer.measureElement}
                onMouseDown={(event) => {
                    if (event.shiftKey || event.ctrlKey || event.metaKey) {
                        event.preventDefault();
                    }
                    selectionHandlers.toggle(schema.key(item), event);
                }}
                className={cls(
                    styles.item,
                    selection.includes(item) && styles.selected,
                )}>
        <Group gap="sm">
            {schema.renderListArtwork(item, 64)}
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
                <CollectionActions selection={selection} actions={itemActions} opened={isDropdownOpen}
                                   setOpened={setIsDropdownOpen}/>
            </Box>
        </Group>
    </Box>
}
