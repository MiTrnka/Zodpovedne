/**
 * Obsluhuje notifikační systém v hlavním menu
 * slovník:
 *  badge je číslo v bublině, které zobrazuje počet nových notifikací
 *  bellIcon je ikona zvonku, která zobrazuje badge
 *
 * Skript načte notifikace o nových odpovědích z API a porovnává jejich časová razítka. Pro sledování, 
 * zda uživatel už notifikace viděl, používá příznak hasSeenNotifications uložený v localStorage prohlížeče. 
 * Když uživatel klikne na zvoneček, nastaví se tento příznak na true, což skryje červenou bublinku (badge) s počtem notifikací. 
 * Tento příznak zůstává aktivní i při navigaci na jiné stránky v rámci aplikace. Během pravidelné kontroly nových notifikací skript porovnává časová razítka 
 * odpovědí s posledním známým časem - pokud najde novější odpověď, resetuje příznak na false, což způsobí, že se červená bublinka opět zobrazí. 
 * Tento mechanismus zajišťuje, že počet notifikací u zvonečku zmizí při zobrazení a znovu se objeví jen když přibudou skutečně nové odpovědi.
 */
document.addEventListener('DOMContentLoaded', function () {
    // Elementy
    const bellIcon = document.getElementById('notification-bell');
    const badge = document.getElementById('notification-badge');
    const notificationsList = document.getElementById('notifications-list');

    // Konfigurace
    const refreshInterval = 60000; // 60 sekund
    let notificationsData = [];

    // Uložené časové razítko notifikací (pro porovnání nových)
    let lastNotificationTimestamp = localStorage.getItem('lastNotificationTimestamp') || 0;

    // Příznak, jestli uživatel viděl notifikace
    let hasSeenNotifications = localStorage.getItem('hasSeenNotifications') === 'true';

    // Načtení notifikací při načtení stránky
    loadNotifications();

    // Automatické obnovení notifikací
    setInterval(loadNotifications, refreshInterval);

    // Události
    if (bellIcon) {
        // Když se otevře dropdown, nastavit příznak "viděno"
        document.getElementById('notificationsDropdown').addEventListener('show.bs.dropdown', function () {
            // Uživatel viděl notifikace
            hasSeenNotifications = true;
            localStorage.setItem('hasSeenNotifications', 'true');

            // Deaktivace zvýraznění a skrytí badge
            bellIcon.classList.remove('notification-active');
            badge.classList.add('d-none');
        });
    }

    /**
     * Načte notifikace z API
     */
    function loadNotifications() {
        // Zkontrolujeme, zda je uživatel přihlášen
        const token = sessionStorage.getItem('JWTToken');
        if (!token) return;

        const apiBaseUrl = document.getElementById('apiBaseUrl')?.value;
        if (!apiBaseUrl) return;

        fetch(`${apiBaseUrl}/users/discussions-with-new-replies`, {
            headers: {
                'Authorization': `Bearer ${token}`
            }
        })
            .then(response => {
                if (!response.ok) throw new Error('Chyba při načítání notifikací');
                return response.json();
            })
            .then(data => {
                notificationsData = data;

                // Zjistit nejnovější časové razítko
                let newestTimestamp = 0;
                data.forEach(notification => {
                    const notificationTime = new Date(notification.latestReplyTime).getTime();
                    if (notificationTime > newestTimestamp) {
                        newestTimestamp = notificationTime;
                    }
                });

                // Zkontrolovat, zda jsou opravdu nové notifikace
                const hasNewNotifications = newestTimestamp > lastNotificationTimestamp;

                // Pokud jsou nové notifikace, resetovat příznak "viděno"
                if (hasNewNotifications) {
                    hasSeenNotifications = false;
                    localStorage.setItem('hasSeenNotifications', 'false');
                    lastNotificationTimestamp = newestTimestamp;
                    localStorage.setItem('lastNotificationTimestamp', newestTimestamp);
                }

                updateNotificationsUI();
            })
            .catch(error => {
                console.error('Chyba při načítání notifikací:', error);
            });
    }

    /**
     * Aktualizuje UI na základě načtených notifikací
     */
    function updateNotificationsUI() {
        if (!bellIcon || !badge || !notificationsList) return;

        // Aktualizace počtu a viditelnosti badge
        if (notificationsData.length > 0) {
            document.getElementById('notifications-count').textContent = notificationsData.length;

            // Zobrazit badge pouze pokud uživatel ještě neviděl notifikace
            if (!hasSeenNotifications) {
                badge.classList.remove('d-none');
                bellIcon.classList.add('notification-active');
            } else {
                badge.classList.add('d-none');
                bellIcon.classList.remove('notification-active');
            }
        } else {
            badge.classList.add('d-none');
            bellIcon.classList.remove('notification-active');
        }

        // Aktualizace seznamu notifikací
        notificationsList.innerHTML = '';

        if (notificationsData.length === 0) {
            notificationsList.innerHTML = '<div class="dropdown-item text-muted text-center py-2">Žádné nové odpovědi</div>';
            return;
        }

        notificationsData.forEach(notification => {
            const notificationTime = new Date(notification.latestReplyTime);
            const formattedTime = notificationTime.toLocaleDateString('cs-CZ') + ' ' +
                notificationTime.toLocaleTimeString('cs-CZ', { hour: '2-digit', minute: '2-digit' });

            const item = document.createElement('a');
            item.href = notification.discussionUrl;
            item.className = 'dropdown-item py-2 notification-item';
            item.innerHTML = `
                <div class="d-flex justify-content-between align-items-start">
                    <div class="text-truncate">
                        <div class="fw-medium text-truncate">${notification.title}</div>
                        <div class="small text-muted">
                            ${notification.categoryName}
                            <span class="badge bg-info ms-1" title="Počet vašich komentářů s novými odpověďmi">
                                <i class="bi bi-chat-dots-fill"></i> ${notification.commentsWithNewRepliesCount}
                            </span>
                        </div>
                    </div>
                    <span class="notification-time ms-2">${formattedTime}</span>
                </div>
            `;

            notificationsList.appendChild(item);
        });
    }
});