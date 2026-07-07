// ShellDocs JS interop — mirrors the ShellUI pattern:
// window.ShellDocs monolith for classic consumption + ES module exports for dynamic import.
// Real implementation lands with the primitives that need JS (SearchDialog hotkey, scroll-spy, etc.)

window.ShellDocs = window.ShellDocs || {};

Object.assign(window.ShellDocs, {
    // Populated in Phase 1 branches. Kept here as the extension point.
});
