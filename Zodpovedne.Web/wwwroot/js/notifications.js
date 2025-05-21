/**
 * Notifikační systém pro zvoneček v hlavičce
 * slovník:
 *  badge je číslo v bublině, které zobrazuje počet nových notifikací
 *  bellIcon je ikona zvonku, která zobrazuje badge
 *
 * Tento skript:
 * 1. Načítá notifikace o nových odpovědích a komentářích z API
 * 2. Zobrazuje počet nových notifikací v badge
 * 3. Zobrazuje seznam notifikací v dropdown menu po kliknutí na zvoneček
 * 4. Sleduje dva typy časových značek:
 *    - lastNotificationTimestamp: Používá se pro sledování celkově nejnovějších notifikací (od předposledního přihlášení)
 *    - lastBadgeClickTimestamp: Používá se pouze pro počítání nových notifikací v badge (od posledního kliknutí)
 * 5. Zobrazuje notifikace pro:
 *    - Nové odpovědi na komentáře přihlášeného uživatele
 *    - Nové komentáře v diskuzích, které založil přihlášený uživatel
 *    - Zajišťuje, že pro jednu diskuzi se nezobrazí více notifikací, i když splňuje více podmínek
 */
document.addEventListener('DOMContentLoaded', function () {

    // NÍŽE JE SEKCE PRO POČET ŽÁDOSTÍ O PŘÁTELSTVÍ
    // Získáme element pro ikonu přátelství
    const friendshipIcon = document.getElementById('user-icon');
    if (!friendshipIcon) return;
    // Přidáme event listener pro kliknutí na ikonu přátelství
    friendshipIcon.addEventListener('click', function () {
        // Přesměrování na stránku s profilem
        window.location.href = '/Account/MyProfile';
    });
    // Načteme počet žádostí o přátelství
    loadFriendshipRequests();
    // Nastavíme pravidelné načítání počtu žádostí o přátelství
    setInterval(loadFriendshipRequests, 30000); // každých 30 sekund
    /**
     * Funkce pro načtení počtu žádostí o přátelství a aktualizaci UI
     * Spouští se při načtení stránky a poté v pravidelném intervalu
     */
    function loadFriendshipRequests() {
        // Zkontrolujeme, zda je uživatel přihlášen
        const token = sessionStorage.getItem('JWTToken');
        if (!token) return;

        // Zkontrolujeme, zda máme URL pro API
        const apiBaseUrl = document.getElementById('apiBaseUrl')?.value;
        if (!apiBaseUrl) return;

        // Získáme element pro ikonu přátelství a badge
        const friendshipBadge = document.getElementById('friendship-badge');
        const friendshipCount = document.getElementById('friendship-count');

        // Pokud elementy neexistují, ukončíme funkci
        if (!friendshipBadge || !friendshipCount) return;

        // Načtení počtu žádostí o přátelství z API
        fetch(`${apiBaseUrl}/users/friendship-requests-count`, {
            headers: {
                'Authorization': `Bearer ${token}`
            }
        })
            .then(response => {
                if (!response.ok) throw new Error('Chyba při načítání počtu žádostí o přátelství');
                return response.json();
            })
            .then(count => {
                // Aktualizace počtu v badge
                friendshipCount.textContent = count;

                // Zobrazení/skrytí badge podle počtu žádostí
                if (count > 0) {
                    friendshipBadge.classList.remove('d-none');
                } else {
                    friendshipBadge.classList.add('d-none');
                }

                updateHamburgerMenuNotification();
            })
            .catch(error => {
                console.error('Chyba při načítání počtu žádostí o přátelství:', error);
            });

    }



    // NÍŽE JE SEKCE PRO NOTIFIKAČNÍ SYSTÉM PRO NOVÉ ODPVĚDI V DISKUZÍCH A NOVÉ KOMENTÁŘE V DISKUZÍCH VYTVOŘENÝCH UŽIVATELEM
    // Hlavní elementy
    const bellIcon = document.getElementById('notification-bell');
    const badge = document.getElementById('notification-badge');
    const notificationsList = document.getElementById('notifications-list');
    // Pokud chybí základní elementy, notifikační systém nemůže fungovat
    if (!bellIcon || !badge || !notificationsList) return;

    // Konfigurace
    const REFRESH_INTERVAL = 30000; // 30 sekund
    const ANIMATION_CLASS = 'notification-active';
    const BADGE_HIDE_CLASS = 'd-none';

    // Datový objekt pro lokální stav
    const notificationState = {
        // Data notifikací
        items: [],

        // Časové značky
        lastNotificationTimestamp: parseInt(localStorage.getItem('lastNotificationTimestamp') || '0'),
        lastBadgeClickTimestamp: parseInt(localStorage.getItem('lastBadgeClickTimestamp') || '0'),

        // Příznak "viděno" pro celý notifikační systém (pro animaci zvonečku)
        hasSeenNotifications: localStorage.getItem('hasSeenNotifications') === 'true'
    };

    // Inicializace
    setupEventListeners();
    loadNotifications();

    // Automatické obnovení notifikací v pravidelném intervalu
    setInterval(loadNotifications, REFRESH_INTERVAL);

    /**
     * Nastaví posluchače událostí pro interakci s notifikačním systémem
     */
    function setupEventListeners() {
        // Posluchač události pro kliknutí na zvoneček (otevření dropdown menu)
        document.getElementById('notificationsDropdown').addEventListener('show.bs.dropdown', function () {
            // Aktualizace stavu "viděno" pro celkové notifikace
            notificationState.hasSeenNotifications = true;
            localStorage.setItem('hasSeenNotifications', 'true');

            // Uložit aktuální čas kliknutí na zvoneček pro počítání nových notifikací v badge
            notificationState.lastBadgeClickTimestamp = Date.now();
            localStorage.setItem('lastBadgeClickTimestamp', notificationState.lastBadgeClickTimestamp.toString());

            // Deaktivace zvýraznění a skrytí badge
            bellIcon.classList.remove(ANIMATION_CLASS);
            badge.classList.add(BADGE_HIDE_CLASS);
        });
    }

    /**
     * Načte diskuzní notifikace z API
     * Volá se při inicializaci a poté pravidelně v intervalu
     * Zpracovává jak notifikace o nových odpovědích na komentáře, tak i o nových komentářích v diskuzích vytvořených uživatelem
     */
    function loadNotifications() {
        // Zkontrolujeme, zda je uživatel přihlášen
        const token = sessionStorage.getItem('JWTToken');
        if (!token) return;

        // Zkontrolujeme, zda máme URL pro API
        const apiBaseUrl = document.getElementById('apiBaseUrl')?.value;
        if (!apiBaseUrl) return;

        // Načtení notifikací z API
        fetch(`${apiBaseUrl}/users/discussions-with-new-activities`, {
            headers: {
                'Authorization': `Bearer ${token}`
            }
        })
            .then(response => {
                if (!response.ok) throw new Error('Chyba při načítání notifikací');
                return response.json();
            })
            .then(data => {
                // Odstranění duplicit - pokud diskuze má více typů aktivit, ponecháme pouze jednu notifikaci
                // ale zachováme informaci o typech aktivit (nové odpovědi, nové komentáře)
                const uniqueDiscussions = new Map();

                data.forEach(notification => {
                    const discussionId = notification.discussionId;

                    // Pokud ještě nemáme notifikaci pro tuto diskuzi, přidáme ji
                    if (!uniqueDiscussions.has(discussionId)) {
                        uniqueDiscussions.set(discussionId, notification);
                    } else {
                        // Pokud už máme notifikaci pro tuto diskuzi, aktualizujeme ji
                        const existing = uniqueDiscussions.get(discussionId);

                        // Zachováme nejnovější čas aktivity
                        if (new Date(notification.latestActivityTime) > new Date(existing.latestActivityTime)) {
                            existing.latestActivityTime = notification.latestActivityTime;
                        }

                        // Sloučíme počty aktivit
                        if (notification.commentsWithNewRepliesCount) {
                            existing.commentsWithNewRepliesCount = (existing.commentsWithNewRepliesCount || 0) + notification.commentsWithNewRepliesCount;
                        }

                        if (notification.newCommentsCount) {
                            existing.newCommentsCount = (existing.newCommentsCount || 0) + notification.newCommentsCount;
                        }

                        // Aktualizujeme typ aktivity
                        if (notification.activityType) {
                            existing.activityType = existing.activityType ?
                                `${existing.activityType}, ${notification.activityType}` :
                                notification.activityType;
                        }
                    }
                });

                // Převedeme zpět na pole a seřadíme podle času aktivity (nejnovější první)
                notificationState.items = Array.from(uniqueDiscussions.values())
                    .sort((a, b) => new Date(b.latestActivityTime) - new Date(a.latestActivityTime));

                // Zjištění nejnovějšího časového razítka mezi všemi notifikacemi
                let newestTimestamp = 0;

                notificationState.items.forEach(notification => {
                    const notificationTime = new Date(notification.latestActivityTime).getTime();
                    if (notificationTime > newestTimestamp) {
                        newestTimestamp = notificationTime;
                    }
                });

                // Zjištění, zda přibyly nové notifikace od posledního načtení
                const hasNewGlobalNotifications = newestTimestamp > notificationState.lastNotificationTimestamp;

                // Pokud jsou nové notifikace v globálním kontextu (od předposledního přihlášení),
                // resetovat příznak "viděno" a aktualizovat časovou značku
                if (hasNewGlobalNotifications) {
                    notificationState.hasSeenNotifications = false;
                    localStorage.setItem('hasSeenNotifications', 'false');
                    notificationState.lastNotificationTimestamp = newestTimestamp;
                    localStorage.setItem('lastNotificationTimestamp', newestTimestamp.toString());
                }

                // Aktualizace UI na základě nových dat
                updateNotificationsUI();

            })
            .catch(error => {
                console.error('Chyba při načítání notifikací:', error);
            });
    }


    function updateHamburgerMenuNotification() {
        // kontrola, zda existuje alespon jedna ikona s notifikací v menu
        const newMessage = $("#messages-badge");
        const newFriendshipRequest = $("#friendship-badge");
        const newReply = $("#notification-badge");
        const hamburgerMenuNotification = $("#hamburger-menu-notification");

        if ((newMessage.length > 0 && !newMessage.hasClass('d-none')) || (newFriendshipRequest.length > 0 && !newFriendshipRequest.hasClass('d-none')) || (newReply.length > 0 && !newReply.hasClass('d-none'))) {
            hamburgerMenuNotification.removeClass('d-none');
        } else {
            hamburgerMenuNotification.addClass('d-none');
        }

    }

    /**
     * Aktualizuje UI na základě načtených notifikací
     * Odděluje logiku pro badge a pro obsah dropdown menu
     */
    function updateNotificationsUI() {
        // Aktualizace seznamu notifikací v dropdown menu
        updateNotificationsList();

        // Aktualizace počtu a viditelnosti badge
        updateNotificationBadge();

        // zobrazeni Ikony u Hamburger munu, pokud existuje notifikace
        updateHamburgerMenuNotification();
    }

    /**
     * Aktualizuje seznam notifikací v dropdown menu
     * Používá kompletní seznam notifikací včetně nových komentářů v uživatelových diskuzích
     */
    function updateNotificationsList() {
        notificationsList.innerHTML = '';

        // Pokud nejsou žádné notifikace, zobrazíme informační zprávu
        if (notificationState.items.length === 0) {
            notificationsList.innerHTML = '<div class="dropdown-item text-muted text-center py-2">Žádné nové aktivity</div>';
            return;
        }

        // Vytvoření položek pro každou notifikaci
        notificationState.items.forEach(notification => {
            // Formátování času
            const notificationTime = new Date(notification.latestActivityTime);
            const formattedTime = formatDateTime(notificationTime);

            // Vytvoření položky notifikace
            const item = createNotificationItem(notification, formattedTime);

            // Přidání do seznamu
            notificationsList.appendChild(item);
        });
    }

    /**
     * Formátuje datum a čas pro zobrazení v notifikaci
     * @param {Date} date - datum k formátování
     * @returns {string} formátovaný řetězec
     */
    function formatDateTime(date) {
        return date.toLocaleDateString('cs-CZ') + ' ' +
            date.toLocaleTimeString('cs-CZ', { hour: '2-digit', minute: '2-digit' });
    }

    /**
     * Vytvoří DOM element pro jednu položku notifikace
     * Zobrazuje kombinované informace o nových odpovědích na komentáře a nových komentářích v diskuzích
     * do session přidává položku scrollToComments, aby se pak při načtení stránky provedl scroll k těm komentářům
     * @param {Object} notification - data notifikace
     * @param {string} formattedTime - formátovaný čas
     * @returns {HTMLElement} vytvořený DOM element
     */
    function createNotificationItem(notification, formattedTime) {
        const item = document.createElement('a');

        // Jednoduchý odkaz bez hash, abychom mohli lépe kontrolovat chování
        item.href = notification.discussionUrl;

        // Rozšířený onclick handler pro řešení obou problémů
        item.onclick = function (e) {
            e.preventDefault(); // Vždy zabráníme výchozímu chování

            // Získáme aktuální cestu bez query parametrů a hash
            const currentPath = window.location.pathname;
            // Získáme cílovou cestu
            const targetPath = notification.discussionUrl;

            // Pokud jsme na stejné stránce
            if (currentPath === targetPath) {
                // Uložíme flag do sessionStorage, že po načtení máme scrollovat
                sessionStorage.setItem('scrollToComments', 'true');
                // Vynutíme reload stránky
                window.location.reload(true);
            } else {
                // Pokud jdeme na jinou stránku, uložíme flag do sessionStorage
                sessionStorage.setItem('scrollToComments', 'true');
                // Přesměrování na cílovou stránku
                window.location.href = notification.discussionUrl;
            }

            return false;
        };

        item.className = 'dropdown-item py-2 notification-item';

        // Zbytek funkce zůstává stejný
        let activityBadges = '';

        if (notification.commentsWithNewRepliesCount > 0) {
            activityBadges += `
            <span class="badge bg-info ms-1" title="Počet vašich komentářů s novými odpověďmi">
                <i class="bi bi-chat-dots-fill"></i> ${notification.commentsWithNewRepliesCount}
            </span>
        `;
        }

        if (notification.newCommentsCount > 0) {
            activityBadges += `
            <span class="badge bg-primary ms-1" title="Počet nových komentářů ve vaší diskuzi">
                <i class="bi bi-chat-left-text-fill"></i> ${notification.newCommentsCount}
            </span>
        `;
        }

        item.innerHTML = `
        <div class="d-flex justify-content-between align-items-start">
            <div class="text-truncate">
                <div class="fw-medium text-truncate">${notification.title}</div>
                <div class="small text-muted">
                    ${notification.categoryName}
                    ${activityBadges}
                </div>
            </div>
            <span class="notification-time ms-2">${formattedTime}</span>
        </div>
    `;

        return item;
    }

    /**
     * Aktualizuje počet a viditelnost badge
     * Počet nových notifikací pro badge se počítá od posledního kliknutí na zvoneček
     * Zahrnuje jak nové odpovědi na komentáře, tak nové komentáře v diskuzích vytvořených uživatelem
     */
    function updateNotificationBadge() {
        // Počet notifikací pro badge - od posledního kliknutí na zvoneček
        const badgeNotificationsCount = notificationState.items.filter(notification => {
            const notificationTime = new Date(notification.latestActivityTime).getTime();
            return notificationTime > notificationState.lastBadgeClickTimestamp;
        }).length;

        // Nastavení počtu v badge
        document.getElementById('notifications-count').textContent =
            badgeNotificationsCount > 0 ? badgeNotificationsCount : notificationState.items.length;

        // Zobrazení/skrytí badge na základě počtu nových notifikací a stavu "viděno"
        if (badgeNotificationsCount > 0 && !notificationState.hasSeenNotifications) {
            badge.classList.remove(BADGE_HIDE_CLASS);
            bellIcon.classList.add(ANIMATION_CLASS);
        } else if (notificationState.items.length > 0 && !notificationState.hasSeenNotifications) {
            // Pokud nejsou nové notifikace od posledního kliknutí, ale jsou od předposledního přihlášení
            // a uživatel je ještě neviděl, zobrazíme celkový počet
            badge.classList.remove(BADGE_HIDE_CLASS);
            bellIcon.classList.add(ANIMATION_CLASS);
        } else {
            badge.classList.add(BADGE_HIDE_CLASS);
            bellIcon.classList.remove(ANIMATION_CLASS);
        }
        updateHamburgerMenuNotification();
    }


    // NÍŽE JE SEKCE PRO POČET NEPŘEČTENÝCH ZPRÁV
    // Získáme element pro ikonu zpráv
    const messagesIcon = document.getElementById('messages-icon');
    if (messagesIcon) {
        // Přidáme event listener pro kliknutí na ikonu zpráv
        messagesIcon.addEventListener('click', function () {
            // Přesměrování na stránku zpráv
            window.location.href = '/Messages';
        });
        // Načteme počet nepřečtených zpráv
        loadUnreadMessagesCount();
        // Nastavíme pravidelné načítání počtu nepřečtených zpráv
        setInterval(loadUnreadMessagesCount, 30000); // každých 30 sekund
    }

    /**
     * Funkce pro načtení počtu nepřečtených zpráv a aktualizaci UI
     * Spouští se při načtení stránky a poté v pravidelném intervalu
     */
    function loadUnreadMessagesCount() {
        // Zkontrolujeme, zda je uživatel přihlášen
        const token = sessionStorage.getItem('JWTToken');
        if (!token) return;

        // Zkontrolujeme, zda máme URL pro API
        const apiBaseUrl = document.getElementById('apiBaseUrl')?.value;
        if (!apiBaseUrl) return;

        // Získáme element pro badge (číselný indikátor) u ikony zpráv
        const messagesBadge = document.getElementById('messages-badge');
        const messagesCount = document.getElementById('messages-count');

        // Pokud elementy neexistují, ukončíme funkci
        if (!messagesBadge || !messagesCount) return;

        // Načtení počtu nepřečtených zpráv z API
        fetch(`${apiBaseUrl}/messages/unread-count`, {
            headers: {
                'Authorization': `Bearer ${token}`
            }
        })
            .then(response => {
                if (!response.ok) throw new Error('Chyba při načítání počtu nepřečtených zpráv');
                return response.json();
            })
            .then(count => {
                // Aktualizace počtu v badge
                messagesCount.textContent = count;

                // Zobrazení/skrytí badge podle počtu zpráv
                if (count > 0) {
                    messagesBadge.classList.remove('d-none');
                    // Volitelně: přidat animaci nebo zvýraznění ikony pro upozornění uživatele
                    messagesIcon.classList.add('text-primary');
                } else {
                    messagesBadge.classList.add('d-none');
                    messagesIcon.classList.remove('text-primary');
                }
                // Zobrazeni hamburgeh ikkony, pokud existuje nejaka notifikace
                updateHamburgerMenuNotification();

            })
            .catch(error => {
                console.error('Chyba při načítání počtu nepřečtených zpráv:', error);
            });
    }
});