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


    emojiBtn.addEventListener("click", () => {
        emojiList.style.display = emojiList.style.display === "block" ? "none" : "block";
    });
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

// Funkce pro nahrazení ikony nevyplnenho srdce za vyplnene srdce po pridani lajku
// vola se pri pridani lajku k diskusi nebo ke komentari
function replaceHeartIcon(button) {
    // Najde vnořený element i s třídou bi-heart
    const icon = button.querySelector('i.bi-heart');

    // Pokud byl nalezen element s třídou bi-heart
    if (icon) {
        // Odstraní třídu bi-heart
        icon.classList.remove('bi-heart');
        // Přidá třídu bi-heart-fill
        icon.classList.add('bi-heart-fill');

        // Volitelně změňte také title atribut pro lepší popis
        if (icon.hasAttribute('title')) {
            icon.setAttribute('title', 'Počet srdcí od uživatelů');
        }
    }
}

// Funkce pro přidání like k diskuzi
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
            document.getElementById('discussion-likes-count').textContent = result.likeCount;
            if (!result.canUserLike) {
                const button = document.querySelector(`button[onclick="likeDiscussion(${discussionId})"]`);
                button.disabled = true;
                button.classList.replace('like-btn', 'like-btn-disable');
                replaceHeartIcon(button);
            }
        } else {
            alert('Nepodařilo se přidat like.');
        }
    } catch (error) {
        console.error('Chyba při přidávání like:', error);
        alert('Došlo k chybě při přidávání like.');
    }
}

// Funkce pro přidání like ke komentáři
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
            document.getElementById(`comment-${commentId}-likes-count`).textContent = result.likeCount;
            if (!result.canUserLike) {
                const button = document.querySelector(`button[onclick="likeComment(${discussionId}, ${commentId})"]`);
                button.disabled = true;
                button.classList.replace('like-btn', 'like-btn-disable');
                replaceHeartIcon(button);
            }
        } else {
            alert('Nepodařilo se přidat like.');
        }
    } catch (error) {
        console.error('Chyba při přidávání like:', error);
        alert('Došlo k chybě při přidávání like.');
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







// Funkce pro konverzi HTML před načtením do editoru
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

// Funkce pro konverzi HTML zpět po uložení
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

// Aktualizovaná funkce toggleDiscussionEdit
async function toggleDiscussionEdit(show) {
    const titleDisplay = document.getElementById('discussion-title-display');
    const titleEdit = document.getElementById('discussion-title-edit');
    const contentDisplay = document.getElementById('discussion-content-display');
    const editorContainer = document.getElementById('editor-container');
    const toolbarContainer = document.getElementById('toolbar-container');
    const editBtn = document.getElementById('edit-discussion-btn');
    const saveBtn = document.getElementById('save-discussion-btn');
    const cancelBtn = document.getElementById('cancel-discussion-btn');
    // Získání informace, zda uživatel může nahrávat soubory
    const canUploadFiles = document.getElementById("discussion-settings").dataset.canUpload === "true";

    // reference na container se selectem pro typ diskuze
    const discussionTypeContainer = document.getElementById('discussion-type-select-container');

    // Reference na select pro typ diskuze
    const discussionTypeSelect = document.getElementById('editDiscussionType');

    // Získání aktuálního typu diskuze z hidden inputu
    const currentDiscussionType = document.getElementById('currentDiscussionType')?.value || "0";

    if (show) {
        titleDisplay.classList.add('d-none');
        titleEdit.classList.remove('d-none');
        contentDisplay.classList.add('d-none');
        editorContainer.classList.remove('d-none');
        toolbarContainer?.classList.remove('d-none');
        editBtn.classList.add('d-none');
        saveBtn.classList.remove('d-none');
        cancelBtn.classList.remove('d-none');

        // Zobrazíme select pro typ diskuze při editaci pouze pokud je uživatel admin
        // nebo pokud diskuze není typu Hidden (2)
        if (discussionTypeContainer) {
            const isAdmin = discussionTypeContainer.dataset.isAdmin === "true";
            const discussionType = parseInt(discussionTypeContainer.dataset.discussionType);

            // Pokud je uživatel admin nebo diskuze není typu Hidden, zobrazíme select
            if (isAdmin || discussionType !== 2) {
                discussionTypeContainer.classList.remove('d-none');

                // Nastavíme aktuální hodnotu v selectu
                const discussionTypeSelect = document.getElementById('editDiscussionType');
                if (discussionTypeSelect) {
                    discussionTypeSelect.value = discussionType.toString();
                }
            }
        }

        // Nastavíme vybranou hodnotu v selectu podle aktuálního typu diskuze
        if (discussionTypeSelect && currentDiscussionType) {
            discussionTypeSelect.value = currentDiscussionType;
        }

        // Inicializace editoru při prvním zobrazení
        if (!window.discussionEditor) {
            // Konvertujeme HTML obsah pro editor
            const originalContent = document.getElementById('discussion-content-display').innerHTML;
            const convertedContent = convertHtmlForCKEditor(originalContent);

            // Příprava editoru
            editorContainer.innerHTML = '';

            ClassicEditor
                .create(document.getElementById('editor-container'), createEditorConfig(canUploadFiles))
                .then(editor => {
                    window.discussionEditor = editor;

                    // Nastavení počáteční hodnoty editoru s konvertovaným obsahem
                    editor.setData(convertedContent);

                    // Přidání kontroly maximální délky
                    const maxContentLength = 10000; // Odpovídá omezení v modelu
                    editor.model.document.on('change:data', () => {
                        const currentLength = editor.getData().length;
                        if (currentLength > maxContentLength) {
                            // Zobrazení varování
                            document.getElementById("modalMessage").textContent =
                                `Obsah diskuze nesmí být delší než ${maxContentLength} znaků. Aktuální délka: ${currentLength}`;
                            new bootstrap.Modal(document.getElementById("errorModal")).show();
                        }
                    });
                })
                .catch(error => {
                    console.error('Chyba při inicializaci editoru:', error);
                });
        }
    } else {
        titleDisplay?.classList.remove('d-none');
        titleEdit?.classList.add('d-none');
        contentDisplay?.classList.remove('d-none');
        editorContainer?.classList.add('d-none');
        toolbarContainer?.classList.add('d-none');
        editBtn?.classList.remove('d-none');
        saveBtn?.classList.add('d-none');
        cancelBtn?.classList.add('d-none');

        if (discussionTypeContainer) {
            discussionTypeContainer.classList.add('d-none');
        }

        // Zrušení instance editoru při zrušení editace
        if (window.discussionEditor) {
            try {
                // Pokud je instance editoru aktivní, zrušte ji
                window.discussionEditor.destroy();
                window.discussionEditor = null;
            } catch (error) {
                console.error('Chyba při rušení instance editoru:', error);
            }
        }
    }
    $("#emoji-btn-discussion").toggle();
}

// Funkce pro uložení změn v diskuzi
async function saveDiscussionChanges(discussionId, discussionType, event) {
    const titleEdit = document.getElementById('discussion-title-edit');
    const contentDisplay = document.getElementById('discussion-content-display');
    const apiBaseUrl = document.getElementById('apiBaseUrl').value;
    const maxContentLength = 10000;

    try {
        // Získáme obsah z editoru
        let content = window.discussionEditor ? window.discussionEditor.getData() : '';

        if (content.length > maxContentLength) {
            document.getElementById("modalMessage").textContent =
                `Obsah diskuze nesmí být delší než ${maxContentLength} znaků. Aktuální délka: ${content.length}`;
            new bootstrap.Modal(document.getElementById("errorModal")).show();
            if (event) {
                event.preventDefault();
            }
            return false;
        }

        // Zpracování obsahu pro zachování zarovnání obrázků
        content = processEditorContentBeforeSave(content);

        // Před uložením zkontrolujeme nepoužívané obrázky a smažeme je
        await cleanupUnusedImages(content);

        // Získání vybraného typu diskuze z selectu
        const discussionTypeSelect = document.getElementById('editDiscussionType');
        let selectedDiscussionType;

        // Kontrola, zda je select viditelný a existuje
        if (discussionTypeSelect && !discussionTypeSelect.closest('.d-none')) {
            selectedDiscussionType = parseInt(discussionTypeSelect.value);
        } else {
            // Pokud select neexistuje nebo není viditelný, použijeme původní typ diskuze
            selectedDiscussionType = discussionType;
        }

        // Odeslání požadavku na server s aktualizovanými daty
        const response = await fetch(`${apiBaseUrl}/discussions/${discussionId}`, {
            method: 'PUT',
            headers: {
                'Authorization': `Bearer ${sessionStorage.getItem('JWTToken')}`,
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                title: titleEdit.value,
                content: content,
                type: selectedDiscussionType,
                voteType: 0 // Zachováme současný stav hlasování
            })
        });

        if (response.ok) {
            contentDisplay.innerHTML = content;
            toggleDiscussionEdit(false);

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
                const formatted = formatter.format(now).replace(/\u00A0/g, '');
                document.getElementById('discussionUpdatedAtValue').textContent = formatted;

                // Způsobí kompletní znovunačtení stránky v prohlížeči
                window.location.reload(true);
                return;
            }
            catch (error) {
                document.getElementById('discussionUpdatedAtValue').textContent = '';
            }
        } else {
            try {
                const errorData = await response.json();
                console.error("Chyba při ukládání:", errorData); // Pro diagnostiku
                if (errorData && errorData.errors && errorData.errors.Content) {
                    alert(errorData.errors.Content[0]);
                } else {
                    alert('Nepodařilo se uložit změny.');
                }
            } catch (e) {
                alert('Nepodařilo se uložit změny.');
            }
        }
    }
    catch (error) {
        console.error('Chyba při ukládání:', error);
        alert('Došlo k chybě při ukládání změn.');
    }
}

// Vlozeni smajliku do diskuse
const emojiBtnDiskuse = document.getElementById("emoji-btn-discussion"); // btn smajlika
const poleSmajlikuDiskuse = document.querySelectorAll("#emoji-list-discussion .emoji"); // pole vsech smajliku v nabidce
const emojiListDiskuse = document.getElementById("emoji-list-discussion");

emojiBtnDiskuse.addEventListener("click", () => {
    emojiListDiskuse.style.display = emojiListDiskuse.style.display === "block" ? "none" : "block";
});
// Vložení smajlíka do editoru při kliknutí
poleSmajlikuDiskuse.forEach(smajlik => {
    smajlik.addEventListener("click", () => {
        // Kontrola, zda globální instance CKEditoru existuje
        if (window.discussionEditor) {
            const emoji = smajlik.textContent;

            // Použití API CKEditoru 5 pro vložení emoji
            window.discussionEditor.model.change(writer => {
                window.discussionEditor.model.insertContent(writer.createText(emoji));
            });

        }
    });
});

// Upravená funkce pro kontrolu a mazání nepoužívaných obrázků, která bere v úvahu vložená YouTube videa
async function cleanupUnusedImages(newContent) {
    try {
        // Získání kódu diskuze z URL
        const discussionCode = window.location.pathname.split('/').pop();

        // Optimalizovaná regex pro nalezení všech obrázků
        const imagesRegex = /<img[^>]+src="([^"]+)"[^>]*>/g;
        let match;
        const currentImages = new Set();

        // Extrahujeme URL všech obrázků z nového obsahu
        while ((match = imagesRegex.exec(newContent)) !== null) {
            const imageUrl = match[1];
            // Zpracuj pouze obrázky patřící k této diskuzi
            if (imageUrl.includes(`/uploads/discussions/${discussionCode}/`)) {
                const fileName = imageUrl.split('/').pop();
                if (fileName) {
                    currentImages.add(fileName);
                }
            }
        }

        // Převedeme Set na pole pro odeslání v těle požadavku
        const currentImagesArray = Array.from(currentImages);

        // Pošleme seznam aktuálních obrázků na server k promazání ostatních
        const response = await fetch(`/upload/delete-files?discussionCode=${discussionCode}`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'Authorization': `Bearer ${sessionStorage.getItem('JWTToken')}`
            },
            body: JSON.stringify(currentImagesArray) // Odesíláme přímo pole
        });

        if (response.ok) {
            const result = await response.json();
            if (result.success) {
                return true;
            } else {
                console.warn('Při mazání nepoužívaných obrázků došlo k chybě:', result.error);
                return true; // Pokračujeme i když mazání selže
            }
        } else {
            console.warn('Chyba při komunikaci se serverem pro mazání obrázků:', response.status);
            return true; // Pokračujeme i když mazání selže
        }

    } catch (error) {
        console.error('Chyba při kontrole a mazání nepoužívaných obrázků:', error);
        return true; // Pokračujeme i když mazání selže
    }
}

// Obsluha události při zrušení editace diskuze
async function handleCancelEditDiscussionClick() {
    const originalContent = document.getElementById('discussion-content-display').innerHTML;

    // Skrytí select boxu pro typ diskuze
    /*const discussionTypeContainer = document.getElementById('discussion-type-select-container');
    if (discussionTypeContainer) {
        discussionTypeContainer.classList.add('d-none');
    }*/

    // Volání standardní funkce pro ukončení editace
    toggleDiscussionEdit(false);

    // Vyčištění nepoužívaných obrázků
    await cleanupUnusedImages(originalContent);
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