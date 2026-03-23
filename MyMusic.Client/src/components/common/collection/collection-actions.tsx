import {ActionIcon, Box, Group, Menu, Tooltip} from "@mantine/core";
import {useUncontrolled} from "@mantine/hooks";
import {IconDotsVertical} from "@tabler/icons-react";
import {useCallback} from "react";
import type {CollectionSchemaAction, CollectionSchemaActionButton} from "./collection-schema.tsx";
import {useSelectionStoreContext} from "./selection-store.ts";

export interface CollectionActionsProps<M> {
    selection: M[];
    actions: CollectionSchemaAction<M>[];
    opened?: boolean;
    setOpened?: (open: boolean) => void;
}

export default function CollectionActions<M>(props: CollectionActionsProps<M>) {
    const [opened, setOpened] = useUncontrolled({
        value: props.opened,
        defaultValue: false,
        onChange: props.setOpened,
    });

    const isPrimary = useCallback((a: CollectionSchemaAction<M>): a is CollectionSchemaActionButton<M> =>
        !('divider' in a) && !('group' in a) && (a.primary ?? false), []);

    const isSecondary = useCallback((a: CollectionSchemaAction<M>) =>
        !isPrimary(a), [isPrimary]);

    const primaryActions = props.actions.filter(isPrimary);
    const secondaryActions = props.actions.filter(isSecondary);

    const getActionKey = (action: CollectionSchemaAction<M>): string => {
        if ('divider' in action) return 'divider';
        if ('group' in action) return `group-${action.group}`;
        return action.name;
    };

    return <>
        <Group gap="xs">
            {primaryActions.length > 0 && primaryActions.map(action =>
                <Tooltip key={action.name} label={action.renderLabel()} openDelay={500}>
                    <ActionIcon
                        variant="default"
                        size="lg"
                        onClick={() => {
                            action.onClick(props.selection);
                        }}>
                        {action.renderIcon()}
                    </ActionIcon>
                </Tooltip>)}

            {secondaryActions.length > 0 && <Menu shadow="md" width={200} opened={opened} onChange={setOpened}>
                <Menu.Target>
                    <ActionIcon
                        variant="default"
                        aria-label="Actions"
                        title="Actions"
                    >
                        <IconDotsVertical/>
                    </ActionIcon>
                </Menu.Target>
                <Menu.Dropdown>
                    {secondaryActions.map((action) => <CollectionActionMenu key={getActionKey(action)} action={action}
                                                                               selection={props.selection}/>)}
                </Menu.Dropdown>
            </Menu>}
        </Group>
    </>
}

interface CollectionActionMenuProps<M> {
    selection: M[];
    action: CollectionSchemaAction<M>;
}

export function CollectionActionMenu<M>(props: CollectionActionMenuProps<M>) {
    if ('divider' in props.action) {
        return <Menu.Divider/>;
    } else if ('group' in props.action) {
        return <Menu.Label>{props.action.group}</Menu.Label>;
    } else {
        const action = props.action;

        return (
            <Menu.Item
                leftSection={action.renderIcon()}
                onClick={ev => {
                    ev.stopPropagation();
                    action.onClick(props.selection);
                }}
                onMouseDown={ev => ev.stopPropagation()}
                onMouseUp={ev => ev.stopPropagation()}
            >
                {action.renderLabel()}
            </Menu.Item>
        )
    }
}

export interface RowActionsContainerProps<M> {
    item: M;
    actions: CollectionSchemaAction<M>[];
    opened: boolean;
    setOpened: (open: boolean) => void;
    containerClassName: string;
    openedClassName: string;
    hiddenClassName: string;
}

export function RowActionsContainer<M>(props: RowActionsContainerProps<M>) {
    const selectionStore = useSelectionStoreContext();
    const hasSelection = selectionStore(state => state.hasSelection);

    const classNames = [props.containerClassName];
    if (props.opened) {
        classNames.push(props.openedClassName);
    }
    if (hasSelection) {
        classNames.push(props.hiddenClassName);
    }

    return (
        <Box className={classNames.join(' ')}>
            <CollectionActions selection={[props.item]} actions={props.actions} opened={props.opened} setOpened={props.setOpened}/>
        </Box>
    );
}