// Navigation helpers for PO Companion Blazor components
window.PoTool = window.PoTool || {};

window.PoTool.Navigation = {
    /**
     * Smoothly scrolls the element with the given ID into view.
     * @param {string} elementId - The element ID to scroll to.
     */
    scrollToElement: function (elementId) {
        var el = document.getElementById(elementId);
        if (el) {
            el.scrollIntoView({ behavior: 'smooth', block: 'start' });
        }
    }
};
