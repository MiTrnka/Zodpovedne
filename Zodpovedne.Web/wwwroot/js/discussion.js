/**
 * JavaScript pro stránku s detailem diskuze
 * Závislosti:
 * - jQuery
 * - Bootstrap
 * - CKEditor 5
 * - Bootstrap Icons
 */

// Inicializace při načtení stránky
document.addEventListener('DOMContentLoaded', function () {
    // Kontrola, zda máme scrollovat k sekcí komentářů
    if (sessionStorage.getItem('scrollToComments') === 'true') {
        // Vymažeme flag, aby se nepoužil při dalším načtení
        sessionStorage.removeItem('scrollToComments');

        // Počkáme 100ms, aby se stránka stihla plně načíst a vyrenderovat
        setTimeout(function () {
            // Najdeme sekci komentářů
            const commentsSection = document.getElementById('comments-container');
            if (commentsSection) {
                // Odscrolujeme k sekci komentářů s offset pro lepší viditelnost
                commentsSection.scrollIntoView({ behavior: 'smooth', block: 'start' });
            }
        }, 100);
    }

    //BLOK NÍŽE JE SKRIPT PRO ZOBRAZENÍ OBRÁZKŮ V NOVÉM OKNĚ PO KLIKNUTÍ
    // Najít všechny obrázky v obsahu diskuze
    const images = document.querySelectorAll('.discussion-content img');
    // Přidat event listener pro kliknutí na každý obrázek
    images.forEach(function (img) {
        img.addEventListener('click', function () {
            // Otevřít obrázek v novém okně
            window.open(this.src, '_blank');
        });

        // Přidat title pro lepší UX
        if (!img.title) {
            img.title = 'Klikněte pro zobrazení v plné velikosti';
        }
    });

    // Zpracování odkazů v komentářích
    document.querySelectorAll('.komentar-wrapper p, .odpoved-na-komentar p').forEach(element => {
        element.innerHTML = linkifyText(element.innerHTML);
    });

    // BLOK KÓDU NÍŽE ZAJISTÍ SKRÝVÁNÍ A ODKRÝVÁNÍ TLAČÍTKA ODESLAT U NOVÉHO ROOR KOMENTÁŘE
    // A TLAČÍTKA ODPOVĚDĚT U REAKČNÍHO KOMENTÁŘ,
    // PŘI KLIKNUTÍ A ODKLIKNUTÍ Z TEXTOVÉHO POLE PRO VYTVIŘENÍ ROOT KOMENTÁŘE A REAKČNÍHO KOMENTÁŘE
    const textarea = document.getElementById('new-comment-textarea'); // Textového pole pro nový komentář
    const submitButton = document.getElementById('submit-comment-button'); // Tlačítko pro odeslání root komentáře

    //if (textarea && submitButton) {
    //    // Zobrazení tlačítka odeslat při kliknutí do textové oblasti pro přidání nového root komentáře
    //    textarea.addEventListener('focus', function () {
    //        submitButton.style.display = 'inline-block';
    //    });

    //    // Skrytí tlačítka odeslat root komentár při kliknutí mimo textovou oblast a tlačítko
    //    document.addEventListener('click', function (event) {
    //        // Nekryjeme tlačítko, pokud je textová oblast prázdná a má focus
    //        if (textarea.value.trim() !== '') {
    //            return;
    //        }
    //        // Skryjeme tlačítko, jen když uživatel kliknul mimo textovou oblast a tlačítko
    //        if (!textarea.contains(event.target) && !submitButton.contains(event.target)) {
    //            submitButton.style.display = 'none';
    //        }
    //    });
    //    // Pro lepší UX nový komentář: pokud uživatel začne psát, tlačítko zůstane viditelné
    //    textarea.addEventListener('input', function () {
    //        if (textarea.value.trim() !== '') {
    //            submitButton.style.display = 'inline-block';
    //        }
    //    });
    //}
    // Funkcionalita pro Reply (odpovědi na komentáře)
    // Jelikož je tlkačítek pro odpověď více, pracujeme s polem tlačítek a textových polí
    $('.reply-button').each(function (index) {

        const $replyButton = $(this); // tlačítko odpovědi
        const $replyArea = $('.new-comment-textarea-reply').eq(index); // Příslušná textová oblast pro odpověď
        const $submitReplyButton = $('.submit-comment-button-reply').eq(index); // Příslušné tlačítko odeslání odpovědi
        const $cancelReplyButton = $('.cancel-comment-button-reply').eq(index); // Příslušné tlačítko zrušení odpovědi

        // Při kliknutí na "Odpovědět" se zobrazí textové pole a tlačítka odeslat a zrušit, skryjeme tlačítko Odpovědět
        $replyButton.on('click', function (event) {
            event.stopPropagation(); // Zastavíme propagaci události, aby se udalost aktivovala pouze pro tento jeden konkretni element

            // Nejprve skryjeme všechny otevřené reply oblasti a zobrazíme všechna tlačítka odpovědět
            $('.new-comment-textarea-reply').hide();
            $('.submit-comment-button-reply').hide();
            $('.cancel-comment-button-reply').hide();
            $('.reply-button').show();

            $replyArea.show();
            $replyArea[0].focus();
            $submitReplyButton.show();
            $cancelReplyButton.show();
            $replyButton.hide();
        });

        // Tlačítko zrušit vrátí vše do původního stavu a vymaže obsah - zneviditelníé textareu a tlačítka odeslat a zrušit
        $cancelReplyButton.on('click', function (event) {
            event.stopPropagation(); // Zastavíme propagaci události,aby se udalost aktivovala pouze pro tento jeden konkretni element

            $replyArea.hide().val(''); // Zde vymažeme obsah, protože uživatel klikl na "zrušit"
            $submitReplyButton.hide();
            $cancelReplyButton.hide();
            $replyButton.show();
        });
    });
    // Otevreni nabidky smajliku - pridat komentar
    const emojiBtn = document.getElementById("emoji-btn");
    const emojiList = document.getElementById("emoji-list");
    const textareaKomentare = document.getElementById("new-comment-textarea");
    const poleSmajliku = document.querySelectorAll("#emoji-list .emoji");

    if (emojiBtn) {
        emojiBtn.addEventListener("click", () => {
            emojiList.style.display = emojiList.style.display === "block" ? "none" : "block";
        });
    }

    // Vlozeni smajlika do textarei pri kliknuti na smajlika
    poleSmajliku.forEach(smajlik => {
        smajlik.addEventListener("click", () => {
            // Získání aktuální hodnoty textarea
            const aktualni = textareaKomentare.value;
            // Získání pozice kurzoru
            const start = textareaKomentare.selectionStart;
            const end = textareaKomentare.selectionEnd;

            // Vložení emoji na pozici kurzoru
            textareaKomentare.value = aktualni.substring(0, start) + smajlik.textContent + aktualni.substring(end);

            // Nastavení kurzoru za vložený smajlík
            const newPosition = start + smajlik.textContent.length;
            textarea.setSelectionRange(newPosition, newPosition);

        });
    });

    // Otevreni nabidky smajliku - pridat reakci na komentar
    const emojiBtnsReply = document.querySelectorAll(".emoji-btn-comment-reply");

    emojiBtnsReply.forEach((emojiBtn, index) => {
        // Najdeme příslušný emoji list
        const form = emojiBtn.closest("form");
        const emojiListReply = form.querySelector(".emoji-list-comment-reply");

        if (!emojiListReply) return; // Pokud list neexistuje, přeskočíme

        // Najděme všechny emoji v tomto listu
        const allEmojis = emojiListReply.querySelectorAll(".emoji");

        // Přidáme event listener pro tlačítko zobrazení emoji
        emojiBtn.addEventListener("click", () => {
            emojiListReply.style.display = emojiListReply.style.display === "block" ? "none" : "block";
        });


        if (!form) return; // Pokud není formulář, přeskočíme

        const replyTextarea = form.querySelector(".new-comment-textarea-reply");

        if (!replyTextarea) return; // Pokud není textarea, přeskočíme

        // Přidáme event listener pro všechny emoji v tomto listu
        allEmojis.forEach(emoji => {
            emoji.addEventListener("click", (event) => {
                // Vložíme emoji do TÉTO konkrétní textarea

                // Získání aktuální hodnoty textarea
                const aktualni = replyTextarea.value;
                // Získání pozice kurzoru
                const start = replyTextarea.selectionStart;
                const end = replyTextarea.selectionEnd;

                // Vložení emoji na pozici kurzoru
                replyTextarea.value = aktualni.substring(0, start) + emoji.textContent + aktualni.substring(end);

                // Nastavení kurzoru za vložený smajlík
                const newPosition = start + emoji.textContent.length;
                replyTextarea.setSelectionRange(newPosition, newPosition);

                event.stopPropagation();
            });
        });
    });


});

async function loadMoreComments(discussionId) {
    const loadMoreBtn = document.getElementById('load-more-comments');
    const loadingSpinner = document.getElementById('loading-spinner');
    const container = document.getElementById('comments-list');
    const currentPage = parseInt(document.getElementById('current-page').value);

    try {
        // Zobrazení loading stavu
        loadMoreBtn.classList.add('d-none');
        loadingSpinner.classList.remove('d-none');

        // Tento kód volá handler OnGetNextPageAsync (z Category.cshtml) pro načtení dat další stránky komentářů z API
        // do response vrátí načtené komentáře (pro 1 stránku) plus informace ok stránkování
        // Je to konvence ASP.NET Core Razor Pages, že ?handler=NextPage zavolá C# metodu OnGetNextPageAsync...
        const response = await fetch(
            `?handler=NextPage&discussionId=${discussionId}&currentPage=${currentPage}`, {
            headers: {
                'X-Requested-With': 'XMLHttpRequest'
            }
        }
        );

        if (!response.ok) throw new Error('Načítání selhalo');

        // do data se dostane json s načtenými komentáři plus informace o stránkování
        const data = await response.json();

        // Načtení HTML pro každý komentář (root + reakční) pomocí Partial View
        for (const comment of data.comments) {
            // Pro každý root komentář z nově načtených dat zavolá handler pro vykreslení jednoho komentáře (root + reakční)
            // Tento AJAX požadavek volá handler OnPostDiscussionPartial (DiscussionModel z Discussion.cshtml.cs)
            const htmlResponse = await fetch(`?handler=CommentPartial`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'X-Requested-With': 'XMLHttpRequest'
                },
                body: JSON.stringify(comment)
            });

            if (htmlResponse.ok) {
                const html = await htmlResponse.text();
                container.insertAdjacentHTML('beforeend', html);
            }
        }

        // Aktualizace stavu stránkování
        document.getElementById('current-page').value = data.currentPage;

        // Zobrazení/skrytí tlačítka pro načtení dalších komentářů
        if (data.hasMoreComments) {
            loadMoreBtn.classList.remove('d-none');
        } else {
            document.getElementById('loading-container').remove();
        }
    } catch (error) {
        console.error('Chyba při načítání:', error);
        loadMoreBtn.classList.remove('d-none');
    } finally {
        loadingSpinner.classList.add('d-none');
    }
}

// Funkce pro přepínání typu diskuze mezi DiscussionType.Normal a DiscussionType.Top
async function toggleTopStatus(discussionId) {
    try {
        const apiBaseUrl = document.getElementById('apiBaseUrl').value;
        const response = await fetch(`${apiBaseUrl}/discussions/${discussionId}/toggle-top`, {
            method: 'PUT',
            headers: {
                'Authorization': `Bearer ${sessionStorage.getItem('JWTToken')}`
            }
        });

        if (response.ok) {
            location.reload();
        } else {
            alert('Nepodařilo se změnit TOP status diskuze.');
        }
    } catch (error) {
        console.error('Chyba při změně TOP statusu:', error);
        alert('Došlo k chybě při změně TOP statusu diskuze.');
    }
}

// Funkce pro přepínání typu diskuze mezi DiscussionType.Normal a DiscussionType.ForFriends
async function toggleForFriendsStatus(discussionId) {
    try {
        const apiBaseUrl = document.getElementById('apiBaseUrl').value;
        const response = await fetch(`${apiBaseUrl}/discussions/${discussionId}/toggle-forfriends`, {
            method: 'PUT',
            headers: {
                'Authorization': `Bearer ${sessionStorage.getItem('JWTToken')}`
            }
        });

        if (response.ok) {
            location.reload();
        } else {
            alert('Nepodařilo se změnit ForFriends status diskuze.');
        }
    } catch (error) {
        console.error('Chyba při změně ForFriends statusu:', error);
        alert('Došlo k chybě při změně ForFriends statusu diskuze.');
    }
}

/**
 * Funkce pro aktualizaci ikony srdce v tlačítku pro lajky.
 *
 * Aktualizuje ikonu srdce v tlačítku podle toho, zda uživatel dal lajk nebo ne.
 * Mění třídu ikony mezi bi-heart (nevyplněné srdce) a bi-heart-fill (vyplněné srdce)
 * a aktualizuje atribut title pro lepší přístupnost.
 *
 * @param {HTMLElement} button - Reference na tlačítko, jehož ikona má být aktualizována
 * @param {boolean} isFilled - True pro vyplněné srdce, false pro nevyplněné
 */
function updateHeartIcon(button, isFilled) {
    // Najde element s ikonou srdce
    const icon = button.querySelector('i.bi-heart, i.bi-heart-fill');

    if (icon) {
        if (isFilled) {
            // Změna na vyplněné srdce
            icon.classList.remove('bi-heart');
            icon.classList.add('bi-heart-fill');
            icon.setAttribute('title', 'Kliknutím odeberete srdce');
        } else {
            // Změna na nevyplněné srdce
            icon.classList.remove('bi-heart-fill');
            icon.classList.add('bi-heart');
            icon.setAttribute('title', 'Kliknutím přidáte srdce');
        }
    }
}

/**
 * Funkce pro přidání nebo odebrání lajku k diskuzi.
 *
 * Funkce asynchronně komunikuje s API endpointem pro správu lajků diskuze. Podle role uživatele a aktuálního stavu
 * lajku se chová dvěma způsoby:
 * 1. Pro adminy: Vždy přidává nový lajk (neomezené množství lajků)
 * 2. Pro běžné uživatele (Member): Funguje jako přepínač - pokud uživatel již dal lajk, odebere ho; pokud ještě nedal, přidá ho
 *
 * Funkce po úspěšné operaci aktualizuje počet lajků v UI a upraví vzhled tlačítka podle aktuálního stavu.
 *
 * @param {number} discussionId - ID diskuze, ke které se přidává/odebírá lajk
 */
async function likeDiscussion(discussionId) {
    try {
        const apiBaseUrl = document.getElementById('apiBaseUrl').value;
        const response = await fetch(`${apiBaseUrl}/discussions/${discussionId}/like`, {
            method: 'POST',
            headers: {
                'Authorization': `Bearer ${sessionStorage.getItem('JWTToken')}`,
                'Content-Type': 'application/json'
            }
        });

        if (response.ok) {
            const result = await response.json();
            // Aktualizace počítadla lajků
            document.getElementById('discussion-likes-count').textContent = result.likeCount;

            // Získání reference na tlačítko lajku
            const button = document.querySelector(`button[onclick="likeDiscussion(${discussionId})"]`);

            // Aktualizace stavu tlačítka podle odpovědi serveru
            if (result.hasUserLiked) {
                // Uživatel přidal lajk - nastavíme vyplněné srdce
                button.classList.remove('like-btn-disable');
                button.classList.add('like-btn');
                button.disabled = false;

                // Aktualizace ikony na vyplněné srdce
                const icon = button.querySelector('i');
                if (icon) {
                    icon.classList.remove('bi-heart');
                    icon.classList.add('bi-heart-fill');
                    icon.setAttribute('title', 'Kliknutím odeberete srdce');
                }
            } else {
                // Uživatel odebral lajk - nastavíme nevyplněné srdce
                button.classList.remove('like-btn-disable');
                button.classList.add('like-btn');
                button.disabled = false;

                // Aktualizace ikony na nevyplněné srdce
                const icon = button.querySelector('i');
                if (icon) {
                    icon.classList.remove('bi-heart-fill');
                    icon.classList.add('bi-heart');
                    icon.setAttribute('title', 'Kliknutím přidáte srdce');
                }
            }
        } else {
            alert('Nepodařilo se provést operaci s lajkem.');
        }
    } catch (error) {
        console.error('Chyba při operaci s lajkem:', error);
        alert('Došlo k chybě při operaci s lajkem.');
    }
}

/**
 * Funkce pro přidání nebo odebrání lajku ke komentáři.
 *
 * Funkce asynchronně komunikuje s API endpointem pro správu lajků komentáře. Podle role uživatele a aktuálního stavu
 * lajku se chová dvěma způsoby:
 * 1. Pro adminy: Vždy přidává nový lajk (neomezené množství lajků)
 * 2. Pro běžné uživatele (Member): Funguje jako přepínač - pokud uživatel již dal lajk, odebere ho; pokud ještě nedal, přidá ho
 *
 * Funkce po úspěšné operaci aktualizuje počet lajků v UI a upraví vzhled tlačítka podle aktuálního stavu.
 *
 * @param {number} discussionId - ID diskuze, ke které komentář patří
 * @param {number} commentId - ID komentáře, ke kterému se přidává/odebírá lajk
 */
async function likeComment(discussionId, commentId) {
    try {
        const apiBaseUrl = document.getElementById('apiBaseUrl').value;
        const response = await fetch(`${apiBaseUrl}/discussions/${discussionId}/comments/${commentId}/like`, {
            method: 'POST',
            headers: {
                'Authorization': `Bearer ${sessionStorage.getItem('JWTToken')}`,
                'Content-Type': 'application/json'
            }
        });

        if (response.ok) {
            const result = await response.json();
            // Aktualizace počítadla lajků
            document.getElementById(`comment-${commentId}-likes-count`).textContent = result.likeCount;

            // Získání reference na tlačítko lajku
            const button = document.querySelector(`button[onclick="likeComment(${discussionId}, ${commentId})"]`);

            // Aktualizace stavu tlačítka podle odpovědi serveru
            if (result.hasUserLiked) {
                // Uživatel přidal lajk - nastavíme vyplněné srdce
                button.classList.remove('like-btn-disable');
                button.classList.add('like-btn');
                button.disabled = false;
                updateHeartIcon(button, true);
            } else {
                // Uživatel odebral lajk - nastavíme nevyplněné srdce
                button.classList.remove('like-btn-disable');
                button.classList.add('like-btn');
                button.disabled = false;
                updateHeartIcon(button, false);
            }
        } else {
            alert('Nepodařilo se provést operaci s lajkem komentáře.');
        }
    } catch (error) {
        console.error('Chyba při operaci s lajkem komentáře:', error);
        alert('Došlo k chybě při operaci s lajkem komentáře.');
    }
}

// Funkce pro správu formuláře odpovědí
function showReplyForm(commentId) {
    document.querySelectorAll('[id^="reply-form-"]').forEach(form => {
        form.style.display = 'none';
    });

    const form = document.getElementById(`reply-form-${commentId}`);
    if (form) {
        form.style.display = 'block';
        form.querySelector('textarea').focus();
    }
}

function hideReplyForm(commentId) {
    const form = document.getElementById(`reply-form-${commentId}`);
    if (form) {
        form.style.display = 'none';
        form.querySelector('textarea').value = '';
    }
}

// Funkce pro získání kódu diskuze z URL adresy
function getDiscussionCodeFromUrl() {
    const urlParts = window.location.pathname.split('/');
    // URL má formát /Categories/{categoryCode}/{discussionCode}
    if (urlParts.length >= 3) {
        return urlParts[urlParts.length - 1];
    }
    return '';
}







/*
Funkce pro konverzi HTML před načtením do editoru
Funkce convertHtmlForCKEditor transformuje HTML obsah před jeho vložením do CKEditoru,
zejména převádí vložená YouTube videa z embedovaných iframe elementů do speciálního formátu, který CKEditor rozpozná jako media obsah
*/
function convertHtmlForCKEditor(html) {
    // Konvertování div.embed-responsive s iframe na data-oembed-url formát, který CKEditor rozpozná
    // Zachováváme třídy pro zarovnání
    return html.replace(
        /<div class="embed-responsive embed-responsive-16by9(?: text-center| image-style-align-center)?">\s*<iframe.*?src="https:\/\/www\.youtube\.com\/embed\/([a-zA-Z0-9_-]+)".*?><\/iframe>\s*<\/div>/g,
        function (match, videoId) {
            // Zjistíme, zda je video zarovnáno na střed
            const isCenter = match.includes('text-center') || match.includes('image-style-align-center');
            const styleClass = isCenter ? ' class="image-style-align-center"' : '';

            return `<figure${styleClass} class="media"><oembed url="https://www.youtube.com/watch?v=${videoId}"></oembed></figure>`;
        }
    );
}

/*
Funkce pro konverzi HTML zpět po uložení
Funkce convertHtmlFromCKEditor provádí opačný proces než funkce convertHtmlForCKEditor.
Převádí speciální formát CKEditoru zpět na standardní HTML s iframe embedů, což zajišťuje
kompatibilitu mezi interní reprezentací editoru a běžným HTML zobrazovaným v prohlížeči
*/
function convertHtmlFromCKEditor(html) {
    // Konvertování CKEditor formátu zpět na původní formát
    // Zachováváme třídy pro zarovnání
    return html.replace(
        /<figure(?:\s+class="([^"]*)")?\s*class="media">\s*<oembed url="https:\/\/www\.youtube\.com\/watch\?v=([a-zA-Z0-9_-]+)".*?><\/oembed>\s*<\/figure>/g,
        function (match, styleClass, videoId) {
            // Zjistíme, zda je video zarovnáno na střed
            const alignCenterClass = styleClass && styleClass.includes('image-style-align-center') ? ' text-center' : '';

            return `<div class="embed-responsive embed-responsive-16by9${alignCenterClass}"><iframe class="embed-responsive-item" src="https://www.youtube.com/embed/${videoId}" allowfullscreen></iframe></div>`;
        }
    );
}







/**
 * Funkce pro přepínání mezi režimem prohlížení a editace diskuze.
 *
 * Tato funkce řídí celý životní cyklus editačního režimu diskuze:
 * - Skrývá/zobrazuje příslušné UI elementy (nadpis, obsah, toolbar)
 * - Inicializuje CKEditor s odpovídající konfigurací při aktivaci editace
 * - Načítá původní obsah s konverzí z databázového formátu do CKEditor formátu
 * - Spravuje viditelnost selectu pro typ diskuze podle oprávnění uživatele
 * - Zajišťuje správné vyčištění CKEditor instance při zrušení editace
 * - Implementuje validaci délky obsahu v reálném čase
 *
 * Funkce používá globální proměnnou window.discussionEditor pro uchování
 * instance editoru a zajišťuje, že je vždy pouze jedna aktivní instance.
 *
 * @param {boolean} show - True pro aktivaci editačního režimu, false pro jeho ukončení
 */
function toggleDiscussionEdit(show) {
    // Získání referencí na všechny potřebné DOM elementy
    const titleDisplay = document.getElementById('discussion-title-display');
    const titleEdit = document.getElementById('discussion-title-edit');
    const contentDisplay = document.getElementById('discussion-content-display');
    const editorContainer = document.getElementById('editor-container');
    const toolbarContainer = document.getElementById('toolbar-container');
    const editBtn = document.getElementById('edit-discussion-btn');
    const saveBtn = document.getElementById('save-discussion-btn');
    const cancelBtn = document.getElementById('cancel-discussion-btn');
    const discussionTypeContainer = document.getElementById('discussion-type-select-container');
    const discussionTypeSelect = document.getElementById('editDiscussionType');
    const currentDiscussionType = document.getElementById('currentDiscussionType')?.value || "0";

    // Získání informace o oprávněních uživatele pro upload souborů
    const canUploadFiles = document.getElementById("discussion-settings").dataset.canUpload === "true";

    if (show) {
        // === AKTIVACE EDITAČNÍHO REŽIMU ===

        // Skrytí zobrazovacích elementů a zobrazení editačních
        titleDisplay.classList.add('d-none');
        titleEdit.classList.remove('d-none');
        contentDisplay.classList.add('d-none');
        editorContainer.classList.remove('d-none');
        toolbarContainer?.classList.remove('d-none');
        editBtn.classList.add('d-none');
        saveBtn.classList.remove('d-none');
        cancelBtn.classList.remove('d-none');

        // Zobrazení selectu pro typ diskuze podle oprávnění
        if (discussionTypeContainer) {
            const isAdmin = discussionTypeContainer.dataset.isAdmin === "true";
            const discussionType = parseInt(discussionTypeContainer.dataset.discussionType);

            // Select se zobrazí pouze adminům nebo pokud diskuze není skrytá (typ 2)
            if (isAdmin || discussionType !== 2) {
                discussionTypeContainer.classList.remove('d-none');

                // Nastavení aktuální hodnoty v selectu
                if (discussionTypeSelect) {
                    discussionTypeSelect.value = discussionType.toString();
                }
            }
        }

        // Nastavení hodnoty selectu podle aktuálního typu diskuze
        if (discussionTypeSelect && currentDiscussionType) {
            discussionTypeSelect.value = currentDiscussionType;
        }

        // Inicializace CKEditoru při prvním zobrazení editačního režimu
        if (!window.discussionEditor) {
            try {
                // Získání původního HTML obsahu a jeho konverze pro CKEditor
                const originalContent = contentDisplay.innerHTML;
                const convertedContent = convertHtmlForCKEditor(originalContent);

                // Vyčištění kontejneru editoru před inicializací
                editorContainer.innerHTML = '';

                // Vytvoření nové instance CKEditoru s příslušnou konfigurací
                ClassicEditor.create(
                    editorContainer,
                    createEditorConfig(canUploadFiles)
                ).then(editor => {
                    // Uložení reference na editor do globální proměnné
                    window.discussionEditor = editor;

                    // Načtení konvertovaného obsahu do editoru
                    editor.setData(convertedContent);

                    // Implementace validace maximální délky obsahu v reálném čase
                    const maxContentLength = 10000;
                    editor.model.document.on('change:data', () => {
                        const currentLength = editor.getData().length;

                        // Zobrazení varování při překročení limitu
                        if (currentLength > maxContentLength) {
                            document.getElementById("modalMessage").textContent =
                                `Obsah diskuze nesmí být delší než ${maxContentLength} znaků. Aktuální délka: ${currentLength}`;
                            new bootstrap.Modal(document.getElementById("errorModal")).show();
                        }
                    });

                }).catch(error => {
                    console.error('Chyba při inicializaci editoru:', error);
                    // Zobrazení chybové hlášky uživateli
                    alert('Nepodařilo se inicializovat editor. Zkuste to prosím znovu.');

                    // Návrat do zobrazovacího režimu při chybě
                    toggleDiscussionEdit(false);
                    return;
                });

            } catch (error) {
                console.error('Chyba při inicializaci editoru:', error);
                // Zobrazení chybové hlášky uživateli
                alert('Nepodařilo se inicializovat editor. Zkuste to prosím znovu.');

                // Návrat do zobrazovacího režimu při chybě
                toggleDiscussionEdit(false);
                return;
            }
        }

        // Zobrazení tlačítka pro emoji v editačním režimu
        const emojiBtn = document.getElementById("emoji-btn-discussion");
        if (emojiBtn) {
            emojiBtn.style.display = "inline-block";
        }

    } else {
        // === DEAKTIVACE EDITAČNÍHO REŽIMU ===

        // Zobrazení zobrazovacích elementů a skrytí editačních
        titleDisplay?.classList.remove('d-none');
        titleEdit?.classList.add('d-none');
        contentDisplay?.classList.remove('d-none');
        editorContainer?.classList.add('d-none');
        toolbarContainer?.classList.add('d-none');
        editBtn?.classList.remove('d-none');
        saveBtn?.classList.add('d-none');
        cancelBtn?.classList.add('d-none');

        // Skrytí selectu pro typ diskuze
        if (discussionTypeContainer) {
            discussionTypeContainer.classList.add('d-none');
        }

        // Skrytí tlačítka pro emoji
        const emojiBtn = document.getElementById("emoji-btn-discussion");
        if (emojiBtn) {
            emojiBtn.style.display = "none";
        }

        // Bezpečné zrušení instance CKEditoru
        if (window.discussionEditor) {
            try {
                window.discussionEditor.destroy().then(() => {
                    window.discussionEditor = null;
                }).catch(error => {
                    console.error('Chyba při rušení instance editoru:', error);
                    // I při chybě nastavíme referenci na null
                    window.discussionEditor = null;
                });
            } catch (error) {
                console.error('Chyba při rušení instance editoru:', error);
                // I při chybě nastavíme referenci na null
                window.discussionEditor = null;
            }
        }
    }
}

/**
 * Uloží změny provedené v editoru diskuze na server.
 *
 * Funkce kompletně zpracovává proces ukládání upravené diskuze:
 * - Validuje délku obsahu před odesláním
 * - Získává a zpracovává obsah z CKEditoru pomocí processEditorContentBeforeSave
 * - Čistí nepoužívané obrázky z úložiště
 * - Odesílá PUT požadavek na API se všemi změnami (nadpis, obsah, typ)
 * - Aktualizuje zobrazení při úspěchu nebo zobrazí chybové hlášky při neúspěchu
 * - Spravuje přechod zpět do zobrazovacího režimu
 * - Provádí refresh stránky pro zobrazení aktuálního stavu
 *
 * Funkce používá async/await pro asynchronní operace a implementuje
 * robustní error handling pro různé typy chyb.
 *
 * @param {number} discussionId - ID diskuze, která se má uložit
 * @param {number} discussionType - Původní typ diskuze (fallback hodnota)
 * @param {Event} event - Event objekt pro možnost preventDefault
 * @returns {Promise<boolean>} True při úspěchu, false při chybě
 */
async function saveDiscussionChanges(discussionId, discussionType, event) {
    // Získání referencí na potřebné DOM elementy
    const titleEdit = document.getElementById('discussion-title-edit');
    const contentDisplay = document.getElementById('discussion-content-display');
    const apiBaseUrl = document.getElementById('apiBaseUrl').value;
    const discussionTypeSelect = document.getElementById('editDiscussionType');

    // Konstanta pro maximální délku obsahu
    const maxContentLength = 10000;

    try {
        // === VALIDACE A PŘÍPRAVA DAT ===

        // Získání obsahu z editoru s kontrolou existence instance
        let content = '';
        if (window.discussionEditor) {
            content = window.discussionEditor.getData();
        } else {
            throw new Error('Editor není inicializován');
        }

        // Validace délky obsahu
        if (content.length > maxContentLength) {
            document.getElementById("modalMessage").textContent =
                `Obsah diskuze nesmí být delší než ${maxContentLength} znaků. Aktuální délka: ${content.length}`;
            new bootstrap.Modal(document.getElementById("errorModal")).show();

            if (event) {
                event.preventDefault();
            }
            return false;
        }

        // Zpracování obsahu pro zachování zarovnání obrázků a videí
        content = processEditorContentBeforeSave(content);

        // Získání vybraného typu diskuze
        let selectedDiscussionType;
        if (discussionTypeSelect && !discussionTypeSelect.closest('.d-none')) {
            selectedDiscussionType = parseInt(discussionTypeSelect.value);
        } else {
            // Použití původního typu pokud select není dostupný
            selectedDiscussionType = discussionType;
        }

        // === CLEANUP NEPOUŽÍVANÝCH OBRÁZKŮ ===

        cleanupUnusedImages(content).catch(cleanupError => {
            console.warn('Nepodařilo se vyčistit nepoužívané obrázky:', cleanupError);
            // Pokračujeme v ukládání i při chybě cleanup
        });

        // === ODESLÁNÍ NA SERVER ===

        // Příprava dat pro PUT požadavek
        const requestData = {
            title: titleEdit.value.trim(),
            content: content,
            type: selectedDiscussionType,
            voteType: 0 // Zachování současného stavu hlasování
        };

        // Odeslání PUT požadavku na API
        const response = await fetch(`${apiBaseUrl}/discussions/${discussionId}`, {
            method: 'PUT',
            headers: {
                'Authorization': `Bearer ${sessionStorage.getItem('JWTToken')}`,
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(requestData)
        });

        // === ZPRACOVÁNÍ ODPOVĚDI ===

        if (response.ok) {
            // Úspěšné uložení - aktualizace zobrazení
            contentDisplay.innerHTML = content;

            // Deaktivace editačního režimu
            toggleDiscussionEdit(false);

            // Aktualizace timestamp posledních úprav
            try {
                const now = new Date();
                const formatter = new Intl.DateTimeFormat('cs-CZ', {
                    year: 'numeric',
                    month: '2-digit',
                    day: '2-digit',
                    hour: '2-digit',
                    minute: '2-digit',
                    hour12: false
                });
                const formatted = formatter.format(now).replace(/\u00A0/g, ' ');
                const updateElement = document.getElementById('discussionUpdatedAtValue');
                if (updateElement) {
                    updateElement.textContent = formatted;
                }
            } catch (dateError) {
                console.warn('Nepodařilo se aktualizovat datum úprav:', dateError);
            }

            // Reload stránky pro zobrazení aktuálního stavu
            window.location.reload(true);
            return true;

        } else {
            // Zpracování chybové odpovědi ze serveru
            try {
                const errorData = await response.json();
                console.error("Chyba při ukládání:", errorData);

                // Zobrazení specifické chybové hlášky pokud je k dispozici
                if (errorData?.errors?.Content?.length > 0) {
                    alert(errorData.errors.Content[0]);
                } else if (errorData?.message) {
                    alert(errorData.message);
                } else {
                    alert('Nepodařilo se uložit změny.');
                }
            } catch (parseError) {
                // Pokud nelze parsovat JSON odpověď
                console.error('Chyba při parsování odpovědi:', parseError);
                alert('Nepodařilo se uložit změny. Zkuste to prosím znovu.');
            }
            return false;
        }

    } catch (error) {
        // Zpracování obecných chyb
        console.error('Chyba při ukládání diskuze:', error);

        // Zobrazení obecné chybové hlášky
        alert('Došlo k neočekávané chybě při ukládání změn. Zkuste to prosím znovu.');
        return false;
    }
}

/**
 * Zpracovává zrušení editace diskuze s vyčištěním nepoužívaných obrázků.
 *
 * Funkce se stará o bezpečné ukončení editačního režimu při zrušení změn:
 * - Získává původní obsah diskuze pro identifikaci použitých obrázků
 * - Volá cleanup funkci pro smazání dočasně nahraných obrázků
 * - Deaktivuje editační režim pomocí toggleDiscussionEdit
 * - Implementuje error handling pro případ selhání cleanup operací
 *
 * Tato funkce je důležitá pro správu úložiště obrázků a prevenci
 * accumulation nepoužívaných souborů.
 *
 * @returns {Promise<void>} Promise který se vyřeší po dokončení všech operací
 */
function handleCancelEditDiscussionClick() {
    try {
        // Získání původního obsahu pro cleanup nepoužívaných obrázků
        const originalContent = document.getElementById('discussion-content-display').innerHTML;

        // Vyčištění nepoužívaných obrázků před ukončením editace
        cleanupUnusedImages(originalContent).catch(cleanupError => {
            console.warn('Nepodařilo se vyčistit nepoužívané obrázky při zrušení editace:', cleanupError);
        });

        // Deaktivace editačního režimu
        toggleDiscussionEdit(false);

    } catch (error) {
        console.error('Chyba při zrušení editace diskuze:', error);

        // Pokus o force ukončení editačního režimu i při chybě
        try {
            toggleDiscussionEdit(false);
        } catch (forceError) {
            console.error('Nepodařilo se force ukončit editační režim:', forceError);
            // Reload stránky jako poslední možnost
            window.location.reload();
        }
    }
}

/**
 * Vyčišťuje nepoužívané obrázky z úložiště na základě aktuálního obsahu.
 *
 * Funkce analyzuje poskytnutý HTML obsah a identifikuje všechny obrázky,
 * které jsou v něm skutečně použity. Poté pošle seznam na server,
 * který smaže všechny ostatní obrázky z adresáře diskuze.
 *
 * Proces zahrnuje:
 * - Extrakci všech img elementů z HTML obsahu pomocí regex
 * - Filtrování pouze obrázků patřících k aktuální diskuzi
 * - Odeslání seznamu používaných obrázků na server
 * - Server response obsahuje informaci o úspěchu/neúspěchu operace
 *
 * Funkce je navržena tak, aby byla odolná vůči chybám - pokud
 * cleanup selže, operace pokračuje bez přerušení workflow.
 *
 * @param {string} newContent - HTML obsah obsahující odkazy na obrázky
 * @returns {Promise<boolean>} True při úspěchu, false při chybě
 */
async function cleanupUnusedImages(newContent) {
    try {
        // Získání kódu diskuze z URL pro identifikaci příslušných obrázků
        const discussionCode = window.location.pathname.split('/').pop();

        // Regex pro nalezení všech img elementů v HTML obsahu
        const imagesRegex = /<img[^>]+src="([^"]+)"[^>]*>/g;
        let match;
        const currentImages = new Set();

        // Extrakce URL všech obrázků z obsahu
        while ((match = imagesRegex.exec(newContent)) !== null) {
            const imageUrl = match[1];

            // Zpracování pouze obrázků patřících k této diskuzi
            if (imageUrl.includes(`/uploads/discussions/${discussionCode}/`)) {
                const fileName = imageUrl.split('/').pop();
                if (fileName) {
                    currentImages.add(fileName);
                }
            }
        }

        // Převod Set na pole pro odeslání v JSON
        const currentImagesArray = Array.from(currentImages);

        // Odeslání seznamu aktuálně používaných obrázků na server
        const response = await fetch(`/upload/delete-files?discussionCode=${discussionCode}`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'Authorization': `Bearer ${sessionStorage.getItem('JWTToken')}`
            },
            body: JSON.stringify(currentImagesArray)
        });

        // Zpracování odpovědi ze serveru
        if (response.ok) {
            const result = await response.json();
            if (result.success) {
                return true;
            } else {
                console.warn('Server reportoval chybu při mazání nepoužívaných obrázků:', result.error);
                return false;
            }
        } else {
            console.warn('HTTP chyba při komunikaci se serverem pro mazání obrázků:', response.status);
            return false;
        }

    } catch (error) {
        console.error('Chyba při cleanup nepoužívaných obrázků:', error);
        return false;
    }
}




// Funkce pro změnu viditelnosti diskuze
async function toggleDiscussionVisibility(discussionId) {
    try {
        const apiBaseUrl = document.getElementById('apiBaseUrl').value;
        const response = await fetch(`${apiBaseUrl}/discussions/${discussionId}/toggle-visibility`, {
            method: 'PUT',
            headers: {
                'Authorization': `Bearer ${sessionStorage.getItem('JWTToken')}`
            }
        });

        if (response.ok) {
            location.reload();
        } else {
            alert('Nepodařilo se změnit viditelnost diskuze.');
        }
    } catch (error) {
        console.error('Chyba při změně viditelnosti:', error);
        alert('Došlo k chybě při změně viditelnosti diskuze.');
    }
}

// Funkce pro změnu viditelnosti komentáře
async function toggleCommentVisibility(discussionId, commentId) {
    try {
        const apiBaseUrl = document.getElementById('apiBaseUrl').value;
        const response = await fetch(`${apiBaseUrl}/discussions/${discussionId}/comments/${commentId}/toggle-visibility`, {
            method: 'PUT',
            headers: {
                'Authorization': `Bearer ${sessionStorage.getItem('JWTToken')}`
            }
        });

        if (response.ok) {
            location.reload();
        } else {
            alert('Nepodařilo se změnit viditelnost komentáře.');
        }
    } catch (error) {
        console.error('Chyba při změně viditelnosti:', error);
        alert('Došlo k chybě při změně viditelnosti komentáře.');
    }
}

/**
 * Funkce pro smazání komentáře
 * @param {number} discussionId - ID diskuze, ve které se komentář nachází
 * @param {number} commentId - ID komentáře, který má být smazán
 */
async function deleteComment(discussionId, commentId) {
    // Zobrazení potvrzovacího dialogu
    if (!confirm('Opravdu chcete smazat tento komentář?')) {
        return; // Uživatel zrušil akci
    }

    try {
        const apiBaseUrl = document.getElementById('apiBaseUrl').value;
        const response = await fetch(`${apiBaseUrl}/discussions/${discussionId}/comments/${commentId}`, {
            method: 'DELETE',
            headers: {
                'Authorization': `Bearer ${sessionStorage.getItem('JWTToken')}`
            }
        });

        if (response.ok) {
            // Pokud se smazání podařilo, obnovíme stránku pro zobrazení aktuálního stavu
            location.reload();
        } else {
            // Pokud nastala chyba, zobrazíme uživateli chybovou hlášku
            alert('Nepodařilo se smazat komentář.');
        }
    } catch (error) {
        console.error('Chyba při mazání komentáře:', error);
        alert('Došlo k chybě při mazání komentáře.');
    }
}

// Funkce pro potvrzení změny kategorie diskuze (admin má tuto možnost na detailu diskuze, tato funkce je je pro závěrečné potvrzení)
function confirmCategoryChange() {
    const select = document.getElementById('categorySelect');
    if (!select) return true; // Pokud element neexistuje, necháme formulář odeslat

    const selectedOption = select.options[select.selectedIndex];

    // Zjistíme ID aktuální kategorie z toho, která možnost je označená jako selected
    let currentCategoryId = null;
    for (let i = 0; i < select.options.length; i++) {
        if (select.options[i].hasAttribute('selected')) {
            currentCategoryId = select.options[i].value;
            break;
        }
    }

    // Pokud není vybrána žádná kategorie nebo je vybrána stejná, zabráníme odeslání
    if (!select.value || select.value === currentCategoryId) {
        return false;
    }

    return confirm(`Opravdu chcete přesunout diskuzi do kategorie "${selectedOption.text}"?`);
}

/**
 * Inicializace emoji funkcionalita pro diskuze.
 *
 * Tento blok kódu se stará o funkcionalita vkládání emoji do editoru diskuze:
 * - Nastavuje event listenery pro tlačítko zobrazení emoji
 * - Přepíná viditelnost seznamu emoji
 * - Vkládá vybrané emoji do aktivního CKEditoru
 *
 * Kód se spustí při načtení stránky a čeká na interakci uživatele.
 * Emoji se vkládají přímo do CKEditoru pomocí jeho model API.
 */

// Získání referencí na emoji elementy pro diskuzi
const emojiBtnDiskuse = document.getElementById("emoji-btn-discussion"); // Tlačítko pro zobrazení emoji
const poleSmajlikuDiskuse = document.querySelectorAll("#emoji-list-discussion .emoji"); // Všechny emoji v nabídce
const emojiListDiskuse = document.getElementById("emoji-list-discussion"); // Kontejner s emoji

// Event listener pro zobrazení/skrytí seznamu emoji
if (emojiBtnDiskuse && emojiListDiskuse) {
    emojiBtnDiskuse.addEventListener("click", () => {
        emojiListDiskuse.style.display = emojiListDiskuse.style.display === "block" ? "none" : "block";
    });
}

// Event listenery pro vložení emoji do editoru při kliknutí
poleSmajlikuDiskuse.forEach(smajlik => {
    smajlik.addEventListener("click", () => {
        // Kontrola, zda globální instance CKEditoru existuje a je připravena
        if (window.discussionEditor) {
            const emoji = smajlik.textContent;

            // Použití CKEditor 5 model API pro vložení emoji na pozici kurzoru
            window.discussionEditor.model.change(writer => {
                window.discussionEditor.model.insertContent(writer.createText(emoji));
            });

            // Skrytí seznamu emoji po výběru pro lepší UX
            emojiListDiskuse.style.display = "none";
        }
    });
});