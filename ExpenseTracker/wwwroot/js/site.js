document.addEventListener('DOMContentLoaded', function () {
    const sidebarElement = document.getElementById('default-sidebar');
    const toggleBtn = document.getElementById('toggle-btn');
    const mobileMenuBtn = document.getElementById('mobile-menu-btn');

    function syncSidebarState(isOpen) {
        document.body.classList.toggle('sidebar-collapsed', !isOpen);
        if (!isOpen) {
            document.querySelectorAll('.sidebar-submenu.show').forEach(function (submenu) {
                submenu.classList.remove('show');
            });
            document.querySelectorAll('.sidebar-parent-toggle').forEach(function (btn) {
                btn.setAttribute('aria-expanded', 'false');
            });
        }
    }

    function toggleSidebar() {
        if (!sidebarElement || !sidebarElement.ej2_instances) return;
        const sidebar = sidebarElement.ej2_instances[0];
        sidebar.toggle();
        syncSidebarState(sidebar.isOpen);
    }

    if (toggleBtn) {
        toggleBtn.onclick = toggleSidebar;
    }

    if (mobileMenuBtn) {
        mobileMenuBtn.onclick = toggleSidebar;
    }

    document.querySelectorAll('.sidebar-parent-toggle').forEach(function (parentLink) {
        parentLink.addEventListener('click', function (e) {
            e.preventDefault();
            if (document.body.classList.contains('sidebar-collapsed') && sidebarElement && sidebarElement.ej2_instances) {
                const sidebar = sidebarElement.ej2_instances[0];
                sidebar.show();
                syncSidebarState(true);
            }
            const targetSelector = this.getAttribute('data-target');
            const targetMenu = targetSelector ? document.querySelector(targetSelector) : null;
            if (targetMenu) {
                const isExpanded = targetMenu.classList.toggle('show');
                this.setAttribute('aria-expanded', isExpanded ? 'true' : 'false');
            }
        });
    });

    const loadingOverlay = document.getElementById('loading-overlay');
    const dashboardContent = document.getElementById('dashboard-content');

    if (loadingOverlay) {
        setTimeout(function () {
            loadingOverlay.classList.add('hidden');
            if (dashboardContent) {
                dashboardContent.style.display = 'block';
            }
            setTimeout(function () {
                loadingOverlay.style.display = 'none';
            }, 300);
        }, 250);
    } else if (dashboardContent) {
        dashboardContent.style.display = 'block';
    }
});