import {autoUpdate, computePosition, flip, offset, shift} from '@floating-ui/dom';
import {CloseButton, Group, Paper, Text, Transition} from "@mantine/core";
import {useLayoutEffect, useRef, useState} from "react";
import {ZINDEX_FLOATING_BAR} from "../../../consts.ts";
import CollectionActions from "./collection-actions.tsx";
import type {CollectionSchemaAction} from "./collection-schema.tsx";

export interface SelectionFloatingBarProps<M> {
    selection: M[];
    actions: CollectionSchemaAction<M>[];
    anchorElement: HTMLElement | null;
    onClearSelection: () => void;
}

export default function SelectionFloatingBar<M>(props: SelectionFloatingBarProps<M>) {
    const {selection, actions, anchorElement, onClearSelection} = props;
    const floatingRef = useRef<HTMLDivElement>(null);
    const [position, setPosition] = useState({x: 0, y: 0, placement: 'bottom-start' as string});

    useLayoutEffect(() => {
        if (!anchorElement || !floatingRef.current) {
            return;
        }

        const virtualEl = {
            getBoundingClientRect: () => anchorElement.getBoundingClientRect(),
        };

        const update = async () => {
            if (!floatingRef.current) return;

            const result = await computePosition(virtualEl as Element, floatingRef.current, {
                placement: 'bottom',
                middleware: [
                    offset(8),
                    flip({
                        fallbackPlacements: ['top'],
                    }),
                    shift({
                        padding: 8,
                    }),
                ],
                strategy: 'fixed',
            });

            setPosition({x: result.x, y: result.y, placement: result.placement});
        };

        update();

        return autoUpdate(virtualEl as Element, floatingRef.current, update);
    }, [anchorElement]);

    if (selection.length === 0 || !anchorElement) {
        return null;
    }

    return (
        <Transition
            mounted={selection.length > 0 && anchorElement != null}
            transition="slide-up"
            duration={200}
        >
            {(transitionStyles) => (
                <Paper
                    ref={floatingRef}
                    style={{
                        position: 'fixed',
                        left: position.x,
                        top: position.y,
                        zIndex: ZINDEX_FLOATING_BAR,
                        ...transitionStyles,
                    }}
                    shadow="lg"
                    px="sm"
                    py="xs"
                    radius="md"
                    withBorder
                >
                    <Group gap="sm">
                        <CloseButton
                            size="sm"
                            onClick={onClearSelection}
                            title="Clear selection"
                        />
                        <Text size="sm" fw={500}>
                            {selection.length} selected
                        </Text>
                        <CollectionActions
                            actions={actions}
                            selection={selection}
                        />
                    </Group>
                </Paper>
            )}
        </Transition>
    );
}
