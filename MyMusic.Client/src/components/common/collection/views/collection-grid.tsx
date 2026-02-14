import {Box, Group, SimpleGrid, Stack, Text} from "@mantine/core";
import {useElementSize, type UseSelectionHandlers} from "@mantine/hooks";
import {useVirtualizer, type VirtualItem, Virtualizer} from "@tanstack/react-virtual";
import {useMemo, useState} from "react";
import {cls} from "../../../../utils/react-utils.tsx";
import CollectionActions from "../collection-actions.tsx";
import {type CollectionSchema} from "../collection-schema.tsx";
import styles from './collection-grid.module.css';

export interface CollectionGridProps<M> {
    schema: CollectionSchema<M>;
    items: M[];
    selection: M[];
    selectionHandlers: UseSelectionHandlers<React.Key>;
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

    const virtualizer = useVirtualizer({
        count: props.items.length,
        getScrollElement: () => parentRef.current,
        estimateSize: () => 280, // TODO props.schema.estimateListRowHeight,
        overscan: 0,
        lanes: lanes,
        horizontal: false,
    });

    const virtualItems = virtualizer.getVirtualItems();

    const items = virtualItems.map((virtualItem) => {
        const item = props.items[virtualItem.index];

        return <CollectionGridItem
            key={props.schema.key(item)}
            virtualItem={virtualItem}
            virtualizer={virtualizer}
            item={item}
            width={elemSize}
            schema={props.schema}
            selection={props.selection}
            selectionHandlers={props.selectionHandlers}
        />;
    });

    return (
        <Box ref={parentRef} style={{width: "100%", maxHeight: "4000px", overflowY: "auto"}}>
            <Box style={{height: `${virtualizer.getTotalSize()}px`}}>
                <SimpleGrid cols={lanes} spacing={gap} style={{
                    transform: `translateY(${virtualItems[0]?.start ?? 0}px)`
                }}>
                    {items}
                </SimpleGrid>
            </Box>
        </Box>
    );
}

export interface CollectionGridItemProps<M> {
    virtualItem: VirtualItem;
    virtualizer: Virtualizer<HTMLDivElement, Element>;
    schema: CollectionSchema<M>;
    item: M;
    selection: M[];
    selectionHandlers: UseSelectionHandlers<React.Key>;
    width: number;
}

export function CollectionGridItem<M>(props: CollectionGridItemProps<M>) {
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
                data-lane={virtualItem.lane}
                ref={virtualizer.measureElement}
                w={props.width}
                h={props.width + 54}
                onClick={() => selectionHandlers.toggle(schema.key(item))}
                className={cls(
                    styles.item,
                    selection.includes(item) && styles.selected,
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
