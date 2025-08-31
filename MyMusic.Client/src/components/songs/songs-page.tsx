import Collection from "../common/collection.tsx";
import {Anchor} from "@mantine/core";
import {useListSongs} from '../../client/songs.ts';
import {useEffect} from "react";
import Artwork from "../common/artwork.tsx";
import {IconMusic} from "@tabler/icons-react";


// const elements = [
//     {
//         id: 0,
//         cover: 'https://lh3.googleusercontent.com/adj3kac3mkCVe-XlLe21050fc39vdSjhOvTFau7Nq_jQ91A0UtiUAnRPF08ivJ4hfBcvjfPm_ke3S9yjAg=w60-h60-l90-rj',
//         title: 'The Sofa',
//         artists: ['Wolf Alice'],
//         album: 'The Clearing',
//         genres: ['Carbon'],
//         year: 2025,
//         rating: 4.5,
//         duration: '3:20'
//     },
//     {
//         id: 1,
//         cover: 'https://lh3.googleusercontent.com/2SUtEUUM8fqH68RHp8qHM9BGf3daK3hjV26-FNFqHP5iy4R-5mn318ZlP1qB_ZVC6RZMmfBGzpxI2Wg=w60-h60-l90-rj',
//         title: 'us.',
//         artists: ['Gracie Abrams', 'Taylor Swift'],
//         album: 'The Secret of Us',
//         genres: ['Carbon'],
//         year: 2025,
//         rating: 4.5,
//         duration: '2:30'
//     },
// ];

export default function SongsPage() {
    const {data: songs, refetch} = useListSongs();

    const elements = songs?.data?.songs ?? [];

    useEffect(() => {
        // noinspection JSIgnoredPromiseFromCall
        refetch()
    }, [refetch]);

    return <>
        <Collection
            items={elements}
            columnHeaders={['', 'Title', 'Artists', 'Album', 'Genres', 'Year', 'Duration']}
            columnCells={[
                row => <Artwork id={row.cover} size={32} placeholderIcon={<IconMusic/>}/>,
                row => <Anchor>{row.title}</Anchor>,
                row => row.artists.map(((artist, i) => <>
                    {i > 0 && ', '}
                    <Anchor>{artist.name}</Anchor>
                </>)),
                row => <Anchor>{row.album.name}</Anchor>,
                row => row.genres.map(((genre, i) => <>
                    {i > 0 && ', '}
                    <Anchor>{genre.name}</Anchor>
                </>)),
                row => row.year,
                row => row.duration,
            ]}>
        </Collection>
    </>;
}
