window.ExpenseTrackerApp = (function () {
    const MOBILE_BREAKPOINT_PX = 768;

    function isMobileViewport() {
        return window.innerWidth < MOBILE_BREAKPOINT_PX;
    }

    function refreshChartsLayout() {
        setTimeout(function () {
            window.dispatchEvent(new Event('resize'));
        }, 360);
    }

    return {
        isMobileViewport: isMobileViewport,
        refreshChartsLayout: refreshChartsLayout
    };
})();

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

    const loadingOverlay = document.getElementById('loading-overlay');
    const pageContentShell = document.getElementById('page-content-shell');
    const dashboardContent = document.getElementById('dashboard-content');
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

    function revealPageContent() {
        if (isContentRevealed) {
            return;
        }
        isContentRevealed = true;

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
            window.ExpenseTrackerApp.refreshChartsLayout();
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

    preserveNavbarAvatar();
    bindSmoothNavigation();

    if (document.readyState === 'complete') {
        schedulePageReveal();
    } else {
        window.addEventListener('load', schedulePageReveal);
        setTimeout(revealPageContent, LOADER_CONFIG.maximumDurationMs);
    }
});