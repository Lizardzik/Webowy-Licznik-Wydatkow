document.addEventListener('DOMContentLoaded', function () {
    const LOADER_CONFIG = {
        minimumDurationMs: 1000,
        maximumDurationMs: 4500,
        overlayFadeMs: 320,
        exitFadeMs: 160
    };

    const pageLoadStartTime = window.__pageLoadStartTime || performance.now();

    if ('serviceWorker' in navigator) {
        navigator.serviceWorker.register('/service-worker.js').catch(function () {
        });
    }

    const sidebarElement = document.getElementById('default-sidebar');
    const pageWrapper = document.querySelector('.page-wrapper');
    const toggleBtn = document.getElementById('toggle-btn');
    const mobileMenuBtn = document.getElementById('mobile-menu-btn');
    const mobileBackdrop = document.getElementById('mobile-sidebar-backdrop');
    const loadingOverlay = document.getElementById('loading-overlay');
    const pageContentShell = document.getElementById('page-content-shell');
    const dashboardContent = document.getElementById('dashboard-content');
    let lastIsMobile = window.innerWidth <= 768;
    let isContentRevealed = false;

    function preserveNavbarAvatar() {
        const avatarStorageKey = 'expenseTracker_profileAvatar';
        const profileImage = document.getElementById('profileImage');
        if (!profileImage) {
            return;
        }

        const fallbackSrc = profileImage.getAttribute('data-fallback-src') || '';
        const currentSrc = profileImage.getAttribute('src') || '';
        const cachedAvatar = sessionStorage.getItem(avatarStorageKey);

        if ((!currentSrc || currentSrc === '#') && cachedAvatar) {
            profileImage.src = cachedAvatar;
        }

        profileImage.style.display = 'block';

        function syncAvatarCache() {
            const resolvedSrc = profileImage.getAttribute('src') || '';
            if (!resolvedSrc || resolvedSrc === '#') {
                return;
            }
            if (profileImage.complete && profileImage.naturalWidth > 0) {
                try {
                    if (resolvedSrc !== fallbackSrc) {
                        sessionStorage.setItem(avatarStorageKey, resolvedSrc);
                    } else {
                        sessionStorage.removeItem(avatarStorageKey);
                    }
                } catch (e) {
                }
            }
        }

        profileImage.addEventListener('load', syncAvatarCache);
        syncAvatarCache();
    }

    function isMobileViewport() {
        return window.innerWidth <= 768;
    }

    function clearPageWrapperInlineStyles() {
        if (!pageWrapper) {
            return;
        }
        pageWrapper.style.marginLeft = '';
        pageWrapper.style.width = '';
        pageWrapper.style.transform = '';
    }

    function getSidebarInstance() {
        if (!sidebarElement || !sidebarElement.ej2_instances) {
            return null;
        }
        return sidebarElement.ej2_instances[0];
    }

    function refreshChartsLayout() {
        setTimeout(function () {
            window.dispatchEvent(new Event('resize'));
        }, 370);
    }

    function revealPageContent() {
        if (isContentRevealed) {
            return;
        }
        isContentRevealed = true;

        clearPageWrapperInlineStyles();

        if (dashboardContent) {
            dashboardContent.style.display = 'block';
        }

        document.body.classList.remove('page-is-loading');

        if (pageContentShell) {
            pageContentShell.classList.remove('is-loading');
            pageContentShell.classList.remove('is-entering');
        }

        window.dispatchEvent(new Event('resize'));

        if (!loadingOverlay) {
            return;
        }

        loadingOverlay.classList.add('hidden');
        setTimeout(function () {
            loadingOverlay.style.display = 'none';
        }, LOADER_CONFIG.overlayFadeMs);
    }

    function schedulePageReveal() {
        if (!loadingOverlay) {
            requestAnimationFrame(function () {
                requestAnimationFrame(revealPageContent);
            });
            return;
        }

        const elapsedMs = performance.now() - pageLoadStartTime;
        const remainingDelayMs = Math.max(0, LOADER_CONFIG.minimumDurationMs - elapsedMs);

        setTimeout(function () {
            revealPageContent();
            refreshChartsLayout();
        }, remainingDelayMs);
    }

    function shouldAnimateNavigation(anchor, event) {
        if (!anchor || event.defaultPrevented || event.button !== 0) {
            return false;
        }
        if (event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) {
            return false;
        }
        if (anchor.hasAttribute('download') || anchor.getAttribute('target') === '_blank') {
            return false;
        }
        if (anchor.hasAttribute('data-bs-toggle') || anchor.classList.contains('sidebar-parent-toggle')) {
            return false;
        }
        const rawHref = anchor.getAttribute('href');
        if (!rawHref || rawHref.startsWith('#') || rawHref.startsWith('javascript:')) {
            return false;
        }
        try {
            const targetUrl = new URL(anchor.href, window.location.origin);
            if (targetUrl.origin !== window.location.origin) {
                return false;
            }
            if (targetUrl.pathname === window.location.pathname && targetUrl.search === window.location.search && targetUrl.hash) {
                return false;
            }
            return true;
        } catch (e) {
            return false;
        }
    }

    function bindSmoothNavigation() {
        document.addEventListener('click', function (event) {
            const anchor = event.target.closest('a');
            if (!shouldAnimateNavigation(anchor, event)) {
                return;
            }
            event.preventDefault();
            const destinationUrl = anchor.href;
            document.body.classList.add('page-is-leaving');
            if (pageContentShell) {
                pageContentShell.classList.add('is-leaving');
            }
            setTimeout(function () {
                window.location.href = destinationUrl;
            }, LOADER_CONFIG.exitFadeMs);
        });

        window.addEventListener('pageshow', function (event) {
            if (event.persisted) {
                document.body.classList.remove('page-is-leaving');
                document.body.classList.remove('page-is-loading');
                if (pageContentShell) {
                    pageContentShell.classList.remove('is-leaving');
                    pageContentShell.classList.remove('is-entering');
                    pageContentShell.classList.remove('is-loading');
                }
            }
        });
    }

    function syncDesktopSidebarState(isOpen) {
        document.body.classList.toggle('sidebar-collapsed', !isOpen);
        clearPageWrapperInlineStyles();
        if (!isOpen) {
            document.querySelectorAll('.sidebar-submenu.show').forEach(function (submenu) {
                submenu.classList.remove('show');
            });
            document.querySelectorAll('.sidebar-parent-toggle').forEach(function (btn) {
                btn.setAttribute('aria-expanded', 'false');
            });
        }
        refreshChartsLayout();
    }

    if (toggleBtn) {
        toggleBtn.addEventListener('click', function () {
            if (isMobileViewport()) {
                closeMobileDrawer();
                return;
            }
            const sidebar = getSidebarInstance();
            if (!sidebar) {
                return;
            }
            const nextIsOpen = !sidebar.isOpen;
            if (nextIsOpen) {
                sidebar.show();
            } else {
                sidebar.hide();
            }
            syncDesktopSidebarState(nextIsOpen);
        });
    }

    document.querySelectorAll('.sidebar-parent-toggle').forEach(function (parentLink) {
        parentLink.addEventListener('click', function (e) {
            e.preventDefault();
            if (!isMobileViewport() && document.body.classList.contains('sidebar-collapsed')) {
                const sidebar = getSidebarInstance();
                if (sidebar) {
                    sidebar.show();
                    syncDesktopSidebarState(true);
                }
            }
            const targetSelector = this.getAttribute('data-target');
            const targetMenu = targetSelector ? document.querySelector(targetSelector) : null;
            if (targetMenu) {
                const isExpanded = targetMenu.classList.toggle('show');
                this.setAttribute('aria-expanded', isExpanded ? 'true' : 'false');
            }
        });
    });

    function setMobileDrawerUiState(isOpen) {
        if (mobileBackdrop) {
            mobileBackdrop.classList.toggle('active', isOpen);
        }
        if (mobileMenuBtn) {
            mobileMenuBtn.classList.toggle('is-active', isOpen);
        }
    }

    function closeMobileDrawer() {
        const sidebar = getSidebarInstance();
        if (sidebar && sidebar.isOpen) {
            sidebar.hide();
        }
        setMobileDrawerUiState(false);
    }

    function openMobileDrawer() {
        const sidebar = getSidebarInstance();
        if (!sidebar) {
            return;
        }
        sidebar.show();
        setMobileDrawerUiState(true);
    }

    function applyViewportSidebarMode() {
        const sidebar = getSidebarInstance();
        if (!sidebar) {
            return false;
        }

        if (isMobileViewport()) {
            document.body.classList.remove('sidebar-collapsed');
            sidebar.hide();
            setMobileDrawerUiState(false);
        } else {
            setMobileDrawerUiState(false);
            sidebar.show();
            syncDesktopSidebarState(true);
        }

        clearPageWrapperInlineStyles();
        return true;
    }

    function enableSidebarTransitions() {
        requestAnimationFrame(function () {
            requestAnimationFrame(function () {
                document.body.classList.add('sidebar-transition-ready');
            });
        });
    }

    if (mobileMenuBtn) {
        mobileMenuBtn.addEventListener('click', function (e) {
            e.stopPropagation();
            const sidebar = getSidebarInstance();
            if (!sidebar) {
                return;
            }
            if (sidebar.isOpen) {
                closeMobileDrawer();
            } else {
                openMobileDrawer();
            }
        });
    }

    if (mobileBackdrop) {
        mobileBackdrop.addEventListener('click', closeMobileDrawer);
    }

    document.querySelectorAll('.sidebar-link:not(.sidebar-parent-toggle), .sidebar-sublink').forEach(function (link) {
        link.addEventListener('click', function () {
            if (isMobileViewport()) {
                closeMobileDrawer();
            }
        });
    });

    window.addEventListener('resize', function () {
        const currentIsMobile = isMobileViewport();
        if (currentIsMobile !== lastIsMobile) {
            lastIsMobile = currentIsMobile;
            applyViewportSidebarMode();
        }
    });

    preserveNavbarAvatar();

    let initAttempts = 0;
    const initTimer = setInterval(function () {
        initAttempts += 1;
        clearPageWrapperInlineStyles();
        if (applyViewportSidebarMode() || initAttempts >= 20) {
            clearInterval(initTimer);
            clearPageWrapperInlineStyles();
            enableSidebarTransitions();
        }
    }, 25);

    bindSmoothNavigation();

    if (document.readyState === 'complete') {
        schedulePageReveal();
    } else {
        window.addEventListener('load', schedulePageReveal);
        setTimeout(revealPageContent, LOADER_CONFIG.maximumDurationMs);
    }
});