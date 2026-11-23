document.addEventListener('DOMContentLoaded', function () {
    const navbarElement = document.getElementById('navbar');
    if (!navbarElement) {
        return;
    }

    const endpoints = {
        notifications: navbarElement.getAttribute('data-notifications-url') || '',
        unreadCount: navbarElement.getAttribute('data-unread-count-url') || '',
        markRead: navbarElement.getAttribute('data-mark-read-url') || '',
        generateReport: navbarElement.getAttribute('data-generate-report-url') || '',
        reportsPage: navbarElement.getAttribute('data-reports-page-url') || '/Report'
    };

    const darkModeToggle = document.getElementById('darkModeToggle');
    const darkModeIcon = document.getElementById('darkModeIcon');
    const syncfusionTheme = document.getElementById('syncfusion-theme');

    const profileImage = document.getElementById('profileImage');
    const profileTooltip = document.getElementById('profileTooltip');

    const notificationBell = document.getElementById('notificationBell');
    const bellTooltip = document.getElementById('bellTooltip');
    const transactionNotificationBadge = document.getElementById('transactionNotificationBadge');
    const notificationContent = document.getElementById('notificationContent');
    const markAllReadBtn = document.getElementById('markAllReadBtn');

    const weeklyReportIcon = document.getElementById('weeklyReportIcon');
    const reportTooltip = document.getElementById('reportTooltip');
    const weeklyReportBadge = document.getElementById('weeklyReportBadge');
    const reportContent = document.getElementById('reportContent');
    const generateReportBtn = document.getElementById('generateReportBtn');

    const tooltips = [bellTooltip, reportTooltip, profileTooltip].filter(Boolean);
    const POLL_INTERVAL_MS = 30000;
    let lastCheckedAt = 0;

    function updateDarkModeIcon(isDark) {
        if (!darkModeIcon) {
            return;
        }
        darkModeIcon.className = isDark ? 'fa-solid fa-sun' : 'fa-solid fa-moon';
    }

    function applyThemeMode(isDark) {
        document.documentElement.classList.toggle('dark-mode', isDark);
        document.body.classList.toggle('dark-mode', isDark);
        localStorage.setItem('expenseTracker_darkMode', isDark ? 'true' : 'false');

        if (syncfusionTheme) {
            syncfusionTheme.href = isDark
                ? 'https://cdn.syncfusion.com/ej2/25.2.3/fabric-dark.css'
                : 'https://cdn.syncfusion.com/ej2/25.2.3/fabric.css';
        }

        updateDarkModeIcon(isDark);
    }

    function closeAllTooltips(exceptTooltip) {
        tooltips.forEach(function (tooltip) {
            if (tooltip !== exceptTooltip) {
                tooltip.classList.remove('active');
            }
        });
    }

    function toggleTooltip(targetTooltip) {
        if (!targetTooltip) {
            return;
        }
        closeAllTooltips(targetTooltip);
        targetTooltip.classList.toggle('active');
    }

    function buildNotificationNode(sourceItem, itemType) {
        const clonedItem = sourceItem.cloneNode(true);
        if (itemType !== '2') {
            return clonedItem;
        }

        const existingAnchor = clonedItem.querySelector('a');
        const targetUrl = (existingAnchor && existingAnchor.getAttribute('href'))
            || clonedItem.getAttribute('data-url')
            || endpoints.reportsPage;

        clonedItem.style.cursor = 'pointer';
        clonedItem.setAttribute('role', 'button');
        clonedItem.setAttribute('tabindex', '0');

        clonedItem.addEventListener('click', function () {
            window.location.href = targetUrl;
        });

        clonedItem.addEventListener('keydown', function (event) {
            if (event.key === 'Enter' || event.key === ' ') {
                event.preventDefault();
                window.location.href = targetUrl;
            }
        });

        return clonedItem;
    }

    function loadNotificationItemsByType(itemType, containerElement, emptyMessage) {
        if (!containerElement || !endpoints.notifications) {
            return;
        }

        fetch(endpoints.notifications)
            .then(function (response) {
                if (!response.ok) {
                    throw new Error('Request failed');
                }
                return response.text();
            })
            .then(function (html) {
                const parser = new DOMParser();
                const parsedDocument = parser.parseFromString(html, 'text/html');
                const items = parsedDocument.querySelectorAll('.notification-item');

                containerElement.innerHTML = '';
                let hasMatchingItems = false;

                items.forEach(function (item) {
                    if (item.getAttribute('data-type') === itemType) {
                        containerElement.appendChild(buildNotificationNode(item, itemType));
                        hasMatchingItems = true;
                    }
                });

                if (!hasMatchingItems) {
                    containerElement.innerHTML = '<p class="text-muted small mb-0">' + emptyMessage + '</p>';
                }
            })
            .catch(function () {
            });
    }

    function postJsonAction(url, onSuccess) {
        if (!url) {
            return;
        }

        fetch(url, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' }
        })
            .then(function (response) {
                if (!response.ok) {
                    throw new Error('Request failed');
                }
                return response.json();
            })
            .then(function (result) {
                if (result && result.success && typeof onSuccess === 'function') {
                    onSuccess(result);
                }
            })
            .catch(function () {
            });
    }

    function checkNotifications(force) {
        if (!endpoints.unreadCount) {
            return;
        }
        if (document.hidden && !force) {
            return;
        }

        const now = Date.now();
        if (!force && now - lastCheckedAt < POLL_INTERVAL_MS) {
            return;
        }
        lastCheckedAt = now;

        fetch(endpoints.unreadCount)
            .then(function (response) {
                if (!response.ok) {
                    throw new Error('Request failed');
                }
                return response.json();
            })
            .then(function (result) {
                if (!result || !result.success) {
                    return;
                }

                if (transactionNotificationBadge) {
                    if (result.transactionCount > 0) {
                        transactionNotificationBadge.style.display = 'block';
                        transactionNotificationBadge.textContent = result.transactionCount > 9 ? '9+' : result.transactionCount;
                    } else {
                        transactionNotificationBadge.style.display = 'none';
                    }
                }

                if (weeklyReportBadge) {
                    if (result.hasWeeklyReport) {
                        weeklyReportBadge.classList.add('active');
                        weeklyReportBadge.textContent = '!';
                    } else {
                        weeklyReportBadge.classList.remove('active');
                    }
                }
            })
            .catch(function () {
            });
    }

    updateDarkModeIcon(document.documentElement.classList.contains('dark-mode') || document.body.classList.contains('dark-mode'));

    if (darkModeToggle) {
        darkModeToggle.addEventListener('click', function () {
            const isCurrentlyDark = document.body.classList.contains('dark-mode');
            applyThemeMode(!isCurrentlyDark);
        });
    }

    if (profileImage) {
        profileImage.addEventListener('error', function () {
            const fallbackSrc = profileImage.getAttribute('data-fallback-src');
            if (fallbackSrc && profileImage.src !== fallbackSrc) {
                profileImage.src = fallbackSrc;
            }
        });

        if (profileTooltip) {
            profileImage.addEventListener('click', function (event) {
                event.stopPropagation();
                toggleTooltip(profileTooltip);
            });
        }
    }

    if (notificationBell && bellTooltip) {
        notificationBell.addEventListener('click', function (event) {
            event.stopPropagation();
            toggleTooltip(bellTooltip);
            loadNotificationItemsByType('1', notificationContent, 'Brak powiadomień');
        });
    }

    if (weeklyReportIcon && reportTooltip) {
        weeklyReportIcon.addEventListener('click', function (event) {
            event.stopPropagation();
            toggleTooltip(reportTooltip);
            loadNotificationItemsByType('2', reportContent, 'Brak dostępnych raportów');
        });
    }

    document.addEventListener('click', function (event) {
        const clickedInsideNavbarControl =
            (bellTooltip && bellTooltip.contains(event.target)) ||
            (notificationBell && notificationBell.contains(event.target)) ||
            (reportTooltip && reportTooltip.contains(event.target)) ||
            (weeklyReportIcon && weeklyReportIcon.contains(event.target)) ||
            (profileTooltip && profileTooltip.contains(event.target)) ||
            event.target === profileImage;

        if (!clickedInsideNavbarControl) {
            closeAllTooltips(null);
        }
    });

    if (markAllReadBtn) {
        markAllReadBtn.addEventListener('click', function (event) {
            event.preventDefault();
            postJsonAction(endpoints.markRead, function () {
                if (transactionNotificationBadge) {
                    transactionNotificationBadge.style.display = 'none';
                }
                if (weeklyReportBadge) {
                    weeklyReportBadge.classList.remove('active');
                }
                loadNotificationItemsByType('1', notificationContent, 'Brak powiadomień');
            });
        });
    }

    if (generateReportBtn) {
        generateReportBtn.addEventListener('click', function (event) {
            event.preventDefault();
            postJsonAction(endpoints.generateReport, function () {
                if (weeklyReportBadge) {
                    weeklyReportBadge.classList.add('active');
                    weeklyReportBadge.textContent = '!';
                }
                loadNotificationItemsByType('2', reportContent, 'Brak dostępnych raportów');
            });
        });
    }

    checkNotifications(true);
    setInterval(function () {
        checkNotifications(false);
    }, POLL_INTERVAL_MS);

    document.addEventListener('visibilitychange', function () {
        if (!document.hidden) {
            checkNotifications(false);
        }
    });
});