// Splitter functionality for ResizableSplitter component
window.PoTool = window.PoTool || {};

window.PoTool.Splitter = (function() {
    let currentDotNetRef = null;
    let currentContainer = null;
    let isResizing = false;
    
    function handleMouseMove(e) {
        if (!isResizing || !currentDotNetRef || !currentContainer) return;
        
        const rect = currentContainer.getBoundingClientRect();
        currentDotNetRef.invokeMethodAsync('UpdatePosition', e.clientX, rect.left, rect.width);
    }
    
    function handleTouchMove(e) {
        if (!isResizing || !currentDotNetRef || !currentContainer) return;
        if (e.touches.length === 0) return;
        
        const rect = currentContainer.getBoundingClientRect();
        const touch = e.touches[0];
        currentDotNetRef.invokeMethodAsync('UpdatePosition', touch.clientX, rect.left, rect.width);
        
        // Prevent scrolling while resizing
        e.preventDefault();
    }
    
    function handleEnd() {
        if (!isResizing) return;
        
        isResizing = false;
        if (currentDotNetRef) {
            currentDotNetRef.invokeMethodAsync('StopResize');
        }
        
        // Remove resizing class
        if (currentContainer) {
            currentContainer.classList.remove('resizing');
        }
        
        document.removeEventListener('mousemove', handleMouseMove);
        document.removeEventListener('mouseup', handleEnd);
        document.removeEventListener('touchmove', handleTouchMove);
        document.removeEventListener('touchend', handleEnd);
        document.removeEventListener('touchcancel', handleEnd);
    }
    
    return {
        initResize: function(dotNetRef) {
            currentDotNetRef = dotNetRef;
            
            // Find the container element
            currentContainer = document.querySelector('.resizable-splitter-container');
            if (!currentContainer) {
                console.error('Splitter container not found');
                return;
            }
            
            isResizing = true;
            
            // Add resizing class for styling
            currentContainer.classList.add('resizing');
            
            // Add event listeners
            document.addEventListener('mousemove', handleMouseMove);
            document.addEventListener('mouseup', handleEnd);
            document.addEventListener('touchmove', handleTouchMove, { passive: false });
            document.addEventListener('touchend', handleEnd);
            document.addEventListener('touchcancel', handleEnd);
        },
        
        cleanup: function() {
            handleEnd();
            currentDotNetRef = null;
            currentContainer = null;
        }
    };
})();
