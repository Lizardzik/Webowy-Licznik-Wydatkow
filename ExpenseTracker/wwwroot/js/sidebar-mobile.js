document.addEventListener('DOMContentLoaded', function () {
    const app = window.ExpenseTrackerApp;
    const sidebarElement = document.getElementById('default-sidebar');
    const mobileMenuBtn = document.getElementById('mobile-menu-btn');
    const mobileBackdrop = document.getElementById('mobile-sidebar-backdrop');
    const toggleBtn = document.getElementById('toggle-btn');

    if (!sidebarElement) {
        return;
    }

    function setMobileMenuOpen(isOpen) {
        document.body.classList.toggle('mobile-menu-open', isOpen);
        if (mobileBackdrop) {
            mobileBackdrop.classList.toggle('active', isOpen);
        }
        if (mobileMenuBtn) {
            mobileMenuBtn.classList.toggle('is-active', isOpen);
            mobileMenuBtn.setAttribute('aria-expanded', isOpen ? 'true' : 'false');
        }
    }

    function closeMobileMenu() {
        setMobileMenuOpen(false);
    }

    function toggleMobileMenu() {
        const isCurrentlyOpen = document.body.classList.contains('mobile-menu-open');
        setMobileMenuOpen(!isCurrentlyOpen);
    }

    if (mobileMenuBtn) {
        mobileMenuBtn.addEventListener('click', function (event) {
            event.preventDefault();
            event.stopPropagation();
            if (!app.isMobileViewport()) {
                return;
            }
            toggleMobileMenu();
        });
    }

    if (toggleBtn) {
        toggleBtn.addEventListener('click', function (event) {
            if (!app.isMobileViewport()) {
                return;
            }
            event.preventDefault();
            event.stopPropagation();
            closeMobileMenu();
        });
    }

    if (mobileBackdrop) {
        mobileBackdrop.addEventListener('click', function () {
            closeMobileMenu();
        });
    }

    document.querySelectorAll('#default-sidebar .sidebar-link:not(.sidebar-parent-toggle), #default-sidebar .sidebar-sublink').forEach(function (link) {
        link.addEventListener('click', function () {
            if (app.isMobileViewport()) {
                closeMobileMenu();
            }
        });
    });

    document.addEventListener('keydown', function (event) {
        if (event.key === 'Escape' && document.body.classList.contains('mobile-menu-open')) {
            closeMobileMenu();
        }
    });

    window.addEventListener('resize', function () {
        if (!app.isMobileViewport() && document.body.classList.contains('mobile-menu-open')) {
            closeMobileMenu();
        }
    });

    if (app.isMobileViewport()) {
        closeMobileMenu();
    }
});