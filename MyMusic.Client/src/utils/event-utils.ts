export function isInteractiveElement(target: EventTarget | null): boolean {
    // SVG elements are instances of Element, not HTMLElement
    if (!(target instanceof Element)) {
        return false;
    }

    // Check if the target or any of its ancestors is an interactive element
    const interactable = target.closest(
        'button, a, [role="button"], [role="menuitem"], .mantine-ActionIcon-root, .mantine-Menu-item, input, select, textarea, svg'
    );

    if (interactable == null) {
        return false;
    }

    // Exclude sortable collection items - they have role="button" from dnd-kit
    // but we still want to allow selection on them
    return !interactable.hasAttribute('data-sortable-item');
}
