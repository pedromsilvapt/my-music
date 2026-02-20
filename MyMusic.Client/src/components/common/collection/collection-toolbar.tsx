import {
    closestCenter,
    DndContext,
    type DragEndEvent,
    KeyboardSensor,
    PointerSensor,
    useSensor,
    useSensors
} from "@dnd-kit/core";
import {SortableContext, useSortable, verticalListSortingStrategy} from "@dnd-kit/sortable";
import {CSS} from "@dnd-kit/utilities";
import {
    ActionIcon,
    Box,
    Button,
    Center,
    Group,
    Menu,
    Popover,
    SegmentedControl,
    Stack,
    Text,
    TextInput
} from "@mantine/core";
import {useDisclosure, useUncontrolled} from "@mantine/hooks";
import {
    IconArrowDown,
    IconArrowUp,
    IconGripVertical,
    IconLayoutGridFilled,
    IconListDetails,
    IconSearch,
    IconSettings,
    IconTableFilled,
    IconX
} from "@tabler/icons-react";
import {useMemo} from "react";
import {ZINDEX_MODAL} from "../../../consts.ts";
import type {CollectionSchemaColumn, CollectionSortField} from "./collection-schema.tsx";
import styles from './collection-toolbar.module.css';

export type CollectionView = 'table' | 'list' | 'grid';

export interface CollectionToolbarProps<M> {
    search?: string;
    setSearch?: (search: string) => void;
    view?: CollectionView;
    setView?: (view: CollectionView) => void;

    sort?: CollectionSortField<M>[];
    onSort?: (field: string) => void;
    onSortRemove?: (field: string) => void;
    onReorderSort?: (fromIndex: number, toIndex: number) => void;
    sortableFields?: (keyof M & string)[];
    columns?: CollectionSchemaColumn<M>[];

    renderLeftSection?: () => React.ReactNode;
    renderMiddleSection?: () => React.ReactNode;
    renderRightSection?: () => React.ReactNode;
}

export default function CollectionToolbar<M>(props: CollectionToolbarProps<M>) {
    const [search, setSearch] = useUncontrolled({
        value: props.search,
        defaultValue: '',
        onChange: props.setSearch,
    });
    const [view, setView] = useUncontrolled({
        value: props.view,
        defaultValue: 'table',
        onChange: props.setView,
    });

    const [popoverOpened, {open: openPopover, close: closePopover}] = useDisclosure(false);

    const sensors = useSensors(
        useSensor(PointerSensor),
        useSensor(KeyboardSensor)
    );

    const sortFields = props.sort ?? [];
    const sortableFieldsList = props.sortableFields ?? [];
    const columns = props.columns ?? [];

    const availableFields = useMemo(() => {
        return sortableFieldsList.filter(field => !sortFields.some(s => s.field === field));
    }, [sortableFieldsList, sortFields]);

    const getFieldDisplayName = (field: string) => {
        const col = columns.find(c => c.name === field);
        return col?.displayName ?? field;
    };

    const handleAddField = (field: string) => {
        props.onSort?.(field);
    };

    const handleDragEnd = (event: DragEndEvent) => {
        const {active, over} = event;
        if (!over) return;

        const oldIndex = sortFields.findIndex(s => s.field === active.id);
        const newIndex = sortFields.findIndex(s => s.field === over.id);

        if (oldIndex !== -1 && newIndex !== -1 && oldIndex !== newIndex) {
            props.onReorderSort?.(oldIndex, newIndex);
        }
    };

    const SortableFieldItem = ({field}: { field: CollectionSortField<M> }) => {
        const {
            attributes,
            listeners,
            setNodeRef,
            transform,
            transition,
            isDragging
        } = useSortable({id: field.field});

        const style = {
            transform: CSS.Transform.toString(transform),
            transition,
            opacity: isDragging ? 0.5 : 1,
            zIndex: isDragging ? ZINDEX_MODAL : 'auto',
        };

        return (
            <Group
                ref={setNodeRef}
                style={style}
                gap="xs"
                wrap="nowrap"
                className={isDragging ? styles.dragging : ''}
            >
                <ActionIcon
                    size="sm"
                    variant="subtle"
                    {...attributes}
                    {...listeners}
                    className={styles.dragHandle}
                    title="Drag to reorder"
                >
                    <IconGripVertical size={14}/>
                </ActionIcon>
                <Text size="sm" style={{flex: 1}}>{getFieldDisplayName(field.field)}</Text>
                <ActionIcon
                    size="sm"
                    variant="subtle"
                    onClick={() => props.onSort?.(field.field)}
                    title={field.direction === 'asc' ? 'Ascending' : 'Descending'}
                >
                    {field.direction === 'asc' ? <IconArrowUp size={14}/> : <IconArrowDown size={14}/>}
                </ActionIcon>
                <ActionIcon
                    size="sm"
                    variant="subtle"
                    color="red"
                    onClick={() => props.onSortRemove?.(field.field)}
                    title="Remove"
                >
                    <IconX size={14}/>
                </ActionIcon>
            </Group>
        );
    };

    const leftSection = props.renderLeftSection
        ? props.renderLeftSection()
        : <Box>
            <SegmentedControl
                value={view}
                onChange={view => setView(view as CollectionView)}
                data={[
                    {
                        value: 'table' satisfies CollectionView,
                        label: (
                            <Center style={{gap: 10}}>
                                <IconTableFilled size={16}/>
                                <span>Table</span>
                            </Center>
                        ),
                    },
                    {
                        value: 'grid' satisfies CollectionView,
                        label: (
                            <Center style={{gap: 10}}>
                                <IconLayoutGridFilled size={16}/>
                                <span>Grid</span>
                            </Center>
                        ),
                    },
                    {
                        value: 'list' satisfies CollectionView,
                        label: (
                            <Center style={{gap: 10}}>
                                <IconListDetails size={16}/>
                                <span>List</span>
                            </Center>
                        ),
                    },
                ]}
            />
        </Box>;

    const middleSection = props.renderMiddleSection
        ? props.renderMiddleSection()
        : <TextInput placeholder="Search..."
                     leftSection={<IconSearch/>}
                     value={search}
                     onChange={e => setSearch(e.currentTarget.value)}/>;

    const rightSection = props.renderRightSection
        ? props.renderRightSection()
        : <Group justify="flex-end">
            <Popover
                opened={popoverOpened}
                onChange={(opened) => opened ? openPopover() : closePopover()}
                position="bottom-end"
                withArrow
            >
                <Popover.Target>
                    <ActionIcon
                        variant="default"
                        size="lg"
                        aria-label="Customize"
                        title="Customize"
                        onClick={openPopover}
                    >
                        <IconSettings/>
                    </ActionIcon>
                </Popover.Target>
                <Popover.Dropdown>
                    <Stack gap="sm">
                        <Text size="sm" fw="bold">Sort By</Text>

                        {sortFields.length > 0 && (
                            <DndContext
                                sensors={sensors}
                                collisionDetection={closestCenter}
                                onDragEnd={handleDragEnd}
                            >
                                <SortableContext
                                    items={sortFields.map(f => f.field)}
                                    strategy={verticalListSortingStrategy}
                                >
                                    <Stack gap="xs">
                                        {sortFields.map((field) => (
                                            <SortableFieldItem key={field.field} field={field}/>
                                        ))}
                                    </Stack>
                                </SortableContext>
                            </DndContext>
                        )}

                        {availableFields.length > 0 && (
                            <Menu shadow="md" width={200} withinPortal={false}>
                                <Menu.Target>
                                    <Button variant="light" size="xs" leftSection={<IconArrowUp size={14}/>}>
                                        Add sort field
                                    </Button>
                                </Menu.Target>
                                <Menu.Dropdown>
                                    {availableFields.map(field => (
                                        <Menu.Item
                                            key={field}
                                            onClick={() => handleAddField(field)}
                                        >
                                            {getFieldDisplayName(field)}
                                        </Menu.Item>
                                    ))}
                                </Menu.Dropdown>
                            </Menu>
                        )}

                        {sortFields.length === 0 && availableFields.length === 0 && (
                            <Text size="xs" c="dimmed">No sortable fields available</Text>
                        )}
                    </Stack>
                </Popover.Dropdown>
            </Popover>
        </Group>;

    return <Group className={styles.toolbar} justify="space-between" grow={true}>
        {leftSection}
        {middleSection}
        {rightSection}
    </Group>;
}