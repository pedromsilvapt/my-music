import {Combobox, InputBase, Loader, ScrollArea, useCombobox} from "@mantine/core";
import {useId} from "@mantine/hooks";
import {useVirtualizer} from "@tanstack/react-virtual";
import type React from "react";
import {memo, useEffect, useMemo, useRef, useState} from "react";
import {VIRTUALIZER_OVERSCAN, ZINDEX_MODAL} from "../../consts.ts";
import {buildGoToSearchVectors, filterGoToItems} from "./collection/goto-filter.ts";
import {isIndexFullyInViewport} from "./collection/views/virtualizer-utils.ts";
import styles from "./virtual-select.module.css";

const LIST_MAX_HEIGHT = 300;

export interface VirtualSelectProps<T> {
    items: T[];
    value: T | null;
    onChange: (item: T) => void;
    getKey: (item: T) => string | number;
    /** Text shown in the input for the selected item, and matched against the search. Must be a stable function. */
    getLabel: (item: T) => string;
    /** Must be a stable function: options are memoized on it. */
    renderOption: (item: T) => React.ReactNode;
    optionHeight: number;
    emptyMessage: string;
    label?: React.ReactNode;
    placeholder?: string;
    error?: React.ReactNode;
    required?: boolean;
    disabled?: boolean;
    loading?: boolean;
    leftSection?: React.ReactNode;
    testId?: string;
}

/**
 * A searchable single-value select over an arbitrarily long list of items: only the options inside the dropdown's
 * scroll viewport are rendered. The options list is virtualized, so Mantine's DOM-based keyboard navigation (which
 * only sees rendered options) is disabled and the active option is tracked by index here instead.
 */
export default function VirtualSelect<T>(props: VirtualSelectProps<T>) {
    const {items, value, onChange, getKey, getLabel} = props;
    const listId = useId();
    const [search, setSearch] = useState('');
    const [activeIndex, setActiveIndex] = useState(0);
    // Incremented on keyboard navigation only, so hovering never scrolls the list under the mouse
    const [scrollRequestId, setScrollRequestId] = useState(0);
    const combobox = useCombobox();
    const dropdownOpened = combobox.dropdownOpened;

    // Computed once per item list, so filtering on each keystroke is just a substring check
    const searchVectors = useMemo(() => buildGoToSearchVectors(items, {searchVector: getLabel}), [items, getLabel]);
    const filteredItems = useMemo(
        () => dropdownOpened ? filterGoToItems(items, searchVectors, search) : [],
        [dropdownOpened, items, searchVectors, search]);

    const activeItem = filteredItems[activeIndex] as T | undefined;

    const openDropdown = () => {
        if (!dropdownOpened && !props.disabled) {
            setSearch('');
            setActiveIndex(0);
            combobox.openDropdown();
        }
    };

    const closeDropdown = () => {
        combobox.closeDropdown();
        setSearch('');
    };

    const submit = (item: T) => {
        onChange(item);
        closeDropdown();
    };

    const handleOptionSubmit = (optionValue: string) => {
        const item = filteredItems.find(i => String(getKey(i)) === optionValue);
        if (item) {
            submit(item);
        }
    };

    const handleKeyDown = (event: React.KeyboardEvent<HTMLInputElement>) => {
        if (event.nativeEvent.isComposing) return;

        if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
            event.preventDefault();
            if (!dropdownOpened) {
                openDropdown();
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
            // The first Escape closes the dropdown; while it is open, data-mantine-stop-propagation keeps an
            // enclosing Modal from closing as well
            closeDropdown();
        }
    };

    return (
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
                    label={props.label}
                    placeholder={props.placeholder}
                    error={props.error}
                    required={props.required}
                    disabled={props.disabled}
                    leftSection={props.leftSection}
                    rightSection={props.loading ? <Loader size={16}/> : <Combobox.Chevron/>}
                    rightSectionPointerEvents="none"
                    autoComplete="off"
                    role="combobox"
                    aria-haspopup="listbox"
                    aria-expanded={dropdownOpened}
                    aria-controls={dropdownOpened ? listId : undefined}
                    aria-activedescendant={dropdownOpened && activeItem ? getOptionId(listId, activeIndex) : undefined}
                    data-expanded={dropdownOpened || undefined}
                    data-mantine-stop-propagation={dropdownOpened || undefined}
                    data-testid={props.testId}
                    // While the dropdown is open the input holds the search text; otherwise, the selected item
                    value={dropdownOpened ? search : (value ? getLabel(value) : '')}
                    onChange={event => {
                        openDropdown();
                        setSearch(event.currentTarget.value);
                        setActiveIndex(0);
                    }}
                    onClick={openDropdown}
                    onBlur={closeDropdown}
                    onKeyDown={handleKeyDown}
                />
            </Combobox.Target>

            <Combobox.Dropdown>
                <Combobox.Options id={listId}>
                    {filteredItems.length === 0
                        ? <Combobox.Empty>{props.emptyMessage}</Combobox.Empty>
                        : <VirtualOptions
                            listId={listId}
                            items={filteredItems}
                            getKey={getKey}
                            getLabel={getLabel}
                            renderOption={props.renderOption}
                            optionHeight={props.optionHeight}
                            activeIndex={activeIndex}
                            scrollRequestId={scrollRequestId}
                            onHover={setActiveIndex}
                        />}
                </Combobox.Options>
            </Combobox.Dropdown>
        </Combobox>
    );
}

function getOptionId(listId: string, index: number) {
    return `${listId}-option-${index}`;
}

interface VirtualOptionsProps<T> extends Pick<VirtualSelectProps<T>, 'items' | 'getKey' | 'getLabel' | 'renderOption' | 'optionHeight'> {
    listId: string;
    activeIndex: number;
    scrollRequestId: number;
    onHover: (index: number) => void;
}

/**
 * Only mounted while the dropdown is open, and only renders the options inside the scroll viewport.
 */
function VirtualOptions<T>({listId, items, getKey, getLabel, renderOption, optionHeight, activeIndex, scrollRequestId, onHover}: VirtualOptionsProps<T>) {
    const viewportRef = useRef<HTMLDivElement>(null);

    const virtualizer = useVirtualizer({
        count: items.length,
        getScrollElement: () => viewportRef.current,
        estimateSize: () => optionHeight,
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
        <ScrollArea.Autosize mah={LIST_MAX_HEIGHT} viewportRef={viewportRef} type="scroll">
            <div style={{height: virtualizer.getTotalSize(), position: 'relative'}}>
                {virtualizer.getVirtualItems().map(virtualItem => {
                    const item = items[virtualItem.index];
                    const selected = virtualItem.index === activeIndex;
                    return (
                        <Combobox.Option
                            key={getKey(item)}
                            id={getOptionId(listId, virtualItem.index)}
                            value={String(getKey(item))}
                            aria-label={getLabel(item)}
                            selected={selected}
                            aria-selected={selected}
                            className={styles.option}
                            style={{height: virtualItem.size, transform: `translateY(${virtualItem.start}px)`}}
                            onMouseEnter={() => onHover(virtualItem.index)}
                        >
                            <VirtualOptionContent item={item} renderOption={renderOption}/>
                        </Combobox.Option>
                    );
                })}
            </div>
        </ScrollArea.Autosize>
    );
}

/**
 * Memoized so that moving the active option only re-renders the option wrappers, not their content. `inert`
 * disables anything interactive inside the content, so every click selects the option instead.
 */
const VirtualOptionContent = memo(function VirtualOptionContent<T>({item, renderOption}: { item: T, renderOption: (item: T) => React.ReactNode }) {
    return <div style={{height: '100%'}} inert>{renderOption(item)}</div>;
}) as <T>(props: { item: T, renderOption: (item: T) => React.ReactNode }) => React.ReactElement;
