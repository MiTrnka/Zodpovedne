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

    // BLOK KÓDU NÍŽE ZAJISTÍ SKRÝVÁNÍ A ODKRÝVÁNÍ TLAČÍTKA ODESLAT U NOVÉHO KOMENTÁŘE, PŘI KLIKNUTÍ A ODKLIKNUTÍ Z TEXTOVÉHO POLE
    const textarea = document.getElementById('new-comment-textarea');
    const submitButton = document.getElementById('submit-comment-button');
    if (textarea && submitButton) {
        // Zobrazení tlačítka při kliknutí do textové oblasti
        textarea.addEventListener('focus', function () {
            submitButton.style.display = 'inline-block';
        });
        // Skrytí tlačítka při kliknutí mimo textovou oblast a tlačítko
        document.addEventListener('click', function (event) {
            // Nekryjeme tlačítko, pokud je textová oblast prázdná a má focus
            if (textarea.value.trim() !== '') {
                return;
            }
            // Skryjeme tlačítko, jen když uživatel kliknul mimo textovou oblast a tlačítko
            if (!textarea.contains(event.target) && !submitButton.contains(event.target)) {
                submitButton.style.display = 'none';
            }
        });
        // Pro lepší UX: pokud uživatel začne psát, tlačítko zůstane viditelné
        textarea.addEventListener('input', function () {
            if (textarea.value.trim() !== '') {
                submitButton.style.display = 'inline-block';
            }
        });
    }
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
            // Tento AJAX požadavek volá handler OnPostDiscussionPartialAsync (DiscussionModel z Discussion.cshtml.cs)
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
                button.classList.replace('btn-outline-primary', 'btn-outline-secondary');
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
                button.classList.replace('btn-outline-primary', 'btn-outline-secondary');
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

// Funkce pro editaci diskuze
function toggleDiscussionEdit(show) {
    const titleDisplay = document.getElementById('discussion-title-display');
    const titleEdit = document.getElementById('discussion-title-edit');
    const contentDisplay = document.getElementById('discussion-content-display');
    const toolbarContainer = document.getElementById('toolbar-container');
    const editorContainer = document.getElementById('editor-container');
    const editBtn = document.getElementById('edit-discussion-btn');
    const saveBtn = document.getElementById('save-discussion-btn');
    const cancelBtn = document.getElementById('cancel-discussion-btn');

    if (show) {
        titleDisplay.classList.add('d-none');
        titleEdit.classList.remove('d-none');
        contentDisplay.classList.add('d-none');
        toolbarContainer.classList.remove('d-none');
        editorContainer.classList.remove('d-none');
        editBtn.classList.add('d-none');
        saveBtn.classList.remove('d-none');
        cancelBtn.classList.remove('d-none');


        // Inicializace editoru při prvním zobrazení
        if (!window.discussionEditor) {
            DecoupledEditor
                .create(document.querySelector('#editor-container'), {
                    toolbar: ['bold', '|', 'bulletedList', 'numberedList'],
                    language: 'cs'
                })
                .then(editor => {
                    window.discussionEditor = editor;
                    // Přidání kontroly maximální délky
                    /*const maxContentLength = 3000; // Odpovídá omezení v modelu

                    editor.model.document.on('change:data', () => {
                        const currentLength = editor.getData().length;

                        if (currentLength > maxContentLength) {
                            // Zobrazení varování
                            alert(`Obsah diskuze nesmí být delší než ${maxContentLength} znaků. Aktuální délka: ${currentLength}`);
                        }
                    });*/
                    const toolbarContainer = document.querySelector('#toolbar-container');
                    toolbarContainer.appendChild(editor.ui.view.toolbar.element);
                });
        }
    } else {
        titleDisplay.classList.remove('d-none');
        titleEdit.classList.add('d-none');
        contentDisplay.classList.remove('d-none');
        toolbarContainer.classList.add('d-none');
        editorContainer.classList.add('d-none');
        editBtn.classList.remove('d-none');
        saveBtn.classList.add('d-none');
        cancelBtn.classList.add('d-none');
    }
}

// Funkce pro uložení změn v diskuzi
async function saveDiscussionChanges(discussionId, discussionType) {
    const titleEdit = document.getElementById('discussion-title-edit');
    const contentDisplay = document.getElementById('discussion-content-display');
    const apiBaseUrl = document.getElementById('apiBaseUrl').value;
    const maxContentLength = 3000;

    try {
        const content = window.discussionEditor ? window.discussionEditor.getData() : '';
        if (content.length > maxContentLength) {
            document.getElementById("modalMessage").textContent =
                `Obsah diskuze nesmí být delší než ${maxContentLength} znaků. Aktuální délka: ${content.length}`;
            new bootstrap.Modal(document.getElementById("errorModal")).show();
            event.preventDefault(); // Zabrání odeslání formuláře
            return false; // Zabránit odeslání formuláře
        }
        const response = await fetch(`${apiBaseUrl}/discussions/${discussionId}`, {
            method: 'PUT',
            headers: {
                'Authorization': `Bearer ${sessionStorage.getItem('JWTToken')}`,
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                title: titleEdit.value,
                content: content,
                type: discussionType
            })
        });

        if (response.ok) {
            contentDisplay.innerHTML = content;
            toggleDiscussionEdit(false);
            // Nastavení data a času aktualizace pro rychlé zobrazení na FE (po refreshi se zahodí a použije se to z BE)
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
            }
            catch (error) {
                document.getElementById('discussionUpdatedAtValue').textContent = '';
            }
        } else {
            // Zkusíme přečíst detaily chyby
            const errorData = await response.json();
            if (errorData && errorData.errors && errorData.errors.Content) {
                // Zobrazíme specifickou chybu pro délku obsahu
                alert(errorData.errors.Content[0]);
            } else {
                // Obecná chybová hláška
                alert('Nepodařilo se uložit změny.');
            }
        }
    }
    catch (error) {
        console.error('Chyba při ukládání:', error);
        alert('Došlo k chybě při ukládání změn.');
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