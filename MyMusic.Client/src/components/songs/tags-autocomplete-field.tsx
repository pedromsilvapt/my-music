import {Box, Checkbox, Group, Input, TagsInput, Text} from "@mantine/core";
import type {TagsInputProps} from "@mantine/core";
import {useDebouncedValue} from "@mantine/hooks";
import {IconUser} from "@tabler/icons-react";
import {useCallback, useEffect, useMemo, useRef, useState} from "react";
import Artwork from "../common/artwork";

const SEARCH_DEBOUNCE_MS = 300;

export interface TagsAutocompleteItem {
    id: number;
    name: string;
    coverId?: number | null;
    albumCount?: number;
    songCount?: number;
}

interface TagsAutocompleteFieldProps {
    label: string;
    placeholder?: string;
    value: TagsAutocompleteItem[];
    onChange: (value: TagsAutocompleteItem[]) => void;
    onSearch: (query: string) => Promise<TagsAutocompleteItem[]>;
    disabled?: boolean;
    error?: string;
    diffMode?: boolean;
    originalValue?: TagsAutocompleteItem[];
    isChecked?: boolean;
    onCheckChange?: (checked: boolean) => void;
    originalDisplayValue?: string;
    showArtwork?: boolean;
    testId?: string;
}

export default function TagsAutocompleteField({
                                                    label,
                                                    placeholder,
                                                    value,
                                                    onChange,
                                                    onSearch,
                                                    disabled,
                                                    error,
                                                    diffMode,
                                                    originalValue,
                                                    isChecked = true,
                                                    onCheckChange,
                                                    originalDisplayValue,
                                                    showArtwork = false,
                                                    testId,
                                                }: TagsAutocompleteFieldProps) {
    const [items, setItems] = useState<TagsAutocompleteItem[]>([]);
    const [query, setQuery] = useState("");
    const pendingSelectedIdRef = useRef<number | null>(null);

    const handleSearch = useCallback(async (searchQuery: string) => {
        if (searchQuery.length < 1) {
            setItems([]);
            return;
        }
        try {
            const results = await onSearch(searchQuery);
            setItems(results);
        } catch {
            setItems([]);
        }
    }, [onSearch]);

    const [debouncedQuery] = useDebouncedValue(query, SEARCH_DEBOUNCE_MS);

    useEffect(() => {
        handleSearch(debouncedQuery);
    }, [debouncedQuery, handleSearch]);

    const hasChanged = diffMode && originalValue &&
        (value.length !== originalValue.length ||
            !value.every(v => originalValue.some(o => o.id === v.id && o.name === v.name)));

    const tags = value.map(v => v.name);

    const dropdownData = useMemo(() => 
        items.map(item => ({ value: String(item.id), label: item.name })),
        [items]
    );

    const itemsById = useMemo(() => {
        const map = new Map<number, TagsAutocompleteItem>();
        for (const item of items) {
            map.set(item.id, item);
        }
        return map;
    }, [items]);

    const handleChange = (newTags: string[]) => {
        const existingByName = new Map<string, TagsAutocompleteItem[]>();
        for (const v of value) {
            const list = existingByName.get(v.name) || [];
            list.push(v);
            existingByName.set(v.name, list);
        }

        const usedCountByName = new Map<string, number>();

        const newValue: TagsAutocompleteItem[] = newTags.map(tag => {
            if (pendingSelectedIdRef.current !== null) {
                const selectedItem = itemsById.get(pendingSelectedIdRef.current);
                if (selectedItem && selectedItem.name === tag) {
                    pendingSelectedIdRef.current = null;
                    return selectedItem;
                }
            }

            const existingList = existingByName.get(tag);
            if (existingList) {
                const count = usedCountByName.get(tag) || 0;
                if (count < existingList.length) {
                    usedCountByName.set(tag, count + 1);
                    return existingList[count];
                }
            }

            const fromSearch = items.find(i => i.name === tag);
            return fromSearch || {id: -Date.now(), name: tag};
        });

        pendingSelectedIdRef.current = null;
        onChange(newValue);
    };

    const handleOptionSubmit = (val: string) => {
        const id = parseInt(val, 10);
        if (!isNaN(id)) {
            pendingSelectedIdRef.current = id;
        }
    };

    const renderOption: TagsInputProps['renderOption'] = ({option}) => {
        const id = parseInt(option.value, 10);
        const item = itemsById.get(id);
        if (!item) return null;
        const counts = [];
        if (item.albumCount !== undefined) counts.push(`${item.albumCount} albums`);
        if (item.songCount !== undefined) counts.push(`${item.songCount} songs`);
        return (
            <Group gap="sm" wrap="nowrap">
                <Artwork
                    id={item.coverId}
                    size={32}
                    placeholderIcon={<IconUser size={16}/>}
                />
                <div>
                    <Text size="sm">{item.name}</Text>
                    {counts.length > 0 && (
                        <Text size="xs" c="dimmed">{counts.join(', ')}</Text>
                    )}
                </div>
            </Group>
        );
    };

    if (diffMode && originalDisplayValue != null) {
        const oldBorderColor = isChecked ? 'var(--mantine-color-red-6)' : 'var(--mantine-color-gray-5)';
        const oldBgColor = isChecked ? 'var(--mantine-color-red-0)' : 'var(--mantine-color-gray-1)';
        const newBorderColor = isChecked ? 'var(--mantine-color-green-6)' : 'var(--mantine-color-gray-5)';
        const newBgColor = isChecked ? 'var(--mantine-color-green-0)' : 'var(--mantine-color-gray-1)';

        return (
            <div>
                <Group gap="xs" align="flex-start">
                    {onCheckChange && (
                        <Checkbox
                            checked={isChecked}
                            onChange={(e) => onCheckChange(e.currentTarget.checked)}
                            mt={24}
                        />
                    )}
                    <Box style={{flex: 1}}>
                        <Input.Wrapper label={label + " (old)"}>
                            <Input
                                value={originalDisplayValue}
                                readOnly
                                styles={{
                                    input: {
                                        borderColor: oldBorderColor,
                                        backgroundColor: oldBgColor,
                                        color: 'var(--mantine-color-gray-7)',
                                    }
                                }}
                            />
                        </Input.Wrapper>
                    </Box>
                    <Box style={{flex: 1}}>
                        <Input.Wrapper label={label + " (new)"}>
                            <TagsInput
                                placeholder={placeholder}
                                value={tags}
                                onChange={handleChange}
                                onSearchChange={setQuery}
                                onOptionSubmit={handleOptionSubmit}
                                data={dropdownData}
                                comboboxProps={{withinPortal: false}}
                                disabled={disabled || !isChecked}
                                splitChars={[',']}
                                renderOption={showArtwork ? renderOption : undefined}
                                data-testid={testId}
                                styles={{
                                    input: {
                                        borderColor: newBorderColor,
                                        backgroundColor: newBgColor,
                                        color: 'var(--mantine-color-gray-7)',
                                    }
                                }}
                            />
                        </Input.Wrapper>
                    </Box>
                </Group>
                {error && <Text size="xs" c="red" mt={4}>{error}</Text>}
            </div>
        );
    }

    return (
        <div>
            <TagsInput
                label={label}
                placeholder={placeholder}
                value={tags}
                onChange={handleChange}
                onSearchChange={setQuery}
                onOptionSubmit={handleOptionSubmit}
                data={dropdownData}
                comboboxProps={{withinPortal: false}}
                disabled={disabled}
                error={error}
                splitChars={[',']}
                renderOption={showArtwork ? renderOption : undefined}
                data-testid={testId}
                styles={hasChanged ? {
                    input: {
                        borderColor: 'var(--mantine-color-green-6)',
                        backgroundColor: 'var(--mantine-color-green-0)',
                    }
                } : undefined}
            />
            {hasChanged && (
                <Text size="xs" c="dimmed" mt={4}>
                    Original: {originalDisplayValue ?? (originalValue && originalValue.map(o => o.name).join(", "))}
                </Text>
            )}
        </div>
    );
}
