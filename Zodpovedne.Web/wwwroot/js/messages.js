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
const REFRESH_INTERVAL = 20000; // 20 sekund

/**
 * Zvýrazní řádek aktuálně vybraného přítele v seznamu
 * @param {string} userId - ID přítele, jehož řádek má být zvýrazněn
 */
function highlightSelectedFriend(userId) {
    // Pokud není userId nastaveno, neprovádět žádné zvýraznění
    if (!userId) return;

    // Nejprve odstraníme zvýraznění ze všech řádků
    document.querySelectorAll('.list-group-item').forEach(item => {
        // Odstraníme třídu 'selected-friend' ze všech prvků
        item.classList.remove('selected-friend');
    });

    // Nyní přidáme zvýraznění vybranému příteli
    const friendElement = document.querySelector(`.list-group-item[onclick*="loadConversation('${userId}'"]`);
    if (friendElement) {
        friendElement.classList.add('selected-friend');
    }
}

// Po načtení dokumentu (stránky Messages.cshtml) inicializujeme event listenery
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

    // Přidáme přímé obnovení aktuální konverzace každých 5 sekund
    // Toto zajistí častější kontrolu stavu přečtení zpráv
    setInterval(() => {
        if (currentRecipientId) {
            refreshCurrentConversation();
        }
    }, 10000); // Každých 10 sekund

});

/**
 Aktualizuje aktuálně otevřenou konverzaci novými daty z API, přičemž zachovává kontext probíhající konverzace.
 Asynchronně načítá nejnovější sadu zpráv, porovnává je s aktuálně zobrazenými, identifikuje a přidává nové zprávy a aktualizuje
 stavy existujících - především indikátory přečtení pro zprávy, které si protistrana mezitím přečetla.
 Funkce také ošetřuje speciální případy, jako je prázdná konverzace nebo chyby při komunikaci se serverem.
 Je volána automaticky v pravidelných intervalech, což zajišťuje, že uživatel vidí aktuální obsah
 konverzace včetně indikátorů přečtení bez nutnosti ručního obnovení stránky.
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

        // Zobrazení indikátoru načítání - aby uživatel věděl, že se něco děje
        const loadingIndicator = document.getElementById('loading-indicator');
        if (loadingIndicator) {
            loadingIndicator.classList.remove('d-none');
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

        // Získáme ID aktuálně přihlášeného uživatele
        let currentUserId = document.getElementById('current-user-id-input')?.value;

        // Pokud nemáme žádné zprávy, není co aktualizovat
        if (!data.messages || data.messages.length === 0) {
            if (loadingIndicator) {
                loadingIndicator.classList.add('d-none');
            }
            return;
        }

        // Vytvoříme mapu zpráv z nově načtených dat pro rychlý přístup
        const messagesMap = new Map();
        data.messages.forEach(message => {
            messagesMap.set(message.id, message);
        });

        // Získáme všechny aktuálně zobrazené zprávy v konverzaci
        const messagesContainer = document.getElementById('messages-container');
        const existingMessages = messagesContainer.querySelectorAll('.message');

        // Procházíme všechny existující zprávy a aktualizujeme jejich stav přečtení
        existingMessages.forEach(messageElement => {
            const messageId = parseInt(messageElement.getAttribute('data-id') || '0');
            if (messageId > 0 && messagesMap.has(messageId)) {
                const updatedMessage = messagesMap.get(messageId);
                const senderId = messageElement.getAttribute('data-sender');

                // Pokud je zpráva od aktuálního uživatele a byla přečtena, přidáme indikátor
                if (senderId === currentUserId && updatedMessage.readAt) {
                    // Hledáme, zda už existuje indikátor přečtení
                    let readIndicator = messageElement.querySelector('.message-read-indicator');

                    // Pokud indikátor neexistuje a zpráva byla přečtena, přidáme ho
                    if (!readIndicator) {
                        // Najdeme footer zprávy nebo ho vytvoříme
                        let messageFooter = messageElement.querySelector('.message-footer');
                        if (!messageFooter) {
                            // Vytvoříme strukturu pro footer, pokud neexistuje
                            const messageTime = messageElement.querySelector('.message-time');
                            if (messageTime) {
                                // Vytvoříme footer a přesuneme do něj časový údaj
                                messageFooter = document.createElement('div');
                                messageFooter.className = 'message-footer';
                                messageTime.parentNode.insertBefore(messageFooter, messageTime);
                                messageFooter.appendChild(messageTime);
                            }
                        }

                        // Pokud máme footer, přidáme indikátor přečtení
                        if (messageFooter) {
                            readIndicator = document.createElement('span');
                            readIndicator.className = 'message-read-indicator';
                            readIndicator.innerHTML = '<i class="bi bi-check2"></i>';
                            messageFooter.appendChild(readIndicator);
                        }
                    }
                }
            }
        });

        // Původní logika pro přidání nových zpráv
        const existingMessageIds = Array.from(existingMessages)
            .map(msg => parseInt(msg.getAttribute('data-id') || '0'))
            .filter(id => id > 0);

        // Zkontrolujeme, zda máme nějaký obsah v kontejneru
        const emptyConversationMessage = messagesContainer.querySelector('.text-center.text-muted.my-5');
        // Pokud jde o prázdnou konverzaci a existují nové zprávy, kompletně nahradíme obsah
        if (emptyConversationMessage && data.messages.length > 0) {
            // Vyčistíme kontejner a zobrazíme všechny zprávy znovu
            messagesContainer.innerHTML = '';
            // Použijeme funkci displayMessages pro správné zobrazení všech zpráv
            displayMessages(data.messages, true);
        }
        // Jinak přidáme jen nové zprávy
        else {
            // Filtrujeme zprávy, které ještě nejsou zobrazeny
            const newMessages = data.messages.filter(message => !existingMessageIds.includes(message.id));

            if (newMessages.length > 0) {
                // Seřadíme zprávy podle času (od nejstarší po nejnovější)
                newMessages.sort((a, b) => new Date(a.sentAt) - new Date(b.sentAt));

                // Přidáme nové zprávy do UI
                newMessages.forEach(message => {
                    // Určení, zda odesílatelem zprávy je aktuální uživatel
                    const isCurrentUserSender = message.senderUserId === currentUserId;
                    addMessageToUI(message, isCurrentUserSender);
                });

                // Scrollování dolů k nejnovější zprávě
                messagesContainer.scrollTop = messagesContainer.scrollHeight;
            }
        }

        // Odstraníme notifikaci u aktuálního příjemce
        removeUnreadNotification(currentRecipientId);

    } catch (error) {
        console.error('Chyba při aktualizaci konverzace:', error);
    } finally {
        // Skrytí indikátoru načítání
        const loadingIndicator = document.getElementById('loading-indicator');
        if (loadingIndicator) {
            loadingIndicator.classList.add('d-none');
        }
    }
}

/**
 * Odstraní notifikaci o nepřečtených zprávách od daného uživatele
 * @param {string} userId - ID uživatele, jehož notifikace chceme odstranit
 */
function removeUnreadNotification(userId) {
    const friendElement = document.querySelector(`.list-group-item[onclick*="loadConversation('${userId}'"]`);
    if (friendElement) {
        // Odstraníme list-group-item-primary, která způsobuje modré podbarvení pro notifikace
        friendElement.classList.remove('list-group-item-primary');

        // Ale zachováme zvýraznění, pokud je to aktuálně vybraný přítel
        if (userId === currentRecipientId) {
            friendElement.classList.add('selected-friend');
        }

        const badge = friendElement.querySelector('.badge');
        if (badge) {
            badge.remove();
        }
    }
}

/**
 * Inicializace formuláře pro odeslání zprávy, spouští se ihned po načtení stránky Messages.cshtml a po odeslání nové zprávy
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
            // OPRAVA: Nejprve aktualizujeme konverzaci, ABY byly načteny všechny předchozí zprávy
            // To pomůže řešit problém s chybějícími zprávami při prvním odeslání
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

            // OPRAVA: Přidání nové zprávy do UI s explicitním nastavením isFromCurrentUser na true
            // Toto zajistí správné formátování zprávy jako odeslané
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
 * Po kliknutí na přítele se načte konverzace s ním a odstraní se u něho notifikace o nepřečtených zprávách
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
    currentRecipientId = userId; // Nastavení ID právě vybraného přítele, se kterým budeme komunikovat
    currentPage = 1;
    hasOlderMessages = false;

    // Aktualizace UI
    updateConversationUI(nickname);

    // Zvýraznění vybraného přítele v seznamu
    highlightSelectedFriend(userId);

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
 * Zobrazuje celou sadu zpráv najednou v uživatelském rozhraní, přičemž může buď vyčistit a kompletně přepsat existující obsah chatu,
 * nebo přidat zprávy na určitou pozici. Zpracovává pole zpráv, třídí je podle času, vytváří pro každou zprávu HTML reprezentaci včetně identifikátorů,
 * formátování textu, časových značek a indikátorů přečtení. Zajišťuje správné rozlišení mezi odeslanými a přijatými zprávami a jejich
 * odpovídající vizuální stylování. Tato funkce se používá především při prvotním načtení konverzace nebo načítání větších dávek historických zpráv.
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

    // OPRAVA: Třídíme zprávy podle času (od nejstarší po nejnovější)
    // Toto zajistí správné pořadí zpráv v konverzaci
    const sortedMessages = [...messages].sort((a, b) =>
        new Date(a.sentAt) - new Date(b.sentAt)
    );

    // Sestavíme HTML pro všechny zprávy
    const messagesHTML = sortedMessages.map(message => {
        // Určení, zda je zpráva od aktuálního uživatele
        const isCurrentUserSender = message.senderUserId === currentUserId;
        const messageClass = isCurrentUserSender ? 'message-sent' : 'message-received';

        // Přidáme data atributy s informacemi o zprávě a odesílateli pro identifikaci
        const debugInfo = `data-sender="${message.senderUserId}" data-current="${currentUserId || 'unknown'}" data-id="${message.id}"`;

        // Formátování času a data
        const timeFormatted = new Date(message.sentAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
        const dateFormatted = new Date(message.sentAt).toLocaleDateString();

        // Nahrazení znaku nového řádku za <br> tag pro HTML zobrazení
        const formattedContent = message.content.replace(/\n/g, '<br>');

        // Přidání ikony přečtení pokud je zpráva odeslána aktuálním uživatelem a byla přečtena
        const readIndicator = (isCurrentUserSender && message.readAt)
            ? '<span class="message-read-indicator"><i class="bi bi-check2"></i></span>'
            : '';

        return `
            <div class="message ${messageClass}" ${debugInfo}>
                <div class="message-content">${formattedContent}</div>
                <div class="message-footer">
                    <div class="message-time">${timeFormatted} | ${dateFormatted}</div>
                    ${readIndicator}
                </div>
            </div>
        `;
    }).join('');

    // Přidání zpráv do kontejneru
    if (clearContainer) {
        messagesContainer.innerHTML = messagesHTML;
    } else {
        // OPRAVA: Změna způsobu přidávání starších zpráv
        // Pokud jde o starší zprávy, přidáme je na začátek
        const tempDiv = document.createElement('div');
        tempDiv.innerHTML = messagesHTML;

        // Vložíme všechny zprávy na začátek kontejneru
        while (tempDiv.firstChild) {
            messagesContainer.insertBefore(tempDiv.firstChild, messagesContainer.firstChild);
        }
    }
}

/**
 * Přidává jednotlivou zprávu do uživatelského rozhraní chatu. Vytváří HTML element zprávy s odpovídajícími třídami, zobrazuje obsah zprávy včetně formátování,
 * časové značky a indikátoru přečtení. Následně vkládá nově vytvořený element do kontejneru zpráv a zajišťuje,
 * aby se zobrazení automaticky posunulo na nejnovější zprávu. Tato funkce se typicky používá při odeslání nové zprávy
 * nebo pro přidání jednotlivých zpráv při jejich postupném načítání.
 * @param {Object} message - Data zprávy k přidání
 * @param {boolean} isFromCurrentUser - Zda byla zpráva odeslána aktuálním uživatelem
 */
function addMessageToUI(message, isFromCurrentUser) {
    const messagesContainer = document.getElementById('messages-container');
    if (!messagesContainer) return;

    // Vytvoření HTML elementu pro zprávu
    const messageElement = document.createElement('div');
    messageElement.className = `message ${isFromCurrentUser ? 'message-sent' : 'message-received'}`;

    // Přidáme data-id atribut pro identifikaci zprávy
    messageElement.setAttribute('data-id', message.id);

    // Přidáme data-sender atribut, použijeme přímo ID z objektu zprávy
    // Tím se vyhneme možným problémům s chybějícím currentUserId
    messageElement.setAttribute('data-sender', message.senderUserId);

    // Formátování času
    const timeFormatted = new Date(message.sentAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
    const dateFormatted = new Date(message.sentAt).toLocaleDateString();

    // Náhrada znaku nového řádku za <br> tag
    const formattedContent = message.content.replace(/\n/g, '<br>');

    // Přidání ikony přečtení pokud je zpráva odeslána aktuálním uživatelem a byla přečtena
    const readIndicator = (isFromCurrentUser && message.readAt)
        ? '<span class="message-read-indicator"><i class="bi bi-check2"></i></span>'
        : '';

    // Nastavení obsahu zprávy
    messageElement.innerHTML = `
    <div class="message-content">${formattedContent}</div>
    <div class="message-footer">
        <div class="message-time">${timeFormatted} | ${dateFormatted}</div>
        ${readIndicator}
    </div>
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
            const userId = item.dataset.userId;
            if (!userId) return;

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
                } else {
                    if (badge) {
                        badge.remove();
                    }
                    // Pro jistotu odstraníme podbarvení, pokud by tam bylo
                    item.classList.remove('list-group-item-primary');
                }
            }
        });

    } catch (error) {
        console.error('Chyba při aktualizaci počtu nepřečtených zpráv:', error);
    }
}
// Otevreni nabidky smajliku - pridat komentar
const emojiBtn = document.getElementById("emoji-btn");
const emojiList = document.getElementById("emoji-list");
const textarea = document.getElementById("message-input");
const poleSmajliku = document.querySelectorAll("#emoji-list .emoji");


emojiBtn.addEventListener("click", () => {
    emojiList.style.display = emojiList.style.display === "block" ? "none" : "block";
});
// Vlozeni smajlika do textarei pri kliknuti na smajlika
poleSmajliku.forEach(smajlik => {
    smajlik.addEventListener("click", () => {
        // Získání aktuální hodnoty textarea
        const aktualni = textarea.value;
        // Získání pozice kurzoru
        const start = textarea.selectionStart;
        const end = textarea.selectionEnd;

        // Vložení emoji na pozici kurzoru
        textarea.value = aktualni.substring(0, start) + smajlik.textContent + aktualni.substring(end);

        // Nastavení kurzoru za vložený smajlík
        const newPosition = start + smajlik.textContent.length;
        textarea.setSelectionRange(newPosition, newPosition);
    });
});
