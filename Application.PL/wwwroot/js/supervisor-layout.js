(() => {
    const toggleBtn = document.getElementById('sidebarToggle');
    const sidebar = document.getElementById('supervisorSidebar');
    const storageKey = 'supervisorSidebarCollapsed';

    if (!toggleBtn || !sidebar) return;

    const saved = localStorage.getItem(storageKey);
    if (saved === 'true') sidebar.classList.add('collapsed');

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

})();
