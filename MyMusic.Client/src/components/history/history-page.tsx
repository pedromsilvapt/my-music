import {Group} from "@mantine/core";
import {DateInput} from "@mantine/dates";
import {useMemo, useState} from "react";
import {useListPlayHistory} from "../../client/play-history.ts";
import {useQueryData} from "../../hooks/use-query-data.ts";
import CollectionToolbar from "../common/collection/collection-toolbar.tsx";
import Collection from "../common/collection/collection.tsx";
import {type AutocompleteItem} from "../songs/autocomplete-field.tsx";
import SongAutocompleteField from "../songs/song-autocomplete-field.tsx";
import {usePlayHistorySchema} from "./usePlayHistorySchema.tsx";

const HISTORY_LIMIT = 100;

export default function HistoryPage() {
    const [selectedSong, setSelectedSong] = useState<AutocompleteItem | null>(null);
    const [startDate, setStartDate] = useState<Date | null>(null);
    const [endDate, setEndDate] = useState<Date | null>(null);

    const queryParams = useMemo(() => {
        const params: {
            limit: number;
            songId?: number;
            startDate?: string;
            endDate?: string;
        } = {limit: HISTORY_LIMIT};

        if (selectedSong?.id) {
            params.songId = selectedSong.id;
        }
        if (startDate) {
            params.startDate = startDate.toISOString();
        }
        if (endDate) {
            params.endDate = endDate.toISOString();
        }

        return params;
    }, [selectedSong, startDate, endDate]);

    const playHistoryQuery = useListPlayHistory(queryParams);

    const playHistoryResponse = useQueryData(
        playHistoryQuery,
        "Failed to fetch play history"
    ) ?? {data: {items: [], total: 0}};

    const historySchema = usePlayHistorySchema();

    const handleStartDateChange = (value: string | Date | null) => {
        if (value === null) {
            setStartDate(null);
        } else if (value instanceof Date) {
            setStartDate(value);
        } else {
            const parsed = new Date(value);
            setStartDate(isNaN(parsed.getTime()) ? null : parsed);
        }
    };

    const handleEndDateChange = (value: string | Date | null) => {
        if (value === null) {
            setEndDate(null);
        } else if (value instanceof Date) {
            const endOfDay = new Date(value);
            endOfDay.setHours(23, 59, 59, 999);
            setEndDate(endOfDay);
        } else {
            const parsed = new Date(value);
            if (!isNaN(parsed.getTime())) {
                parsed.setHours(23, 59, 59, 999);
            }
            setEndDate(isNaN(parsed.getTime()) ? null : parsed);
        }
    };

    const historyItems = playHistoryResponse?.data?.items ?? [];

    return (
        <div style={{height: 'var(--parent-height)'}}>
            <Collection
                key="history"
                items={historyItems}
                schema={historySchema}
                filterMode="none"
                isFetching={playHistoryQuery.isLoading}
                toolbar={(props) => (
                    <CollectionToolbar
                        {...props}
                        renderMiddleSection={() => (
                            <Group gap="md" wrap="nowrap">
                                <SongAutocompleteField
                                    value={selectedSong}
                                    onChange={setSelectedSong}
                                />
                                <DateInput
                                    label="Start Date"
                                    placeholder="Start date"
                                    value={startDate}
                                    onChange={handleStartDateChange}
                                    clearable
                                />
                                <DateInput
                                    label="End Date"
                                    placeholder="End date"
                                    value={endDate}
                                    onChange={handleEndDateChange}
                                    clearable
                                />
                            </Group>
                        )}
                    />
                )}
            />
        </div>
    );
}
