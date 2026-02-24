import {Autocomplete, Checkbox, Group, Loader, Text} from "@mantine/core";
import {useCallback, useEffect, useState} from "react";

export interface AutocompleteItem {
    id: number;
    name: string;
    subtitle?: string;
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

    useEffect(() => {
        const timer = setTimeout(() => {
            handleSearch(query);
        }, 300);
        return () => clearTimeout(timer);
    }, [query, handleSearch]);

    const hasChanged = diffMode && originalValue && value &&
        (originalValue.id !== value?.id || originalValue.name !== value?.name);

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
                data={items.map(item => item.name)}
                disabled={disabled || (diffMode && !isChecked)}
                error={error}
                rightSection={loading ? <Loader size={16}/> : null}
                limit={15}
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
