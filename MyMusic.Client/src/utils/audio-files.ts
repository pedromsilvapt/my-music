const ACCEPTED_AUDIO_TYPES = ['audio/mpeg', 'audio/mp4', 'audio/x-m4a', 'audio/m4a'];
const ACCEPTED_EXTENSIONS = ['.mp3', '.m4a'];

/** The audio files a dropzone accepts, by mime type. */
export const ACCEPTED_AUDIO_FILES = {
    'audio/mpeg': ['.mp3'],
    'audio/mp4': ['.m4a'],
    'audio/x-m4a': ['.m4a'],
};

/** Whether the file is a song file the server can import, going by its mime type or its extension. */
export function isAudioFile(file: File): boolean {
    if (ACCEPTED_AUDIO_TYPES.includes(file.type)) {
        return true;
    }
    const lowerName = file.name.toLowerCase();
    return ACCEPTED_EXTENSIONS.some(ext => lowerName.endsWith(ext));
}
