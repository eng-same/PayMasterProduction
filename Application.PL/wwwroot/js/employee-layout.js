// Simple sidebar toggle with persistence
(() => {
    const toggleBtn = document.getElementById('sidebarToggle');
    const sidebar = document.getElementById('employeeSidebar');
    const storageKey = 'employeeSidebarCollapsed';

    if (!toggleBtn || !sidebar) return;

    // read saved state
    const saved = localStorage.getItem(storageKey);
    if (saved === 'true') sidebar.classList.add('collapsed');

    // Update ARIA
    const applyAria = () => {
        const isCollapsed = sidebar.classList.contains('collapsed');
        toggleBtn.setAttribute('aria-expanded', String(!isCollapsed));
        sidebar.setAttribute('aria-hidden', String(isCollapsed));
    };
    applyAria();

    toggleBtn.addEventListener('click', () => {
        sidebar.classList.toggle('collapsed');
        const isCollapsed = sidebar.classList.contains('collapsed');
        localStorage.setItem(storageKey, isCollapsed ? 'true' : 'false');
        applyAria();
    });

    // Optional: allow double-click on sidebar to toggle
    sidebar.addEventListener('dblclick', () => {
        sidebar.classList.toggle('collapsed');
        const isCollapsed = sidebar.classList.contains('collapsed');
        localStorage.setItem(storageKey, isCollapsed ? 'true' : 'false');
        applyAria();
    });
})();
