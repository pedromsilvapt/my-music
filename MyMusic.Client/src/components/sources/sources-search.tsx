import {useDebouncedValue} from "@mantine/hooks";
import {useQueryClient} from "@tanstack/react-query";
import {useCallback, useState} from "react";
import {getListPurchasesQueryKey, useCreatePurchase} from "../../client/purchases.ts";
import {useSearchSongs} from "../../client/sources.ts";
import {API_SEARCH_DEBOUNCE_MS} from "../../consts.ts";
import type {ListSourcesItem, SourceSong} from "../../model";
import Collection from "../common/collection/collection.tsx";
import SourcesSearchToolbar from "./sources-song-toolbar.tsx";
import {useSourceSongsSchema} from "./useSourceSongsSchema.tsx";

export default function SourcesSearch() {
    const queryClient = useQueryClient()

    const [search, setSearch] = useState('');
    const [filter, setFilter] = useState('');
    const [appliedFilter, setAppliedFilter] = useState('');
    const [source, setSource] = useState<ListSourcesItem | null | undefined>(null);
    const [debouncedSearch] = useDebouncedValue(search, API_SEARCH_DEBOUNCE_MS);

    const {data: data, isFetching} = useSearchSongs(source?.id ?? 0, debouncedSearch, {
        query: {
            placeholderData: prev => prev
        },
        fetch: {
            filter: appliedFilter
        }
    });

    const createPurchase = useCreatePurchase({
        mutation: {
            onSuccess: () => {
                queryClient.invalidateQueries({queryKey: getListPurchasesQueryKey()})
            }
        }
    });

    const onPurchase = useCallback(async (songs: SourceSong[]) => {
        await Promise.all(songs.map(s => createPurchase.mutate({
            songId: s.id,
            sourceId: source!.id
        })))
    }, [createPurchase, source]);

    const sourceSongsSchema = useSourceSongsSchema(onPurchase);

    const elements = data?.data ?? [];

    return <>
        <div style={{height: 'var(--parent-height)'}}>
            <Collection
                key="songs"
                items={elements}
                schema={sourceSongsSchema}
                isFetching={isFetching}
                toolbar={p => (
                    <SourcesSearchToolbar
                        {...p}
                        source={source}
                        setSource={setSource}
                        search={search}
                        setSearch={setSearch}
                        filter={filter}
                        setFilter={setFilter}
                        onApplyFilter={(filterValue) => {
                            setFilter(filterValue);
                            setAppliedFilter(filterValue);
                        }}
                    />
                )}
            >
            </Collection>
        </div>
    </>;
}