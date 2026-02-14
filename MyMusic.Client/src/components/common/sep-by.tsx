export function sepBy(elems: React.ReactNode[], sep: React.ReactNode) {
    if (elems.length < 2) {
        return elems;
    }

    return elems.flatMap((elem, i) => i == 0 ? [elem] : [sep, elem]);
}