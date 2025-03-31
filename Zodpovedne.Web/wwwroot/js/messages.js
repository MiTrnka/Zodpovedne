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
            if (messagesContainer.scrollTop < 50 && hasOlderMessages && !isLoadingMessages) {
                loadMoreMessages(apiBaseUrl);
            }
        });
    }
});

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
            // Odeslání zprávy na server
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

            // Aktualizace seznamu konverzací (optional)
            // Mohli bychom aktualizovat seznam konverzací, ale pro jednoduchost
            // očekáváme, že uživatel obnoví stránku nebo příště uvidí aktualizovaný seznam
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

        // Pokud jsou další zprávy, přidáme tlačítko pro načtení dalších
        if (hasOlderMessages) {
            addLoadMoreButton();
        }

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

    // Zobrazení indikátoru načítání v tlačítku, pokud existuje
    const loadMoreBtn = document.querySelector('.load-more-messages');
    if (loadMoreBtn) {
        loadMoreBtn.innerHTML = '<span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span> Načítání...';
        loadMoreBtn.disabled = true;
    }

    try {
        isLoadingMessages = true;

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

        // Přidání zpráv do UI (na začátek, protože jsou starší)
        displayMessages(data.messages, false);

        // Aktualizace stránkování
        currentPage = nextPage;
        hasOlderMessages = data.hasOlderMessages;

        // Pokud už nejsou starší zprávy, odstraníme tlačítko
        if (!hasOlderMessages && loadMoreBtn) {
            loadMoreBtn.remove();
        }

    } catch (error) {
        console.error('Chyba při načítání starších zpráv:', error);
        if (loadMoreBtn) {
            loadMoreBtn.innerHTML = 'Nepodařilo se načíst starší zprávy';
            loadMoreBtn.classList.remove('btn-primary');
            loadMoreBtn.classList.add('btn-danger');
        }
    } finally {
        isLoadingMessages = false;

        // Obnovení tlačítka, pokud existuje a jsou další zprávy
        if (loadMoreBtn && hasOlderMessages) {
            loadMoreBtn.innerHTML = 'Načíst starší zprávy';
            loadMoreBtn.disabled = false;
        }
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

    // Vytvoření HTML pro každou zprávu
    const currentUserId = document.querySelector('meta[name="current-user-id"]')?.content;
    const messagesHTML = messages.map(message => {
        const isCurrentUserSender = message.senderUserId === currentUserId;
        const messageClass = isCurrentUserSender ? 'message-sent' : 'message-received';
        const timeFormatted = new Date(message.sentAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
        const dateFormatted = new Date(message.sentAt).toLocaleDateString();

        return `
            <div class="message ${messageClass}">
                <div class="message-content">${message.content}</div>
                <div class="message-time">${timeFormatted} | ${dateFormatted}</div>
            </div>
        `;
    }).join('');

    // Přidání zpráv do kontejneru (na začátek, pokud jsou starší)
    if (clearContainer) {
        messagesContainer.innerHTML = messagesHTML;
    } else {
        // Připojíme na začátek, protože jde o starší zprávy
        const firstMessage = messagesContainer.firstChild;
        const tempDiv = document.createElement('div');
        tempDiv.innerHTML = messagesHTML;

        while (tempDiv.firstChild) {
            messagesContainer.insertBefore(tempDiv.firstChild, firstMessage);
        }
    }

    // Scrollování na konec, pokud jde o nově načtenou konverzaci
    if (clearContainer) {
        messagesContainer.scrollTop = 0; // messagesContainer.scrollHeight;
    }
}

/**
 * Přidá tlačítko pro načtení starších zpráv
 */
function addLoadMoreButton() {
    const messagesContainer = document.getElementById('messages-container');
    if (!messagesContainer) return;

    // Vytvoření tlačítka
    const loadMoreButton = document.createElement('button');
    loadMoreButton.className = 'btn btn-outline-primary btn-sm load-more-messages';
    loadMoreButton.textContent = 'Načíst starší zprávy';

    // Přidání event listeneru
    loadMoreButton.addEventListener('click', function () {
        const apiBaseUrl = document.getElementById('apiBaseUrl')?.value;
        if (apiBaseUrl) {
            loadMoreMessages(apiBaseUrl);
        }
    });

    // Přidání na začátek kontejneru
    messagesContainer.insertBefore(loadMoreButton, messagesContainer.firstChild);
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
    messageElement.className = `message ${isFromCurrentUser ? 'message-sent' : 'message-received'}`;

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

    // Scrollování na konec
    messagesContainer.scrollTop = 0; // messagesContainer.scrollHeight;
}
