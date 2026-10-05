import {ActionIcon} from "@mantine/core";
import {useDisclosure} from "@mantine/hooks";
import {IconPlus} from "@tabler/icons-react";
import {useState} from "react";
import {useTranslation} from "react-i18next";
import {useListArtists} from "../../client/artists.ts";
import {useQueryData} from "../../hooks/use-query-data.ts";
import Collection from "../common/collection/collection.tsx";
import CollectionToolbar from "../common/collection/collection-toolbar.tsx";
import CreateArtistModal from "./create-artist-modal.tsx";
import {useArtistsSchema} from "./useArtistsSchema.tsx";

export default function ArtistsPage() {
    const {t} = useTranslation(["artists", "common"]);
    const [appliedSearch, setAppliedSearch] = useState("");
    const [appliedFilter, setAppliedFilter] = useState("");

    const artistsQuery = useListArtists(
        {search: appliedSearch, filter: appliedFilter},
        {query: {select: response => response.data}},
    );

    const artists = useQueryData(artistsQuery, t("artists:page.fetchFailed")) ?? {artists: []};

    const artistsSchema = useArtistsSchema();
    const [createOpened, {open: openCreate, close: closeCreate}] = useDisclosure(false);

    const handleFilterChange = (newSearch: string, newFilter: string) => {
        setAppliedSearch(newSearch);
        setAppliedFilter(newFilter);
    };

    const elements = artists?.artists ?? [];

    return (
        <div style={{height: 'var(--parent-height)'}} data-testid="artists">
            <Collection
                key="artists"
                stateKey="artists"
                items={elements}
                schema={artistsSchema}
                isFetching={artistsQuery.isFetching}
                filterMode="server"
                serverSearch={appliedSearch}
                serverFilter={appliedFilter}
                onServerFilterChange={handleFilterChange}
                searchPlaceholder={t("artists:page.searchPlaceholder")}
                toolbar={p => (
                    <CollectionToolbar
                        {...p}
                        renderExtraActions={() => (
                            <ActionIcon
                                variant="default"
                                size="lg"
                                aria-label={t("artists:page.createArtist")}
                                title={t("artists:page.createArtist")}
                                onClick={openCreate}
                                data-testid="create-artist"
                            >
                                <IconPlus/>
                            </ActionIcon>
                        )}
                    />
                )}
            />
            <CreateArtistModal opened={createOpened} onClose={closeCreate}/>
        </div>
    );
}
