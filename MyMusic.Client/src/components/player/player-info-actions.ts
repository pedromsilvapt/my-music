import type {CollectionSchemaAction} from '../common/collection/collection-schema.tsx';

/**
 * Song actions that make no sense for the currently playing song in the footer player.
 */
export const FOOTER_HIDDEN_ACTIONS = ['play', 'play-next', 'play-last', 'shuffle', 'remove-from-queue', 'skip-next-playback'];

/**
 * Song actions shown as buttons next to the footer player menu, instead of inside it.
 */
export const FOOTER_PRIMARY_ACTIONS = ['favorite', 'manage-playlists'];

/**
 * Adapts the song schema actions to the footer player: hides the actions that do not apply
 * to the currently playing song and promotes the most used ones to primary buttons.
 */
export function getFooterSongActions<M>(actions: CollectionSchemaAction<M>[]): CollectionSchemaAction<M>[] {
    return actions
        .filter(action => !('name' in action && FOOTER_HIDDEN_ACTIONS.includes(action.name)))
        .map(action => {
            if ('name' in action && FOOTER_PRIMARY_ACTIONS.includes(action.name)) {
                return {...action, primary: true};
            }
            return action;
        });
}
