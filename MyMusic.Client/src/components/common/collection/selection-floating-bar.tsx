import {computePosition, flip, offset, shift} from '@floating-ui/dom';
import {CloseButton, Group, Paper, Portal, Text, Transition} from "@mantine/core";
import {useLayoutEffect, useRef, useState} from "react";
import {ZINDEX_FLOATING_BAR} from "../../../consts.ts";
import CollectionActions from "./collection-actions.tsx";
import type {CollectionSchemaAction} from "./collection-schema.tsx";

export interface SelectionFloatingBarProps<M> {
    selection: M[];
    actions: CollectionSchemaAction<M>[];
    anchorElement: HTMLElement | null;
    containerRef: React.RefObject<HTMLElement | null>;
    onClearSelection: () => void;
}

const isElementInViewport = (el: HTMLElement) => {
    const rect = el.getBoundingClientRect();
    return (
        rect.top >= 0 &&
        rect.left >= 0 &&
        rect.bottom <= (window.innerHeight || document.documentElement.clientHeight) &&
        rect.right <= (window.innerWidth || document.documentElement.clientWidth)
    );
};

export default function SelectionFloatingBar<M>(props: SelectionFloatingBarProps<M>) {
    const {selection, actions, anchorElement, containerRef, onClearSelection} = props;
    const floatingRef = useRef<HTMLDivElement>(null);
    const [position, setPosition] = useState({x: 0, y: 0, placement: 'bottom-start' as string});

    const showAtAnchor = anchorElement && isElementInViewport(anchorElement);
    const containerElement = containerRef.current;
    const showAtContainer = containerElement && !showAtAnchor;
    const canShow = selection.length > 0 && (showAtAnchor || showAtContainer);

    useLayoutEffect(() => {
        if (!floatingRef.current) {
            return;
        }

        const containerEl = containerRef.current;

        const update = async () => {
            if (!floatingRef.current) return;

            let virtualEl: { getBoundingClientRect: () => DOMRect };
            let placement: 'top' | 'bottom' = 'bottom';

            if (showAtAnchor && anchorElement) {
                virtualEl = {
                    getBoundingClientRect: () => anchorElement.getBoundingClientRect(),
                };
                placement = 'bottom';
            } else if (showAtContainer && containerEl) {
                const rect = containerEl.getBoundingClientRect();
                virtualEl = {
                    getBoundingClientRect: () => new DOMRect(
                        rect.left + rect.width / 2 - 100,
                        rect.bottom - 50,
                        200,
                        50
                    ),
                };
                placement = 'top';
            } else {
                return;
            }

            const result = await computePosition(virtualEl as Element, floatingRef.current, {
                placement,
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
    }, [anchorElement, containerRef.current, showAtAnchor, showAtContainer]);

    if (!canShow) {
        return null;
    }

    return (
        <Transition
            mounted={canShow}
            transition="slide-up"
            duration={200}
        >
            {(transitionStyles) => (
                <Portal>
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
                </Portal>
            )}
        </Transition>
    );
}
