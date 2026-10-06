import {ActionIcon, Button, Group, Popover, Stack, Text, TextInput, Tooltip} from "@mantine/core";
import {useElementSize} from "@mantine/hooks";
import {IconCode, IconFilter, IconSearch, IconX} from "@tabler/icons-react";
import {forwardRef, useEffect, useId, useImperativeHandle, useRef, useState} from "react";
import {useTranslation} from "react-i18next";
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
    /** Extra content shown inside the search input, before the clear button */
    searchRightSection?: React.ReactNode;
}

export const CollectionFilterBar = forwardRef<CollectionFilterBarRef, CollectionFilterBarProps>(
    function CollectionFilterBar({
                                      searchValue,
                                      onSearchChange,
                                      filterValue,
                                      onFilterChange,
                                      onApply,
                                      placeholder,
                                      filterMode,
                                      filterMetadata,
                                      fetchFilterValues,
                                      searchRightSection,
                                  }, ref) {
        const {t} = useTranslation(["collection", "common"]);
        const searchInputRef = useRef<HTMLInputElement>(null);
        // The input reserves the measured width, so typed text never runs under the custom content
        const {ref: rightSectionRef, width: rightSectionWidth} = useElementSize<HTMLDivElement>();

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
        // Ties the toggle to its popover, which is rendered in a portal, away from the filter bar
        const popoverId = useId();

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

        const clearSearchButton = localSearch ? (
            <ActionIcon
                size="sm"
                variant="subtle"
                onClick={() => handleSearchChange("")}
            >
                <IconX size={12}/>
            </ActionIcon>
        ) : null;

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
                    data-testid="collection-search"
                    placeholder={placeholder ?? t("collection:filterBar.searchPlaceholder")}
                    leftSection={<IconSearch size={16}/>}
                    value={localSearch}
                    onChange={(e) => handleSearchChange(e.target.value)}
                    style={{flex: 1}}
                    rightSection={
                        searchRightSection ? (
                            <Group ref={rightSectionRef} gap={4} wrap="nowrap" px={6}>
                                {searchRightSection}
                                {clearSearchButton}
                            </Group>
                        ) : clearSearchButton
                    }
                    rightSectionWidth={searchRightSection && rightSectionWidth > 0 ? rightSectionWidth + 12 : undefined}
                    rightSectionPointerEvents={searchRightSection ? "all" : undefined}
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
                                <Tooltip label={showAdvanced ? t("collection:filterBar.hideAdvanced") : t("collection:filterBar.showAdvanced")}>
                                    <Button
                                        data-testid="collection-filter-toggle"
                                        data-popover-id={popoverId}
                                        data-filter={filterValue}
                                        aria-expanded={showAdvanced}
                                        variant={hasFilter ? "light" : "subtle"}
                                        leftSection={hasFilter ? <IconFilter size={16}/> : <IconCode size={16}/>}
                                        onClick={() => setShowAdvanced(!showAdvanced)}
                                        color={hasFilter ? "blue" : "gray"}
                                        size="sm"
                                    >
                                        {t("collection:filterBar.filters")}
                                    </Button>
                                </Tooltip>
                            </Popover.Target>

                            <Popover.Dropdown data-testid="collection-filter-popover" data-popover-id={popoverId}>
                                <Stack gap="xs">
                                    <Group justify="space-between" align="center">
                                        <Text size="sm" fw={500} c="dimmed">
                                            {t("collection:filterBar.filterDsl")}
                                        </Text>
                                        <Text size="xs" c="dimmed">
                                            {t("collection:filterBar.pressCtrlEnter")}
                                        </Text>
                                    </Group>

                                    <FilterCodeEditor
                                        value={localFilter}
                                        onChange={setLocalFilter}
                                        onApply={handleApply}
                                        height={100}
                                        metadata={filterMetadata}
                                        fetchFilterValues={fetchFilterValues}
                                        scopes={filterMode === 'server'}
                                    />

                                    <Text size="xs" c="dimmed">
                                        {t("collection:filterBar.examples")}: <code>year &gt;= 2020</code>, <code>name contains "love"</code>
                                    </Text>

                                    <Group justify="space-between" align="center">
                                        <Button
                                            size="xs"
                                            variant="subtle"
                                            onClick={handleClearFilter}
                                            disabled={!hasFilter}
                                        >
                                            {t("collection:filterBar.clear")}
                                        </Button>
                                        <Button
                                            data-testid="collection-filter-apply"
                                            size="xs"
                                            onClick={handleApply}
                                            disabled={localFilter === filterValue}
                                        >
                                            {t("common:actions.apply")}
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
