import {computePosition, flip, offset, shift} from '@floating-ui/dom';
import {CloseButton, Group, Paper, Portal, Text, Transition} from "@mantine/core";
import {useLayoutEffect, useMemo, useRef, useState} from "react";
import CollectionActions from "./collection-actions.tsx";
import type {CollectionSchemaAction} from "./collection-schema.tsx";
import type {SelectionStore} from "./selection-store.ts";

export interface SelectionFloatingBarProps<M> {
    items: M[];
    itemKey: (item: M) => React.Key;
    selectionStore: SelectionStore;
    actionsFn?: (selection: M[]) => CollectionSchemaAction<M>[];
    containerRef: React.RefObject<HTMLElement | null>;
    portalTarget: React.RefObject<HTMLElement | null>;
    onClearSelection: () => void;
    isContextMenuOpen?: boolean;
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
    const {items, itemKey, selectionStore, actionsFn, containerRef, portalTarget, onClearSelection, isContextMenuOpen} = props;
    const floatingRef = useRef<HTMLDivElement>(null);
    const [position, setPosition] = useState({x: 0, y: 0, placement: 'bottom-start' as string});

    const selectedKeys = selectionStore(state => state.selectedKeys);
    const anchorElement = selectionStore(state => state.lastSelectedElement);
    const selectionCount = selectedKeys.size;

    const selection = useMemo(() => items.filter(item => selectedKeys.has(itemKey(item))), [items, itemKey, selectedKeys]);
    const actions = useMemo(() => actionsFn?.(selection) ?? [], [actionsFn, selection]);

    const showAtAnchor = anchorElement && isElementInViewport(anchorElement);
    const containerElement = containerRef.current;
    const showAtContainer = containerElement && !showAtAnchor;
    const canShow = selectionCount > 0 && (showAtAnchor || showAtContainer) && !isContextMenuOpen;

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
    }, [anchorElement, containerRef, showAtAnchor, showAtContainer]);

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
                <Portal target={portalTarget.current ?? undefined}>
                    <Paper
                        ref={floatingRef}
                        style={{
                            position: 'fixed',
                            left: position.x,
                            top: position.y,
                            zIndex: 1,
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
