import {ActionIcon, Menu} from "@mantine/core";
import {useUncontrolled} from "@mantine/hooks";
import {IconDotsVertical} from "@tabler/icons-react";
import type {CollectionSchemaAction} from "./collection-schema.tsx";

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

    return <>
        <Menu shadow="md" width={200} opened={opened} onChange={setOpened}>
            <Menu.Target>
                <ActionIcon
                    variant="default"
                    // size="lg"
                    aria-label="Actions"
                    title="Actions"
                    onClick={ev => ev.stopPropagation()}
                >
                    <IconDotsVertical/>
                </ActionIcon>
            </Menu.Target>
            <Menu.Dropdown>
                {props.actions.map((action, i) => <CollectionActionMenu key={i} action={action}
                                                                        selection={props.selection}/>)}
            </Menu.Dropdown>
        </Menu>
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
            >
                {action.renderLabel()}
            </Menu.Item>
        )
    }
}