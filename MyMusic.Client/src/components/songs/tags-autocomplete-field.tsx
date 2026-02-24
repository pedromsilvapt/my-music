import {Checkbox, Group, TagsInput, Text} from "@mantine/core";
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

    return (
        <div>
            <TagsInput
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
                value={tags}
                onChange={handleChange}
                onSearchChange={handleSearchChange}
                data={items.map(item => item.name)}
                comboboxProps={{withinPortal: false}}
                disabled={disabled || (diffMode && !isChecked)}
                error={error}
                splitChars={[',']}
                styles={hasChanged ? {
                    input: {
                        borderColor: 'var(--mantine-color-green-6)',
                        backgroundColor: 'var(--mantine-color-green-0)',
                    }
                } : undefined}
            />
            {hasChanged && originalValue && (
                <Text size="xs" c="dimmed" mt={4}>
                    Original: {originalValue.map(o => o.name).join(", ")}
                </Text>
            )}
        </div>
    );
}
