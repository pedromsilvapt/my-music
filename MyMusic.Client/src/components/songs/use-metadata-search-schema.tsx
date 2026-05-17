import {IconCheck, IconMusic} from "@tabler/icons-react";
import {useMemo} from "react";
import type {SearchMetadataResult} from "../../model";
import Artwork from "../common/artwork";
import type {CollectionSchema} from "../common/collection/collection";
import ExplicitLabel from "../common/explicit-label";
import {sepBy} from "../common/sep-by";

export type MetadataSearchItem = SearchMetadataResult & { id: string };

export function useMetadataSearchSchema(
    onApply: (result: MetadataSearchItem) => void,
): CollectionSchema<MetadataSearchItem> {
    return useMemo(() => ({
        key: (item: MetadataSearchItem) => item.id,
        searchVector: (item: MetadataSearchItem) =>
            `${item.song.title} ${item.song.artists.map(a => a.name).join(" ")} ${item.song.album?.name ?? ""}`,
        estimateTableRowHeight: () => 60,
        estimateListRowHeight: () => 84,
        renderListArtwork: (item: MetadataSearchItem, size: number) => (
            <Artwork
                url={item.song.cover?.normal ?? item.song.cover?.small ?? null}
                size={size}
                placeholderIcon={<IconMusic/>}
            />
        ),
        renderListTitle: (item: MetadataSearchItem, lineClamp: number) => (
            <ExplicitLabel visible={item.song.explicit ?? false}>
                {item.song.title}
            </ExplicitLabel>
        ),
        renderListSubTitle: (item: MetadataSearchItem) => {
            const segments: React.ReactNode[] = [];

            if (item.song.artists.length > 0) {
                segments.push(item.song.artists.map(a => a.name).join(", "));
            }

            if (item.song.album?.name) {
                segments.push(item.song.album.name);
            }

            if (item.song.year) {
                segments.push(String(item.song.year));
            }

            segments.push(`Source: ${item.sourceName}`);

            return <>{sepBy(segments, " • ")}</>;
        },
        columns: [
            {
                name: "artwork",
                displayName: "",
                render: (item: MetadataSearchItem) => (
                    <Artwork
                        url={item.song.cover?.normal ?? item.song.cover?.small ?? null}
                        size={40}
                        placeholderIcon={<IconMusic/>}
                    />
                ),
                width: 52,
            },
            {
                name: "title",
                displayName: "Title",
                render: (item: MetadataSearchItem) => item.song.title,
                width: "2fr",
            },
            {
                name: "artists",
                displayName: "Artists",
                render: (item: MetadataSearchItem) => item.song.artists.map(a => a.name).join(", "),
                width: "1fr",
            },
            {
                name: "source",
                displayName: "Source",
                render: (item: MetadataSearchItem) => item.sourceName,
                width: "1fr",
            },
            {
                name: "actions",
                displayName: "",
                render: () => null,
                width: 52,
            },
        ],
        actions: () => [
            {
                name: "apply",
                primary: true,
                renderIcon: () => <IconCheck size={16}/>,
                renderLabel: () => "Apply",
                onClick: (items: MetadataSearchItem[]) => {
                    if (items.length > 0) {
                        onApply(items[0]!);
                    }
                },
            },
        ],
    }), [onApply]);
}