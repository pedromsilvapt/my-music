import type {CollectionSchemaAction} from "./collection-schema.tsx";

export function getCollectionActionKey<M>(action: CollectionSchemaAction<M>, index: number): string {
    if ('divider' in action) return `divider-${index}`;
    if ('group' in action) return `group-${action.group}`;
    return action.name;
}
