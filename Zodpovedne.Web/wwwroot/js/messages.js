/**
 * JavaScript pro zpracování zpráv mezi uživateli
 * Umožňuje zobrazení konverzací, načítání a odesílání zpráv
 */

// Globální proměnné
let currentRecipientId = null;   // ID aktuálně vybraného příjemce
let currentPage = 1;             // Aktuální stránka zpráv
let hasOlderMessages = false;    // Indikátor, zda existují starší zprávy
let isLoadingMessages = false;   // Indikátor, zda probíhá načítání zpráv
const pageSize = 20;             // Počet zpráv na stránku
const REFRESH_INTERVAL = 30000; // 30 sekund

// Po načtení dokumentu inicializujeme event listenery
document.addEventListener('DOMContentLoaded', function () {
    // Získání API URL z konfigurace
    const apiBaseUrl = document.getElementById('apiBaseUrl')?.value || '';
    if (!apiBaseUrl) {
        console.error('Není definován apiBaseUrl');
        return;
    }

    // Inicializace formuláře pro odeslání zprávy
    initMessageForm(apiBaseUrl);

    // Nastavení scrollování pro kontejner zpráv
    const messagesContainer = document.getElementById('messages-container');
    if (messagesContainer) {
        // Nastavíme event listener pro detekci, když uživatel scrolluje nahoru k začátku konverzace
        messagesContainer.addEventListener('scroll', function () {
            // Pokud jsme blízko začátku a existují starší zprávy, načteme další dávku
            // scrollTop < 50 znamená, že jsme 50px od vrcholu kontejneru
            if (messagesContainer.scrollTop < 50 && hasOlderMessages && !isLoadingMessages) {
                loadMoreMessages(apiBaseUrl);
            }
        });
    }

    // Spustíme aktualizaci počtu nepřečtených zpráv každých 30 sekund
    updateUnreadCounts();
    setInterval(updateUnreadCounts, REFRESH_INTERVAL);

});

/**
 * Funkce pro aktualizaci aktuální otevřené konverzace
 * Načte nejnovější zprávy od posledního načtení
 * @returns {Promise<void>}
 */
async function refreshCurrentConversation() {
    // Pokud není otevřená žádná konverzace, není co aktualizovat
    if (!currentRecipientId) return;

    try {
        const apiBaseUrl = document.getElementById('apiBaseUrl')?.value;
        if (!apiBaseUrl) {
            console.error('Není definován apiBaseUrl');
            return;
        }

        // Načtení první stránky konverzace s aktuálním příjemcem (nejnovější zprávy)
        const response = await fetch(`${apiBaseUrl}/messages/conversation/${currentRecipientId}?page=1&pageSize=${pageSize}`, {
            headers: {
                'Authorization': `Bearer ${sessionStorage.getItem('JWTToken')}`
            }
        });

        if (!response.ok) throw new Error('Nepodařilo se načíst zprávy');

        // Zpracování odpovědi
        const data = await response.json();

        // Pokud nemáme žádné zprávy, není co aktualizovat
        if (!data.messages || data.messages.length === 0) return;

        // Získáme ID poslední zobrazené zprávy v konverzaci
        const messagesContainer = document.getElementById('messages-container');
        const existingMessages = messagesContainer.querySelectorAll('.message');
        let lastDisplayedMessageId = 0;

        if (existingMessages.length > 0) {
            // Hledáme v atributech data-id poslední zobrazené zprávy
            const lastMessage = existingMessages[existingMessages.length - 1];
            lastDisplayedMessageId = parseInt(lastMessage.getAttribute('data-id') || '0');
        }

        // Filtrujeme jen nové zprávy, které ještě nejsou zobrazeny
        const newMessages = data.messages.filter(message => message.id > lastDisplayedMessageId);

        // Pokud nemáme žádné nové zprávy, není co aktualizovat
        if (newMessages.length === 0) return;

        // Přidáme nové zprávy do UI
        newMessages.forEach(message => {
            addMessageToUI(message);
        });

        // Scrollování dolů k nejnovější zprávě
        messagesContainer.scrollTop = messagesContainer.scrollHeight;

        // Protože jsme načetli a zobrazili nové zprávy, odstraníme notifikaci u aktuálního příjemce
        removeUnreadNotification(currentRecipientId);

    } catch (error) {
        console.error('Chyba při aktualizaci konverzace:', error);
    }
}

/**
 * Odstraní notifikaci o nepřečtených zprávách od daného uživatele
 * @param {string} userId - ID uživatele, jehož notifikace chceme odstranit
 */
function removeUnreadNotification(userId) {
    const friendElement = document.querySelector(`.list-group-item[onclick*="loadConversation('${userId}'"]`);
    if (friendElement) {
        friendElement.classList.remove('list-group-item-primary');
        const badge = friendElement.querySelector('.badge');
        if (badge) {
            badge.remove();
        }
    }
}

/**
 * Inicializace formuláře pro odeslání zprávy
 * @param {string} apiBaseUrl - URL pro API endpointy
 */
function initMessageForm(apiBaseUrl) {
    const messageForm = document.getElementById('message-form');
    if (!messageForm) return;

    messageForm.addEventListener('submit', async function (event) {
        event.preventDefault();

        const recipientId = document.getElementById('recipient-id').value;
        const messageInput = document.getElementById('message-input');
        const content = messageInput.value.trim();

        if (!recipientId || !content) return;

        try {
            // KLÍČOVÁ ZMĚNA: Nejprve aktualizujeme konverzaci, PŘED odesláním nové zprávy
            // To zajistí, že uvidíme nejnovější zprávy od druhého účastníka
            await refreshCurrentConversation();

            // Až poté odešleme novou zprávu
            const response = await fetch(`${apiBaseUrl}/messages`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Authorization': `Bearer ${sessionStorage.getItem('JWTToken')}`
                },
                body: JSON.stringify({
                    recipientUserId: recipientId,
                    content: content
                })
            });

            if (!response.ok) {
                throw new Error('Nepodařilo se odeslat zprávu');
            }

            // Zpracování odpovědi
            const messageData = await response.json();

            // Přidání nové zprávy do UI
            addMessageToUI(messageData, true);

            // Vyčištění formuláře
            messageInput.value = '';

            // Scrollování dolů k nejnovější zprávě
            const messagesContainer = document.getElementById('messages-container');
            if (messagesContainer) {
                messagesContainer.scrollTop = messagesContainer.scrollHeight;
            }

        } catch (error) {
            console.error('Chyba při odesílání zprávy:', error);
            alert('Nepodařilo se odeslat zprávu. Zkuste to prosím znovu.');
        }
    });
}

/**
 * Načte konverzaci s vybraným uživatelem
 * @param {string} userId - ID uživatele, se kterým chceme zobrazit konverzaci
 * @param {string} nickname - Přezdívka uživatele pro zobrazení
 */
async function loadConversation(userId, nickname) {
    // Pokud jde o stejného uživatele, jako máme již načteného, nic neděláme
    if (currentRecipientId === userId) return;

    // Získání API URL
    const apiBaseUrl = document.getElementById('apiBaseUrl')?.value;
    if (!apiBaseUrl) {
        console.error('Není definován apiBaseUrl');
        return;
    }

    // Reset proměnných
    currentRecipientId = userId;
    currentPage = 1;
    hasOlderMessages = false;

    // Aktualizace UI
    updateConversationUI(nickname);

    // Zobrazení indikátoru načítání
    const loadingIndicator = document.getElementById('loading-indicator');
    loadingIndicator.classList.remove('d-none');

    try {
        isLoadingMessages = true;

        // Načtení zpráv z API
        const response = await fetch(`${apiBaseUrl}/messages/conversation/${userId}?page=${currentPage}&pageSize=${pageSize}`, {
            headers: {
                'Authorization': `Bearer ${sessionStorage.getItem('JWTToken')}`
            }
        });

        if (!response.ok) {
            throw new Error('Nepodařilo se načíst zprávy');
        }

        // Zpracování odpovědi
        const data = await response.json();

        // Zobrazení zpráv
        displayMessages(data.messages, true);

        // Aktualizace stránkování
        hasOlderMessages = data.hasOlderMessages;

        // Scrollování na konec k nejnovější zprávě
        const messagesContainer = document.getElementById('messages-container');
        if (messagesContainer) {
            messagesContainer.scrollTop = messagesContainer.scrollHeight;
        }

        // Aktualizace UI - odstranění označení nepřečtených zpráv u aktuálního příjemce
        removeUnreadNotification(currentRecipientId);

    } catch (error) {
        console.error('Chyba při načítání konverzace:', error);
        document.getElementById('messages-container').innerHTML =
            '<div class="alert alert-danger">Nepodařilo se načíst zprávy.</div>';
    } finally {
        isLoadingMessages = false;
        loadingIndicator.classList.add('d-none');
    }
}

/**
 * Načte další stránku starších zpráv
 * @param {string} apiBaseUrl - URL pro API endpointy
 */
async function loadMoreMessages(apiBaseUrl) {
    if (!currentRecipientId || !hasOlderMessages || isLoadingMessages) return;

    // Příprava na načtení další stránky
    const nextPage = currentPage + 1;
    const messagesContainer = document.getElementById('messages-container');

    // Zapamatujeme si aktuální pozici scrollu a výšku obsahu
    const scrollHeight = messagesContainer.scrollHeight;
    const scrollPosition = messagesContainer.scrollTop;

    try {
        isLoadingMessages = true;

        // Přidání indikátoru načítání
        const loadingIndicator = document.createElement('div');
        loadingIndicator.className = 'text-center p-2';
        loadingIndicator.innerHTML = '<div class="spinner-border spinner-border-sm" role="status"></div> Načítání...';
        messagesContainer.insertBefore(loadingIndicator, messagesContainer.firstChild);

        // Načtení starších zpráv z API
        const response = await fetch(`${apiBaseUrl}/messages/conversation/${currentRecipientId}?page=${nextPage}&pageSize=${pageSize}`, {
            headers: {
                'Authorization': `Bearer ${sessionStorage.getItem('JWTToken')}`
            }
        });

        if (!response.ok) {
            throw new Error('Nepodařilo se načíst starší zprávy');
        }

        // Zpracování odpovědi
        const data = await response.json();

        // Odstranění indikátoru načítání
        messagesContainer.removeChild(loadingIndicator);

        // Přidání zpráv do UI (na začátek, protože jsou starší)
        displayMessages(data.messages, false);

        // Aktualizace stránkování
        currentPage = nextPage;
        hasOlderMessages = data.hasOlderMessages;

        // Zachování relativní pozice scrollu
        // Zjistíme rozdíl ve výšce obsahu před a po přidání zpráv
        const newScrollHeight = messagesContainer.scrollHeight;
        const scrollDiff = newScrollHeight - scrollHeight;

        // Nastavíme scroll tak, aby uživatel zůstal na stejném místě v konverzaci
        // (posunutý o výšku nově načteného obsahu)
        messagesContainer.scrollTop = scrollPosition + scrollDiff;

    } catch (error) {
        console.error('Chyba při načítání starších zpráv:', error);
        // V případě chyby zobrazíme diskrétní notifikaci
        const errorNotification = document.createElement('div');
        errorNotification.className = 'alert alert-danger p-1 m-1 small';
        errorNotification.textContent = 'Nepodařilo se načíst starší zprávy';
        messagesContainer.insertBefore(errorNotification, messagesContainer.firstChild);

        // Automatické odstranění notifikace po 3 sekundách
        setTimeout(() => {
            if (errorNotification.parentNode === messagesContainer) {
                messagesContainer.removeChild(errorNotification);
            }
        }, 3000);
    } finally {
        isLoadingMessages = false;
    }
}

/**
 * Aktualizuje UI konverzace - nastaví titulek a zobrazí formulář
 * @param {string} nickname - Přezdívka uživatele pro zobrazení
 */
function updateConversationUI(nickname) {
    // Nastavení titulku
    const conversationTitle = document.getElementById('conversation-title');
    if (conversationTitle) {
        conversationTitle.textContent = nickname;
    }

    // Zobrazení formuláře pro odeslání zprávy
    const messageForm = document.getElementById('message-form');
    if (messageForm) {
        messageForm.classList.remove('d-none');
    }

    // Nastavení ID příjemce do formuláře
    const recipientIdInput = document.getElementById('recipient-id');
    if (recipientIdInput) {
        recipientIdInput.value = currentRecipientId;
    }

    // Vyčištění kontejneru zpráv
    const messagesContainer = document.getElementById('messages-container');
    if (messagesContainer) {
        messagesContainer.innerHTML = '';
    }

    // Skrytí prázdného stavu
    const emptyState = document.getElementById('empty-state');
    if (emptyState) {
        emptyState.classList.add('d-none');
    }
}

/**
 * Zobrazí zprávy v UI
 * @param {Array} messages - Seznam zpráv k zobrazení
 * @param {boolean} clearContainer - Zda má být kontejner před přidáním zpráv vyčištěn
 */
function displayMessages(messages, clearContainer = false) {
    const messagesContainer = document.getElementById('messages-container');
    if (!messagesContainer) return;

    // Vyčištění kontejneru, pokud je požadováno
    if (clearContainer) {
        messagesContainer.innerHTML = '';
    }

    // Pokud nejsou žádné zprávy, zobrazíme informaci
    if (messages.length === 0 && clearContainer) {
        messagesContainer.innerHTML = `
            <div class="text-center text-muted my-5">
                <p>Zatím žádné zprávy. Napište první zprávu!</p>
            </div>
        `;
        return;
    }

    // Získáme ID aktuálního uživatele z hidden pole
    let currentUserId = document.getElementById('current-user-id-input')?.value;

    // Pokud ID není v hidden poli, zkusíme ho získat ze session storage (pokud bylo uloženo při přihlášení)
    if (!currentUserId) {
        try {
            // Můžeme zkusit dekódovat JWT token
            const token = sessionStorage.getItem('JWTToken');
            if (token) {
                // Parsování JWT tokenu pro získání user ID
                const payload = JSON.parse(atob(token.split('.')[1]));
                // Hledání ID v obvyklých JWT claimech
                currentUserId = payload.nameid || payload.sub || payload.userId;
            }
        } catch (error) {
            console.error('Chyba při získávání ID uživatele:', error);
        }
    }

    // Třídíme zprávy podle času
    const sortedMessages = [...messages].sort((a, b) =>
        new Date(a.sentAt) - new Date(b.sentAt)
    );

    const messagesHTML = sortedMessages.map(message => {
        // Určení, zda je zpráva od aktuálního uživatele
        const isCurrentUserSender = message.senderUserId === currentUserId;
        const messageClass = isCurrentUserSender ? 'message-sent' : 'message-received';

        // Pro ladění - přidáme data atributy s informacemi o odesílateli
        const debugInfo = `data-sender="${message.senderUserId}" data-current="${currentUserId || 'unknown'}" data-id="${message.id}"`;

        const timeFormatted = new Date(message.sentAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
        const dateFormatted = new Date(message.sentAt).toLocaleDateString();

        return `
            <div class="message ${messageClass}" ${debugInfo}>
                <div class="message-content">${message.content}</div>
                <div class="message-time">${timeFormatted} | ${dateFormatted}</div>
            </div>
        `;
    }).join('');

    // Přidání zpráv do kontejneru
    if (clearContainer) {
        messagesContainer.innerHTML = messagesHTML;
    } else {
        // Připojíme na začátek, protože jde o starší zprávy
        const tempDiv = document.createElement('div');
        tempDiv.innerHTML = messagesHTML;

        // Vložíme všechny zprávy na začátek kontejneru
        while (tempDiv.firstChild) {
            messagesContainer.insertBefore(tempDiv.firstChild, messagesContainer.firstChild);
        }
    }
}

/**
 * Přidá novou zprávu do UI
 * @param {Object} message - Data zprávy k přidání
 * @param {boolean} isFromCurrentUser - Zda byla zpráva odeslána aktuálním uživatelem
 */
function addMessageToUI(message, isFromCurrentUser) {
    const messagesContainer = document.getElementById('messages-container');
    if (!messagesContainer) return;

    // Vytvoření HTML elementu pro zprávu
    const messageElement = document.createElement('div');

    // Pokud isFromCurrentUser není explicitně dodáno, určíme to podle message.senderUserId
    if (isFromCurrentUser === undefined) {
        // Získáme ID aktuálního uživatele z meta tagu nebo tokenu
        let currentUserId = document.querySelector('meta[name="current-user-id"]')?.content;
        if (!currentUserId) {
            try {
                const token = sessionStorage.getItem('JWTToken');
                if (token) {
                    const payload = JSON.parse(atob(token.split('.')[1]));
                    currentUserId = payload.nameid || payload.sub || payload.userId;
                }
            } catch (error) {
                console.error('Chyba při získávání ID uživatele:', error);
            }
        }
        isFromCurrentUser = message.senderUserId === currentUserId;
    }

    messageElement.className = `message ${isFromCurrentUser ? 'message-sent' : 'message-received'}`;

    // Přidáme data-id atribut pro identifikaci zprávy
    messageElement.setAttribute('data-id', message.id);

    // Formátování času
    const timeFormatted = new Date(message.sentAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
    const dateFormatted = new Date(message.sentAt).toLocaleDateString();

    // Nastavení obsahu zprávy
    messageElement.innerHTML = `
        <div class="message-content">${message.content}</div>
        <div class="message-time">${timeFormatted} | ${dateFormatted}</div>
    `;

    // Přidání na konec kontejneru
    messagesContainer.appendChild(messageElement);

    // Scrollování na konec (k nejnovější zprávě)
    messagesContainer.scrollTop = messagesContainer.scrollHeight;
}

/**
* Pravidelně aktualizuje počet nepřečtených zpráv pro každého přítele
*/
async function updateUnreadCounts() {
    const apiBaseUrl = document.getElementById('apiBaseUrl')?.value;
    if (!apiBaseUrl) return;

    try {
        const response = await fetch(`${apiBaseUrl}/messages/unread-counts-by-user`, {
            headers: {
                'Authorization': `Bearer ${sessionStorage.getItem('JWTToken')}`
            }
        });

        if (!response.ok) return;

        const unreadCounts = await response.json();

        // Aktualizace UI pro každého přítele
        document.querySelectorAll('.list-group-item-action').forEach(item => {
            const onclickAttr = item.getAttribute('onclick');
            if (!onclickAttr) return;

            // Extrahování userId z onclick atributu
            const match = onclickAttr.match(/loadConversation\('([^']+)'/);
            if (!match || !match[1]) return;

            const userId = match[1];
            const unreadCount = unreadCounts[userId] || 0;

            // Pokud je konverzace s tímto uživatelem právě otevřená,
            // refreshneme konverzaci místo zobrazení notifikace
            if (userId === currentRecipientId) {
                if (unreadCount > 0) {
                    // Volání funkce pro refresh konverzace
                    refreshCurrentConversation();
                }
                // Odstraníme notifikaci bez ohledu na počet nepřečtených zpráv
                item.classList.remove('list-group-item-primary');
                const badge = item.querySelector('.badge');
                if (badge) {
                    badge.remove();
                }
            } else {
                // Pro ostatní přátele standardní aktualizace notifikací
                let badge = item.querySelector('.badge');
                if (unreadCount > 0) {
                    if (!badge) {
                        badge = document.createElement('span');
                        badge.className = 'badge bg-primary rounded-pill';
                        const flexContainer = item.querySelector('.d-flex') || item;
                        flexContainer.appendChild(badge);
                    }
                    badge.textContent = unreadCount;
                    item.classList.add('list-group-item-primary');
                } else {
                    if (badge) {
                        badge.remove();
                    }
                    item.classList.remove('list-group-item-primary');
                }
            }
        });

    } catch (error) {
        console.error('Chyba při aktualizaci počtu nepřečtených zpráv:', error);
    }
}