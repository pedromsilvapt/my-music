export type QueueContextType = 'album' | 'artist' | 'playlist' | 'songs';

export interface QueueContext {
    type: QueueContextType;
    albumName?: string;
    artistName?: string;
    playlistName?: string;
}

export function generateQueueName(context: QueueContext): string {
    const date = new Date();
    const formattedDate = date.toLocaleDateString('en-US', {
        day: '2-digit',
        month: 'short',
        year: 'numeric',
    });

    switch (context.type) {
        case 'album':
            return `${context.albumName ?? 'Album'} (${formattedDate})`;
        case 'artist':
            return `${context.artistName ?? 'Artist'} Songs (${formattedDate})`;
        case 'playlist':
            return `Playlist: ${context.playlistName ?? 'Untitled'} (${formattedDate})`;
        case 'songs':
        default:
            return `Queue (${formattedDate})`;
    }
}