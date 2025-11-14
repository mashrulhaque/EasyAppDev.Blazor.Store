// Blazor Store Diagnostics Panel JavaScript

export function initializeDiagnosticsPanel(panelId) {
    const panel = document.getElementById(panelId);
    if (!panel || !panel.classList.contains('floating')) {
        return;
    }

    const header = panel.querySelector('.diagnostics-header');
    if (!header) {
        return;
    }

    let isDragging = false;
    let currentX;
    let currentY;
    let initialX;
    let initialY;
    let xOffset = 0;
    let yOffset = 0;

    // Get initial position from current right/bottom styles
    const computedStyle = window.getComputedStyle(panel);
    const right = parseInt(computedStyle.right) || 20;
    const bottom = parseInt(computedStyle.bottom) || 20;

    // Convert to left/top for easier dragging
    panel.style.left = `${window.innerWidth - panel.offsetWidth - right}px`;
    panel.style.top = `${window.innerHeight - panel.offsetHeight - bottom}px`;
    panel.style.right = 'auto';
    panel.style.bottom = 'auto';

    xOffset = parseInt(panel.style.left);
    yOffset = parseInt(panel.style.top);

    header.addEventListener('mousedown', dragStart);
    document.addEventListener('mousemove', drag);
    document.addEventListener('mouseup', dragEnd);

    // Touch events for mobile
    header.addEventListener('touchstart', dragStart);
    document.addEventListener('touchmove', drag);
    document.addEventListener('touchend', dragEnd);

    function dragStart(e) {
        if (e.type === 'touchstart') {
            initialX = e.touches[0].clientX - xOffset;
            initialY = e.touches[0].clientY - yOffset;
        } else {
            initialX = e.clientX - xOffset;
            initialY = e.clientY - yOffset;
        }

        if (e.target === header || header.contains(e.target)) {
            isDragging = true;
        }
    }

    function drag(e) {
        if (isDragging) {
            e.preventDefault();

            if (e.type === 'touchmove') {
                currentX = e.touches[0].clientX - initialX;
                currentY = e.touches[0].clientY - initialY;
            } else {
                currentX = e.clientX - initialX;
                currentY = e.clientY - initialY;
            }

            xOffset = currentX;
            yOffset = currentY;

            // Keep panel within viewport bounds
            const maxX = window.innerWidth - panel.offsetWidth;
            const maxY = window.innerHeight - panel.offsetHeight;

            xOffset = Math.max(0, Math.min(xOffset, maxX));
            yOffset = Math.max(0, Math.min(yOffset, maxY));

            setTranslate(xOffset, yOffset, panel);
        }
    }

    function dragEnd(e) {
        initialX = currentX;
        initialY = currentY;
        isDragging = false;
    }

    function setTranslate(xPos, yPos, el) {
        el.style.left = `${xPos}px`;
        el.style.top = `${yPos}px`;
    }
}

export function toggleCollapse(panelId) {
    const panel = document.getElementById(panelId);
    if (!panel) {
        return;
    }

    const content = panel.querySelector('.diagnostics-content');
    if (!content) {
        return;
    }

    content.classList.toggle('collapsed');

    return !content.classList.contains('collapsed');
}

export function disposePanel(panelId) {
    // Cleanup if needed
    const panel = document.getElementById(panelId);
    if (panel) {
        const header = panel.querySelector('.diagnostics-header');
        if (header) {
            // Remove event listeners by cloning and replacing
            const newHeader = header.cloneNode(true);
            header.parentNode.replaceChild(newHeader, header);
        }
    }
}
