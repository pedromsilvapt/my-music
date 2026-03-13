import {Box, Checkbox, Group, Input, TagsInput, Text} from "@mantine/core";
import {useCallback, useState} from "react";

export interface TagsAutocompleteItem {
    id: number;
    name: string;
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
                                                }: TagsAutocompleteFieldProps) {
    const [items, setItems] = useState<TagsAutocompleteItem[]>([]);

    const handleSearchChange = useCallback(async (query: string) => {
        if (query.length < 1) {
            setItems([]);
            return;
        }
        try {
            const results = await onSearch(query);
            setItems(results);
        } catch {
            setItems([]);
        }
    }, [onSearch]);

    const hasChanged = diffMode && originalValue &&
        (value.length !== originalValue.length ||
            !value.every(v => originalValue.some(o => o.id === v.id && o.name === v.name)));

    const tags = value.map(v => v.name);

    const handleChange = (newTags: string[]) => {
        const newValue: TagsAutocompleteItem[] = newTags.map(tag => {
            const existing = value.find(v => v.name === tag);
            const fromSearch = items.find(i => i.name === tag);
            return existing || fromSearch || {id: -Date.now(), name: tag};
        });
        onChange(newValue);
    };

    if (diffMode && originalDisplayValue != null) {
        const oldBorderColor = isChecked ? 'var(--mantine-color-red-6)' : 'var(--mantine-color-gray-5)';
        const oldBgColor = isChecked ? 'var(--mantine-color-red-0)' : 'var(--mantine-color-gray-1)';
        const newBorderColor = isChecked ? 'var(--mantine-color-green-6)' : 'var(--mantine-color-gray-5)';
        const newBgColor = isChecked ? 'var(--mantine-color-green-0)' : 'var(--mantine-color-gray-1)';

        return (
            <div>
                <Group gap="xs" align="flex-end">
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
                                onSearchChange={handleSearchChange}
                                data={items.map(item => item.name)}
                                comboboxProps={{withinPortal: false}}
                                disabled={disabled || !isChecked}
                                splitChars={[',']}
                                styles={{
                                    input: {
                                        borderColor: newBorderColor,
                                        backgroundColor: newBgColor,
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
                onSearchChange={handleSearchChange}
                data={items.map(item => item.name)}
                comboboxProps={{withinPortal: false}}
                disabled={disabled}
                error={error}
                splitChars={[',']}
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
