document.addEventListener('DOMContentLoaded', function () {
    const app = window.ExpenseTrackerApp;
    const sidebarElement = document.getElementById('default-sidebar');
    const pageWrapper = document.querySelector('.page-wrapper');
    const toggleBtn = document.getElementById('toggle-btn');

    if (!sidebarElement) {
        return;
    }

    function getSidebarInstance() {
        if (!sidebarElement.ej2_instances || !sidebarElement.ej2_instances.length) {
            return null;
        }
        return sidebarElement.ej2_instances[0];
    }

    function clearPageWrapperInlineStyles() {
        if (!pageWrapper) {
            return;
        }
        pageWrapper.style.marginLeft = '';
        pageWrapper.style.width = '';
        pageWrapper.style.transform = '';
    }

    function collapseAllSubmenus() {
        document.querySelectorAll('.sidebar-submenu.show').forEach(function (submenu) {
            submenu.classList.remove('show');
        });
        document.querySelectorAll('.sidebar-parent-toggle').forEach(function (btn) {
            btn.setAttribute('aria-expanded', 'false');
        });
    }

    function setDesktopCollapsed(isCollapsed) {
        if (app.isMobileViewport()) {
            return;
        }
        document.body.classList.toggle('sidebar-collapsed', isCollapsed);
        clearPageWrapperInlineStyles();
        if (isCollapsed) {
            collapseAllSubmenus();
        }
        app.refreshChartsLayout();
    }

    function initDesktopSidebar() {
        if (app.isMobileViewport()) {
            document.body.classList.remove('sidebar-collapsed');
            clearPageWrapperInlineStyles();
            return true;
        }

        const sidebar = getSidebarInstance();
        if (!sidebar) {
            return false;
        }

        sidebar.show();
        setDesktopCollapsed(false);
        return true;
    }

    if (toggleBtn) {
        toggleBtn.addEventListener('click', function () {
            if (app.isMobileViewport()) {
                return;
            }
            const nextCollapsed = !document.body.classList.contains('sidebar-collapsed');
            setDesktopCollapsed(nextCollapsed);
        });
    }

    document.querySelectorAll('.sidebar-parent-toggle').forEach(function (parentLink) {
        parentLink.addEventListener('click', function (event) {
            event.preventDefault();
            if (!app.isMobileViewport() && document.body.classList.contains('sidebar-collapsed')) {
                setDesktopCollapsed(false);
            }
            const targetSelector = this.getAttribute('data-target');
            const targetMenu = targetSelector ? document.querySelector(targetSelector) : null;
            if (targetMenu) {
                const isExpanded = targetMenu.classList.toggle('show');
                this.setAttribute('aria-expanded', isExpanded ? 'true' : 'false');
            }
        });
    });

    window.addEventListener('resize', function () {
        if (!app.isMobileViewport()) {
            clearPageWrapperInlineStyles();
        } else {
            document.body.classList.remove('sidebar-collapsed');
        }
    });

    let attempts = 0;
    const initTimer = setInterval(function () {
        attempts += 1;
        clearPageWrapperInlineStyles();
        if (initDesktopSidebar() || attempts >= 20) {
            clearInterval(initTimer);
            clearPageWrapperInlineStyles();
            requestAnimationFrame(function () {
                document.body.classList.add('sidebar-transition-ready');
            });
        }
    }, 25);
});