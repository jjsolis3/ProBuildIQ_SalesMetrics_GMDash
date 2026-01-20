// Notification System with SignalR
(function () {
    'use strict';

    let connection = null;
    let unreadCount = 0;

    // Initialize SignalR connection
    function initializeSignalR() {
        connection = new signalR.HubConnectionBuilder()
            .withUrl("/notificationHub")
            .withAutomaticReconnect()
            .build();

        // Handle receiving new notifications
        connection.on("ReceiveNotification", function (notification) {
            // Update unread count
            updateUnreadCount();

            // Show toast notification
            showToastNotification(notification);

            // Refresh notification dropdown
            loadNotifications();
        });

        // Handle notification marked as read
        connection.on("NotificationRead", function (notificationId) {
            updateUnreadCount();
            loadNotifications();
        });

        // Handle all notifications marked as read
        connection.on("AllNotificationsRead", function () {
            updateUnreadCount();
            loadNotifications();
        });

        // Handle notification deleted
        connection.on("NotificationDeleted", function (notificationId) {
            updateUnreadCount();
            loadNotifications();
        });

        // Start connection
        connection.start()
            .then(function () {
                // Load initial notifications
                loadNotifications();
                updateUnreadCount();
            })
            .catch(function (err) {
                // Connection failed, will retry automatically
                if (typeof toastr !== 'undefined') {
                    toastr.error('Failed to connect to notification service');
                }
            });

        // Handle reconnection
        connection.onreconnected(function () {
            loadNotifications();
            updateUnreadCount();
        });
    }

    // Cleanup SignalR connection on page unload
    function cleanupConnection() {
        if (connection) {
            connection.stop()
                .then(function () {
                    connection = null;
                })
                .catch(function (err) {
                    // Ignore errors during cleanup
                });
        }
    }

    // Load notifications from server
    function loadNotifications() {
        const controller = new AbortController();
        const timeoutId = setTimeout(() => controller.abort(), 10000); // 10 second timeout

        fetch('/Notifications/GetNotifications?unreadOnly=true&limit=10', { signal: controller.signal })
            .then(response => {
                clearTimeout(timeoutId);
                return response.json();
            })
            .then(notifications => {
                renderNotifications(notifications);
            })
            .catch(error => {
                clearTimeout(timeoutId);
                if (error.name === 'AbortError') {
                    // Request timed out
                } else {
                    // Network error
                }
            });
    }

    // Update unread count
    function updateUnreadCount() {
        const controller = new AbortController();
        const timeoutId = setTimeout(() => controller.abort(), 10000); // 10 second timeout

        fetch('/Notifications/GetUnreadCount', { signal: controller.signal })
            .then(response => {
                clearTimeout(timeoutId);
                return response.json();
            })
            .then(data => {
                unreadCount = data.count;
                updateUnreadBadge(unreadCount);
            })
            .catch(error => {
                clearTimeout(timeoutId);
                if (error.name === 'AbortError') {
                    // Request timed out
                } else {
                    // Network error
                }
            });
    }

    // Update unread badge in UI
    function updateUnreadBadge(count) {
        const badge = document.getElementById('notification-count-badge');
        const marker = document.querySelector('#page-header-notifications-dropdown .btn-marker');

        if (badge) {
            badge.textContent = count;
        }

        // Show/hide notification dot marker
        if (marker) {
            if (count > 0) {
                marker.style.display = 'inline-block';
            } else {
                marker.style.display = 'none';
            }
        }
    }

    // Render notifications in dropdown
    function renderNotifications(notifications) {
        const container = document.getElementById('notification-list');
        if (!container) return;

        if (notifications.length === 0) {
            container.innerHTML = `
                <div class="text-center p-4">
                    <i class="fas fa-bell-slash text-muted fs-1"></i>
                    <p class="text-muted mt-2">No notifications</p>
                </div>
            `;
            return;
        }

        let html = '';
        notifications.forEach(notification => {
            const isUnread = !notification.isRead;
            const timeAgo = getTimeAgo(notification.createdDate);
            const iconClass = getNotificationIcon(notification.notificationType);
            const iconColor = getNotificationColor(notification.notificationType);

            html += `
                <a href="${notification.actionUrl || '#'}"
                   class="text-reset notification-item ${isUnread ? 'unread' : ''}"
                   data-notification-id="${notification.notificationId}"
                   onclick="markAsRead(${notification.notificationId}); return true;">
                    <div class="d-flex">
                        <div class="avatar avatar-xs avatar-label-${iconColor} me-3">
                            <span class="rounded fs-16">
                                <i class="${iconClass}"></i>
                            </span>
                        </div>
                        <div class="flex-1">
                            <h6 class="mb-1 ${isUnread ? 'fw-bold' : ''}">${notification.title}</h6>
                            ${notification.message ? `<p class="fs-13 text-muted mb-1">${notification.message}</p>` : ''}
                            <div class="fs-12 text-muted">
                                <p class="mb-0"><i class="mdi mdi-clock-outline"></i> ${timeAgo}</p>
                            </div>
                        </div>
                        <i class="mdi mdi-chevron-right align-middle ms-2"></i>
                    </div>
                </a>
            `;
        });

        container.innerHTML = html;
    }

    // Mark notification as read
    window.markAsRead = function(notificationId) {
        const controller = new AbortController();
        const timeoutId = setTimeout(() => controller.abort(), 10000);

        fetch('/Notifications/MarkAsRead', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
            },
            body: JSON.stringify({ notificationId: notificationId }),
            signal: controller.signal
        })
        .then(response => {
            clearTimeout(timeoutId);
            return response.json();
        })
        .then(data => {
            if (data.success) {
                updateUnreadCount();
                loadNotifications();
            }
        })
        .catch(error => {
            clearTimeout(timeoutId);
            // Silently fail
        });
    };

    // Mark all as read
    window.markAllAsRead = function() {
        const controller = new AbortController();
        const timeoutId = setTimeout(() => controller.abort(), 10000);

        fetch('/Notifications/MarkAllAsRead', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
            },
            signal: controller.signal
        })
        .then(response => {
            clearTimeout(timeoutId);
            return response.json();
        })
        .then(data => {
            if (data.success) {
                updateUnreadCount();
                loadNotifications();
                if (typeof toastr !== 'undefined') {
                    toastr.success('All notifications marked as read');
                }
            }
        })
        .catch(error => {
            clearTimeout(timeoutId);
            if (typeof toastr !== 'undefined' && error.name !== 'AbortError') {
                toastr.error('Failed to mark notifications as read');
            }
        });
    };

    // Show toast notification
    function showToastNotification(notification) {
        if (typeof toastr !== 'undefined') {
            const options = {
                closeButton: true,
                progressBar: true,
                positionClass: 'toast-top-right',
                timeOut: 5000,
                onclick: function() {
                    if (notification.actionUrl) {
                        window.location.href = notification.actionUrl;
                    }
                }
            };

            switch (notification.type) {
                case 'TaskAssigned':
                    toastr.info(notification.message, notification.title, options);
                    break;
                case 'NoteAdded':
                    toastr.info(notification.message, notification.title, options);
                    break;
                case 'StatusChanged':
                    toastr.success(notification.message, notification.title, options);
                    break;
                case 'BroadcastMessage':
                    if (notification.priority === 'Urgent') {
                        toastr.error(notification.message, notification.title, options);
                    } else if (notification.priority === 'High') {
                        toastr.warning(notification.message, notification.title, options);
                    } else {
                        toastr.info(notification.message, notification.title, options);
                    }
                    break;
                default:
                    toastr.info(notification.message, notification.title, options);
            }
        }
    }

    // Get notification icon based on type
    function getNotificationIcon(type) {
        switch (type) {
            case 'TaskAssigned': return 'fas fa-tasks';
            case 'NoteAdded': return 'fas fa-comment';
            case 'StatusChanged': return 'fas fa-exchange-alt';
            case 'DueDateReminder': return 'fas fa-clock';
            case 'BroadcastMessage': return 'fas fa-bullhorn';
            case 'DocumentSigning': return 'fas fa-file-signature';
            default: return 'fas fa-bell';
        }
    }

    // Get notification color based on type
    function getNotificationColor(type) {
        switch (type) {
            case 'TaskAssigned': return 'primary';
            case 'NoteAdded': return 'info';
            case 'StatusChanged': return 'success';
            case 'DueDateReminder': return 'warning';
            case 'BroadcastMessage': return 'danger';
            case 'DocumentSigning': return 'secondary';
            default: return 'primary';
        }
    }

    // Calculate time ago
    function getTimeAgo(dateString) {
        const date = new Date(dateString);
        const now = new Date();
        const seconds = Math.floor((now - date) / 1000);

        if (seconds < 60) return 'just now';
        if (seconds < 3600) return Math.floor(seconds / 60) + ' min ago';
        if (seconds < 86400) return Math.floor(seconds / 3600) + ' hr ago';
        if (seconds < 2592000) return Math.floor(seconds / 86400) + ' days ago';
        if (seconds < 31536000) return Math.floor(seconds / 2592000) + ' months ago';
        return Math.floor(seconds / 31536000) + ' years ago';
    }

    // Initialize on page load
    document.addEventListener('DOMContentLoaded', function() {
        initializeSignalR();
    });

    // Cleanup on page unload
    window.addEventListener('beforeunload', function() {
        cleanupConnection();
    });

    // Fallback cleanup on unload
    window.addEventListener('unload', function() {
        cleanupConnection();
    });

})();
