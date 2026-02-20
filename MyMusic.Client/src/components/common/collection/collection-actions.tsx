import {ActionIcon, Group, Menu, Tooltip} from "@mantine/core";
import {useUncontrolled} from "@mantine/hooks";
import {IconDotsVertical} from "@tabler/icons-react";
import {useCallback} from "react";
import type {CollectionSchemaAction, CollectionSchemaActionButton} from "./collection-schema.tsx";

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

    return <>
        <Group gap="xs">
            {primaryActions.length > 0 && primaryActions.map(action =>
                <Tooltip label={action.renderLabel()} openDelay={500}>
                    <ActionIcon
                        variant="default"
                        size="lg"
                        // aria-label={action.renderLabel()}
                        // title={action.renderLabel()}
                        onClick={_ev => {
                            // ev.stopPropagation();
                            action.onClick(props.selection);
                        }}>
                        {action.renderIcon()}
                    </ActionIcon>
                </Tooltip>)}

            {secondaryActions.length > 0 && <Menu shadow="md" width={200} opened={opened} onChange={setOpened}>
                <Menu.Target>
                    <ActionIcon
                        variant="default"
                        // size="lg"
                        aria-label="Actions"
                        title="Actions"
                        // onClick={ev => ev.stopPropagation()}
                        // onMouseDown={ev => ev.stopPropagation()}
                        // onMouseUp={ev => ev.stopPropagation()}
                    >
                        <IconDotsVertical/>
                    </ActionIcon>
                </Menu.Target>
                <Menu.Dropdown>
                    {secondaryActions.map((action, i) => <CollectionActionMenu key={i} action={action}
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

function CollectionActionMenu<M>(props: CollectionActionMenuProps<M>) {
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