import {Combobox, Group, InputBase, Modal, ScrollArea, Stack, Text, useCombobox} from "@mantine/core";
import {useId} from "@mantine/hooks";
import {IconSearch} from "@tabler/icons-react";
import {useVirtualizer} from "@tanstack/react-virtual";
import type React from "react";
import {memo, useEffect, useMemo, useRef, useState} from "react";
import {useTranslation} from "react-i18next";
import {VIRTUALIZER_OVERSCAN, ZINDEX_MODAL} from "../../../consts.ts";
import type {CollectionSchema} from "./collection-schema.tsx";
import {buildGoToSearchVectors, filterGoToItems} from "./goto-filter.ts";
import styles from "./goto-modal.module.css";
import {isIndexFullyInViewport} from "./views/virtualizer-utils.ts";

const GOTO_ARTWORK_SIZE = 40;
const GOTO_OPTION_HEIGHT = 56;
const GOTO_LIST_MAX_HEIGHT = 300;

interface GoToModalProps<T extends { id: string | number }> {
    opened: boolean;
    onClose: () => void;
    items: T[];
    schema: CollectionSchema<T>;
    onSelect: (item: T) => void;
}

/**
 * The options list is virtualized, so Mantine's DOM-based keyboard navigation (which only sees rendered options)
 * is disabled and the active option is tracked by index here instead.
 */
export default function GoToModal<T extends { id: string | number }>({opened, onClose, items, schema, onSelect}: GoToModalProps<T>) {
    const {t} = useTranslation(["collection"]);
    const listId = useId();
    const [search, setSearch] = useState('');
    const [activeIndex, setActiveIndex] = useState(0);
    // Incremented on keyboard navigation only, so hovering never scrolls the list under the mouse
    const [scrollRequestId, setScrollRequestId] = useState(0);
    // The dropdown starts hidden and opens once the user types, clicks the input or presses the arrow keys
    const combobox = useCombobox();
    const dropdownOpened = combobox.dropdownOpened;

    // Only computed while the modal is open; the modal is mounted in every collection toolbar
    const searchVectors = useMemo(() => opened ? buildGoToSearchVectors(items, schema) : [], [opened, items, schema]);
    const filteredItems = useMemo(() => opened ? filterGoToItems(items, searchVectors, search) : [], [opened, items, searchVectors, search]);

    const activeItem = filteredItems[activeIndex] as T | undefined;

    const handleClose = () => {
        setSearch('');
        setActiveIndex(0);
        combobox.closeDropdown();
        onClose();
    };

    const submit = (item: T) => {
        onSelect(item);
        handleClose();
    };

    const handleOptionSubmit = (value: string) => {
        const item = filteredItems.find(i => String(schema.key(i)) === value);
        if (item) {
            submit(item);
        }
    };

    const handleKeyDown = (event: React.KeyboardEvent<HTMLInputElement>) => {
        if (event.nativeEvent.isComposing) return;

        if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
            event.preventDefault();
            if (!dropdownOpened) {
                combobox.openDropdown();
                return;
            }
            const count = filteredItems.length;
            if (count === 0) return;
            const delta = event.key === 'ArrowDown' ? 1 : -1;
            setActiveIndex(index => (Math.min(index, count - 1) + delta + count) % count);
            setScrollRequestId(id => id + 1);
        } else if (event.key === 'Enter') {
            if (dropdownOpened && activeItem) {
                event.preventDefault();
                submit(activeItem);
            }
        } else if (event.key === 'Escape') {
            // The first Escape closes the dropdown; while it is open, data-mantine-stop-propagation keeps the
            // Modal from closing as well
            combobox.closeDropdown();
        }
    };

    return (
        <Modal
            opened={opened}
            onClose={handleClose}
            title={t("collection:goTo.title")}
            overlayProps={{blur: 8}}
            zIndex={ZINDEX_MODAL}
        >
            <Combobox
                store={combobox}
                onOptionSubmit={handleOptionSubmit}
                position="bottom-start"
                middlewares={{flip: false, shift: false}}
                zIndex={ZINDEX_MODAL + 1}
                width="target"
                keepMounted={false}
            >
                <Combobox.Target withKeyboardNavigation={false} withAriaAttributes={false}>
                    <InputBase
                        data-autofocus
                        autoFocus
                        autoComplete="off"
                        leftSection={<IconSearch size={16}/>}
                        placeholder={t("collection:goTo.placeholder")}
                        aria-label={t("collection:goTo.placeholder")}
                        aria-haspopup="listbox"
                        aria-expanded={dropdownOpened}
                        aria-controls={dropdownOpened ? listId : undefined}
                        aria-activedescendant={dropdownOpened && activeItem ? getOptionId(listId, activeIndex) : undefined}
                        data-expanded={dropdownOpened || undefined}
                        data-mantine-stop-propagation={dropdownOpened || undefined}
                        value={search}
                        onChange={event => {
                            setSearch(event.currentTarget.value);
                            setActiveIndex(0);
                            combobox.openDropdown();
                        }}
                        onClick={() => combobox.openDropdown()}
                        onKeyDown={handleKeyDown}
                    />
                </Combobox.Target>

                <Combobox.Dropdown>
                    <Combobox.Options id={listId}>
                        {filteredItems.length === 0
                            ? <Combobox.Empty>{t("collection:goTo.empty")}</Combobox.Empty>
                            : <GoToVirtualOptions
                                listId={listId}
                                items={filteredItems}
                                schema={schema}
                                activeIndex={activeIndex}
                                scrollRequestId={scrollRequestId}
                                onHover={setActiveIndex}
                            />}
                    </Combobox.Options>
                </Combobox.Dropdown>
            </Combobox>
        </Modal>
    );
}

function getOptionId(listId: string, index: number) {
    return `${listId}-option-${index}`;
}

interface GoToVirtualOptionsProps<T> {
    listId: string;
    items: T[];
    schema: CollectionSchema<T>;
    activeIndex: number;
    scrollRequestId: number;
    onHover: (index: number) => void;
}

/**
 * Only mounted while the dropdown is open, and only renders the options inside the scroll viewport.
 */
function GoToVirtualOptions<T>({listId, items, schema, activeIndex, scrollRequestId, onHover}: GoToVirtualOptionsProps<T>) {
    const viewportRef = useRef<HTMLDivElement>(null);

    const virtualizer = useVirtualizer({
        count: items.length,
        getScrollElement: () => viewportRef.current,
        estimateSize: () => GOTO_OPTION_HEIGHT,
        overscan: VIRTUALIZER_OVERSCAN,
    });

    // A new search starts over from the top of the list
    useEffect(() => {
        virtualizer.scrollToOffset(0);
    }, [items, virtualizer]);

    useEffect(() => {
        if (scrollRequestId > 0 && !isIndexFullyInViewport(virtualizer, activeIndex)) {
            virtualizer.scrollToIndex(activeIndex, {align: 'auto'});
        }
        // Only keyboard navigation (scrollRequestId) should scroll, not hover changes to activeIndex
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [scrollRequestId, virtualizer]);

    return (
        <ScrollArea.Autosize mah={GOTO_LIST_MAX_HEIGHT} viewportRef={viewportRef} type="scroll">
            <div style={{height: virtualizer.getTotalSize(), position: 'relative'}}>
                {virtualizer.getVirtualItems().map(virtualItem => {
                    const item = items[virtualItem.index];
                    const selected = virtualItem.index === activeIndex;
                    return (
                        <Combobox.Option
                            key={schema.key(item)}
                            id={getOptionId(listId, virtualItem.index)}
                            value={String(schema.key(item))}
                            aria-label={schema.searchVector(item)}
                            selected={selected}
                            aria-selected={selected}
                            className={styles.option}
                            style={{height: virtualItem.size, transform: `translateY(${virtualItem.start}px)`}}
                            onMouseEnter={() => onHover(virtualItem.index)}
                        >
                            <GoToOptionContent item={item} schema={schema}/>
                        </Combobox.Option>
                    );
                })}
            </div>
        </ScrollArea.Autosize>
    );
}

/**
 * Memoized so that moving the active option only re-renders the option wrappers, not the schema renderers.
 * The schema renderers contain links, play buttons and tooltips; `inert` disables them so every click selects
 * the option instead.
 */
const GoToOptionContent = memo(function GoToOptionContent<T>({item, schema}: { item: T, schema: CollectionSchema<T> }) {
    return (
        <Group gap="sm" wrap="nowrap" h="100%" inert>
            {schema.renderListArtwork(item, GOTO_ARTWORK_SIZE)}
            <Stack gap={0} style={{minWidth: 0}}>
                <Text size="sm" lineClamp={1}>{schema.renderListTitle(item, 1)}</Text>
                <Text size="xs" c="dimmed" lineClamp={1} component="div">
                    {schema.renderListSubTitle(item, 1)}
                </Text>
            </Stack>
        </Group>
    );
}) as <T>(props: { item: T, schema: CollectionSchema<T> }) => React.ReactElement;
