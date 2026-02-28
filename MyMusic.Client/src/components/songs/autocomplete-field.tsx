import {Autocomplete, type AutocompleteProps, Checkbox, Group, Loader, Text} from "@mantine/core";
import {useDebouncedValue} from "@mantine/hooks";
import {IconDisc} from "@tabler/icons-react";
import {useCallback, useEffect, useMemo, useState} from "react";
import Artwork from "../common/artwork";

const SEARCH_DEBOUNCE_MS = 300;

export interface AutocompleteItem {
    id: number;
    name: string;
    subtitle?: string;
    coverId?: number | null;
    artistName?: string | null;
}

interface AutocompleteFieldProps {
    label: string;
    placeholder?: string;
    value: AutocompleteItem | null;
    onChange: (value: AutocompleteItem | string | null) => void;
    onSearch: (query: string) => Promise<AutocompleteItem[]>;
    disabled?: boolean;
    error?: string;
    diffMode?: boolean;
    originalValue?: AutocompleteItem | null;
    isChecked?: boolean;
    onCheckChange?: (checked: boolean) => void;
    showArtwork?: boolean;
}

export default function AutocompleteField({
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
                                              showArtwork = false,
                                          }: AutocompleteFieldProps) {
    const [query, setQuery] = useState(value?.name || "");
    const [items, setItems] = useState<AutocompleteItem[]>([]);
    const [loading, setLoading] = useState(false);

    useEffect(() => {
        setQuery(value?.name || "");
    }, [value]);

    const handleSearch = useCallback(async (searchQuery: string) => {
        if (searchQuery.length < 1) {
            setItems([]);
            return;
        }
        setLoading(true);
        try {
            const results = await onSearch(searchQuery);
            setItems(results);
        } finally {
            setLoading(false);
        }
    }, [onSearch]);

    const [debouncedQuery] = useDebouncedValue(query, SEARCH_DEBOUNCE_MS);

    useEffect(() => {
        handleSearch(debouncedQuery);
    }, [debouncedQuery, handleSearch]);

    const hasChanged = diffMode && originalValue && value &&
        (originalValue.id !== value?.id || originalValue.name !== value?.name);

    const itemsRecord = useMemo(() => {
        const record: Record<string, AutocompleteItem> = {};
        for (const item of items) {
            record[item.name] = item;
        }
        return record;
    }, [items]);

    const data = useMemo(() => items.map(item => item.name), [items]);

    const handleBlur = () => {
        if (query === "") {
            onChange(null);
        } else if (value?.name !== query) {
            const existingItem = items.find(item => item.name === query);
            if (existingItem) {
                onChange(existingItem);
            } else {
                onChange(query);
            }
        }
    };

    const renderOption: AutocompleteProps['renderOption'] = ({option}) => {
        const item = itemsRecord[option.value];
        return (
            <Group gap="sm" wrap="nowrap">
                <Artwork
                    id={item.coverId ?? item.id}
                    size={32}
                    placeholderIcon={<IconDisc size={16}/>}
                />
                <div>
                    <Text size="sm">{item.name}</Text>
                    {item.artistName && (
                        <Text size="xs" c="dimmed">{item.artistName}</Text>
                    )}
                </div>
            </Group>
        );
    };

    return (
        <div>
            <Autocomplete
                label={diffMode ? (
                    <Group gap="xs">
                        {onCheckChange && (
                            <Checkbox
                                checked={isChecked}
                                onChange={(e) => onCheckChange(e.currentTarget.checked)}
                            />
                        )}
                        <span>{label}</span>
                    </Group>
                ) : label}
                placeholder={placeholder}
                value={query}
                onChange={setQuery}
                onBlur={handleBlur}
                onOptionSubmit={(val) => {
                    setQuery(val);
                    const existingItem = items.find(item => item.name === val);
                    if (existingItem) {
                        onChange(existingItem);
                    }
                }}
                comboboxProps={{withinPortal: false}}
                data={data}
                disabled={disabled || (diffMode && !isChecked)}
                error={error}
                rightSection={loading ? <Loader size={16}/> : null}
                limit={15}
                renderOption={showArtwork ? renderOption : undefined}
                styles={hasChanged ? {
                    input: {
                        borderColor: 'var(--mantine-color-green-6)',
                        backgroundColor: 'var(--mantine-color-green-0)',
                    }
                } : undefined}
            />
            {hasChanged && (
                <Text size="xs" c="dimmed" mt={4}>
                    Original: {originalValue.name}
                    {originalValue.subtitle && ` (${originalValue.subtitle})`}
                </Text>
            )}
        </div>
    );
}
