import {ActionIcon, Button, Collapse, Group, Paper, Stack, Text, Tooltip} from "@mantine/core";
import {IconChevronDown, IconChevronUp, IconCode, IconFilter, IconX} from "@tabler/icons-react";
import {useState} from "react";
import {FilterCodeEditor} from "./filter-code-editor.tsx";
import {UnifiedSearch} from "./unified-search.tsx";

interface FilterBarProps {
    searchValue: string;
    onSearchChange: (value: string) => void;
    filterValue: string;
    onFilterChange: (value: string) => void;
    onApply: () => void;
}

export function FilterBar({searchValue, onSearchChange, filterValue, onFilterChange, onApply}: FilterBarProps) {
    const [showAdvanced, setShowAdvanced] = useState(false);
    const hasFilter = filterValue.trim().length > 0;

    const handleClearFilter = () => {
        onFilterChange("");
    };

    return (
        <Stack gap="xs">
            <Paper p="xs" radius="md" withBorder>
                <Group gap="sm" align="center" justify="space-between">
                    <UnifiedSearch
                        value={searchValue}
                        onChange={onSearchChange}
                    />

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

                        <Tooltip label={showAdvanced ? "Hide advanced filter" : "Show advanced filter"}>
                            <Button
                                variant={hasFilter ? "light" : "subtle"}
                                leftSection={hasFilter ? <IconFilter size={16}/> : <IconCode size={16}/>}
                                rightSection={showAdvanced ? <IconChevronUp size={14}/> : <IconChevronDown size={14}/>}
                                onClick={() => setShowAdvanced(!showAdvanced)}
                                color={hasFilter ? "blue" : "gray"}
                            >
                                Advanced
                                {hasFilter && (
                                    <Text size="xs" c="blue" ml={4}>
                                        (1)
                                    </Text>
                                )}
                            </Button>
                        </Tooltip>
                    </Group>
                </Group>
            </Paper>

            <Collapse in={showAdvanced}>
                <Paper p="sm" radius="md" withBorder>
                    <Stack gap="xs">
                        <Group justify="space-between" align="center">
                            <Text size="sm" fw={500} c="dimmed">
                                Filter DSL
                            </Text>
                            <Group gap="xs">
                                <Text size="xs" c="dimmed">
                                    Press Ctrl+Enter to apply
                                </Text>
                                <Button
                                    size="xs"
                                    onClick={onApply}
                                    disabled={!filterValue.trim() && !searchValue.trim()}
                                >
                                    Apply
                                </Button>
                            </Group>
                        </Group>

                        <FilterCodeEditor
                            value={filterValue}
                            onChange={onFilterChange}
                            onApply={onApply}
                            height={100}
                        />

                        <Text size="xs" c="dimmed">
                            Examples: <code>year &gt;= 2020</code>, <code>title contains "love"</code>,{" "}
                            <code>isFavorite = true and hasLyrics = true</code>
                        </Text>
                    </Stack>
                </Paper>
            </Collapse>
        </Stack>
    );
}
