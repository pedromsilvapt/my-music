export function cls(...classes: (string | boolean | null | undefined)[]) {
    return classes.filter(x => typeof x === 'string' && x != '').join(' ');
}