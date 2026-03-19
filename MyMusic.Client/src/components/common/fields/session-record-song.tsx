import {Flex, Text} from "@mantine/core";
import {Link} from "@tanstack/react-router";
import Artwork from "../artwork.tsx";
import type {SyncRecordSongInfo} from "../../../model";

interface SessionRecordSongProps {
    songInfo?: SyncRecordSongInfo | null;
}

export default function SessionRecordSong({songInfo}: SessionRecordSongProps) {
    if (!songInfo) {
        return <Text c="dimmed">-</Text>;
    }

    return (
        <Link to={`/songs/$songId`} params={{songId: String(songInfo.id)}} style={{textDecoration: 'none'}}>
            <Flex gap="sm" align="center">
                <Artwork 
                    id={songInfo.coverId ? parseInt(songInfo.coverId, 10) : null} 
                    size={40}
                />
                <div>
                    <Text fw={500} lineClamp={1}>
                        {songInfo.title}
                    </Text>
                    <Text size="sm" c="dimmed" lineClamp={1}>
                        {songInfo.artistNames}
                    </Text>
                </div>
            </Flex>
        </Link>
    );
}
