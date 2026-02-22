import {ActionIcon, Button, Group, Popover, Stack, Text, TextInput, Tooltip} from "@mantine/core";
import {useDebouncedValue} from "@mantine/hooks";
import {IconCode, IconFilter, IconSearch, IconX} from "@tabler/icons-react";
import {useEffect, useState} from "react";
import {FILTER_DEBOUNCE_MS} from "../../../consts.ts";
import {FilterCodeEditor} from "../../filters/filter-code-editor.tsx";
import type {FilterMetadataResponse} from "../../filters/use-filter-metadata.ts";

export interface CollectionFilterBarProps {
    searchValue: string;
    onSearchChange: (value: string) => void;
    filterValue: string;
    onFilterChange: (value: string) => void;
    onApply?: (filterValue: string) => void;
    placeholder?: string;
    filterMode: 'client' | 'server' | 'none';
    filterMetadata?: FilterMetadataResponse;
    fetchFilterValues?: (field: string, searchTerm: string) => Promise<string[]>;
}

export function CollectionFilterBar({
                                        searchValue,
                                        onSearchChange,
                                        filterValue,
                                        onFilterChange,
                                        onApply,
                                        placeholder = "Search...",
                                        filterMode,
                                        filterMetadata,
                                        fetchFilterValues,
                                    }: CollectionFilterBarProps) {
    const [showAdvanced, setShowAdvanced] = useState(false);
    const [localSearch, setLocalSearch] = useState(searchValue);
    const [localFilter, setLocalFilter] = useState(filterValue);
    const [debouncedSearch] = useDebouncedValue(localSearch, FILTER_DEBOUNCE_MS);
    const hasFilter = filterValue.trim().length > 0;

    useEffect(() => {
        setLocalFilter(filterValue);
    }, [filterValue]);

    useEffect(() => {
        if (filterMode === 'server' && debouncedSearch !== searchValue) {
            onSearchChange(debouncedSearch);
        }
    }, [debouncedSearch, filterMode, onSearchChange, searchValue]);

    useEffect(() => {
        if (filterMode === 'client') {
            setLocalSearch(searchValue);
        }
    }, [searchValue, filterMode]);

    const handleClearFilter = () => {
        setLocalFilter("");
        onFilterChange("");
    };

    const handleApply = () => {
        if (localFilter !== filterValue) {
            onFilterChange(localFilter);
        }
        onApply?.(localFilter);
    };

    return (
        <Group gap="sm" align="center" justify="space-between" style={{flex: 1}}>
            <TextInput
                placeholder={placeholder}
                leftSection={<IconSearch size={16}/>}
                value={localSearch}
                onChange={(e) => {
                    setLocalSearch(e.target.value);
                    if (filterMode === 'client') {
                        onSearchChange(e.target.value);
                    }
                }}
                style={{flex: 1, maxWidth: 300}}
                styles={{
                    input: {
                        backgroundColor: "var(--mantine-color-gray-0)",
                    },
                }}
                rightSection={
                    localSearch ? (
                        <ActionIcon
                            size="sm"
                            variant="subtle"
                            onClick={() => {
                                setLocalSearch("");
                                onSearchChange("");
                            }}
                        >
                            <IconX size={12}/>
                        </ActionIcon>
                    ) : null
                }
            />

            {filterMode !== 'none' && (
                <Group gap="xs">
                    {hasFilter && (
                        <Tooltip label="Clear filter">
                            <ActionIcon
                                variant="subtle"
                                color="gray"
                                onClick={handleClearFilter}
                            >
                                <IconX size={16}/>
                            </ActionIcon>
                        </Tooltip>
                    )}

                    <Popover
                        opened={showAdvanced}
                        onChange={setShowAdvanced}
                        position="bottom-end"
                        width={400}
                        shadow="md"
                    >
                        <Popover.Target>
                            <Tooltip label={showAdvanced ? "Hide advanced filter" : "Show advanced filter"}>
                                <Button
                                    variant={hasFilter ? "light" : "subtle"}
                                    leftSection={hasFilter ? <IconFilter size={16}/> : <IconCode size={16}/>}
                                    onClick={() => setShowAdvanced(!showAdvanced)}
                                    color={hasFilter ? "blue" : "gray"}
                                    size="sm"
                                >
                                    {hasFilter ? "Filtered" : "Advanced"}
                                </Button>
                            </Tooltip>
                        </Popover.Target>

                        <Popover.Dropdown>
                            <Stack gap="xs">
                                <Group justify="space-between" align="center">
                                    <Text size="sm" fw={500} c="dimmed">
                                        Filter DSL
                                    </Text>
                                    <Text size="xs" c="dimmed">
                                        Press Ctrl+Enter to apply
                                    </Text>
                                </Group>

                                <FilterCodeEditor
                                    value={localFilter}
                                    onChange={setLocalFilter}
                                    onApply={handleApply}
                                    height={100}
                                    metadata={filterMetadata}
                                    fetchFilterValues={fetchFilterValues}
                                />

                                <Text size="xs" c="dimmed">
                                    Examples: <code>year &gt;= 2020</code>, <code>name contains "love"</code>
                                </Text>

                                <Group justify="space-between" align="center">
                                    <Button
                                        size="xs"
                                        variant="subtle"
                                        onClick={handleClearFilter}
                                        disabled={!hasFilter}
                                    >
                                        Clear
                                    </Button>
                                    <Button
                                        size="xs"
                                        onClick={handleApply}
                                        disabled={localFilter === filterValue}
                                    >
                                        Apply
                                    </Button>
                                </Group>
                            </Stack>
                        </Popover.Dropdown>
                    </Popover>
                </Group>
            )}
        </Group>
    );
}
