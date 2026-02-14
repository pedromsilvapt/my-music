import {ActionIcon, Box, Center, CloseButton, Divider, Group, SegmentedControl, Text, TextInput} from "@mantine/core";
import {useUncontrolled} from "@mantine/hooks";
import {IconLayoutGridFilled, IconListDetails, IconSearch, IconSettings, IconTableFilled} from "@tabler/icons-react";
import CollectionActions from "./collection-actions.tsx";
import type {CollectionSchemaAction} from "./collection-schema.tsx";
import styles from './collection-toolbar.module.css';

export type CollectionView = 'table' | 'list' | 'grid';

export interface CollectionToolbarProps<M> {
    search?: string;
    setSearch?: (search: string) => void;
    view?: CollectionView;
    setView?: (view: CollectionView) => void;
    selection: M[];
    onClearSelection: () => void;
    actions: CollectionSchemaAction<M>[];

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
            {props.selection.length > 0 &&
                <>
                    <Group gap={8} className={styles.selectedActions}>
                        <CloseButton onClick={() => props.onClearSelection()}/>

                        <Text span c="blue" fw="bold" inherit>{props.selection.length} Selected</Text>

                        <CollectionActions actions={props.actions} selection={props.selection}/>
                    </Group>
                    <Divider orientation="vertical" style={{marginTop: 5, marginBottom: 5}}/>
                </>
            }

            <ActionIcon
                variant="default"
                size="lg"
                aria-label="Customize"
                title="Customize"
            >
                <IconSettings/>
            </ActionIcon>
        </Group>;

    return <Group className={styles.toolbar} justify="space-between" grow={true}>
        {leftSection}
        {middleSection}
        {rightSection}
    </Group>;
}