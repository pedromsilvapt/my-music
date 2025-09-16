import {ActionIcon, Box, Center, CloseButton, Divider, Group, SegmentedControl, Text, TextInput} from "@mantine/core";
import {IconLayoutGridFilled, IconListDetails, IconSearch, IconSettings, IconTableFilled} from "@tabler/icons-react";
import CollectionActions from "./collection-actions.tsx";
import type {CollectionSchemaAction} from "./collection-schema.tsx";
import styles from './collection-toolbar.module.css';

export interface CollectionToolbarProps<M> {
    search: string;
    setSearch: (search: string) => void;
    selection: M[];
    onClearSelection: () => void;
    actions: CollectionSchemaAction<M>[];
}

export default function CollectionToolbar<M>(props: CollectionToolbarProps<M>) {
    const {search, setSearch} = props;

    return <Group className={styles.toolbar} justify="space-between" grow={true}>
        <Box>
            <SegmentedControl
                data={[
                    {
                        value: 'table',
                        label: (
                            <Center style={{gap: 10}}>
                                <IconTableFilled size={16}/>
                                <span>Table</span>
                            </Center>
                        ),
                    },
                    {
                        value: 'grid',
                        label: (
                            <Center style={{gap: 10}}>
                                <IconLayoutGridFilled size={16}/>
                                <span>Grid</span>
                            </Center>
                        ),
                    },
                    {
                        value: 'list',
                        label: (
                            <Center style={{gap: 10}}>
                                <IconListDetails size={16}/>
                                <span>List</span>
                            </Center>
                        ),
                    },
                ]}
            />
        </Box>
        <TextInput placeholder="Search..."
                   leftSection={<IconSearch/>}
                   value={search}
                   onChange={e => setSearch(e.currentTarget.value)}/>
        <Group justify="flex-end">
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
        </Group>
    </Group>;
}