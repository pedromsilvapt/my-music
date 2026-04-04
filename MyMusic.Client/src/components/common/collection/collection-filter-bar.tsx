import {ActionIcon, Button, Group, Popover, Stack, Text, TextInput, Tooltip} from "@mantine/core";
import {IconCode, IconFilter, IconSearch, IconX} from "@tabler/icons-react";
import {forwardRef, useEffect, useImperativeHandle, useRef, useState} from "react";
import {FilterCodeEditor} from "../../filters/filter-code-editor.tsx";
import type {FilterMetadataResponse} from "../../filters/use-filter-metadata.ts";

export interface CollectionFilterBarRef {
    focusAndSelect: () => void;
}

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

export const CollectionFilterBar = forwardRef<CollectionFilterBarRef, CollectionFilterBarProps>(
    function CollectionFilterBar({
                                      searchValue,
                                      onSearchChange,
                                      filterValue,
                                      onFilterChange,
                                      onApply,
                                      placeholder = "Search...",
                                      filterMode,
                                      filterMetadata,
                                      fetchFilterValues,
                                  }, ref) {
        const searchInputRef = useRef<HTMLInputElement>(null);

        useImperativeHandle(ref, () => ({
            focusAndSelect: () => {
                const input = searchInputRef.current;
                if (input) {
                    input.focus();
                    input.select();
                }
            }
        }), []);

        const [showAdvanced, setShowAdvanced] = useState(false);

        // Local state for immediate input (controlled by parent prop, synced on prop changes)
        const [localSearch, setLocalSearch] = useState(searchValue);

        // Sync local state when prop changes (e.g., from URL params or parent updates)
        useEffect(() => {
            setLocalSearch(searchValue);
        }, [searchValue]);

        // Debounce timer ref for search input
        const searchDebounceRef = useRef<ReturnType<typeof setTimeout> | null>(null);

        // Cleanup debounce timer on unmount
        useEffect(() => {
            return () => {
                if (searchDebounceRef.current) {
                    clearTimeout(searchDebounceRef.current);
                }
            };
        }, []);

        // Handle search input change with optional debounce for server mode
        const handleSearchChange = (value: string) => {
            setLocalSearch(value);

            // Clear existing timer
            if (searchDebounceRef.current) {
                clearTimeout(searchDebounceRef.current);
                searchDebounceRef.current = null;
            }

            if (filterMode === 'server') {
                // Debounce for server mode
                searchDebounceRef.current = setTimeout(() => {
                    onSearchChange(value);
                    searchDebounceRef.current = null;
                }, 300);
            } else {
                // Immediate for client mode
                onSearchChange(value);
            }
        };

        // Local state for filter DSL (apply on demand, not debounced)
        const [localFilter, setLocalFilter] = useState(filterValue);

        // Sync filter local state when prop changes
        useEffect(() => {
            setLocalFilter(filterValue);
        }, [filterValue]);

        const hasFilter = filterValue.trim().length > 0;

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
                    ref={searchInputRef}
                    placeholder={placeholder}
                    leftSection={<IconSearch size={16}/>}
                    value={localSearch}
                    onChange={(e) => handleSearchChange(e.target.value)}
                    style={{flex: 1}}
                    rightSection={
                        localSearch ? (
                            <ActionIcon
                                size="sm"
                                variant="subtle"
                                onClick={() => handleSearchChange("")}
                            >
                                <IconX size={12}/>
                            </ActionIcon>
                        ) : null
                    }
                />

                {filterMode !== 'none' && (
                    <Group gap="xs">
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
                                        Filters
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
    });
