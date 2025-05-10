/**
 * Skript pro správu hlasování v diskuzích
 *
 * Tento soubor obsluhuje funkcionalitu přidávání, editace a mazání
 * hlasovacích otázek při vytváření nebo úpravě diskuze.
 * Zároveň obsahuje funkce pro zobrazení a hlasování na stránce detailu diskuze.
 *
 * Funkce pro stránku CreateDiscussion:
 * - Přidání nové otázky
 * - Mazání otázek
 * - Aktualizace číslování otázek
 * - Počítání znaků v textu otázky
 * - Validace hlasovacích formulářů
 *
 * Funkce pro stránku Discussion:
 * - Načtení hlasování pro aktuální diskuzi
 * - Zobrazení otázek a výsledků hlasování
 * - Odeslání hlasů uživatele
 * - Aktualizace UI podle stavu hlasování
 * - Vytváření a editace hlasování z detailu diskuze
 */

document.addEventListener('DOMContentLoaded', function () {
    // ========================================================================
    // ČÁST 1: Funkce pro stránku CreateDiscussion - vytváření hlasovacích otázek
    // ========================================================================

    // Základní prvky pro práci s hlasováním na stránce CreateDiscussion
    const hasVotingCheckbox = document.getElementById('hasVoting');
    const votingSection = document.getElementById('voting-section');
    const addQuestionBtn = document.getElementById('add-question-btn');
    const questionsContainer = document.getElementById('voting-questions-container');
    const noQuestionsMessage = document.getElementById('no-questions-message');
    const questionTemplate = document.getElementById('question-template');
    const votingTypeSelect = document.getElementById('voting-type');

    // Sledování globálního stavu otázek
    let questionCounter = 0;

    // Inicializace pro CreateDiscussion - zobrazení/skrytí sekce hlasování podle výchozího stavu checkboxu
    if (hasVotingCheckbox && votingSection) {
        votingSection.style.display = hasVotingCheckbox.checked ? 'block' : 'none';
        if (hasVotingCheckbox.checked) {
            // Pokud je výchozí stav zapnutý, inicializujeme existující otázky
            initExistingQuestions();
        }
    }

    // Přidání posluchače událostí pro přepínač hlasování na stránce CreateDiscussion
    if (hasVotingCheckbox && votingSection) {
        hasVotingCheckbox.addEventListener('change', function () {
            votingSection.style.display = this.checked ? 'block' : 'none';

            // Aktualizace hodnoty VoteType podle stavu checkboxu
            if (votingTypeSelect) {
                votingTypeSelect.value = this.checked ? "1" : "0"; // 1 = Visible, 0 = None
            }

            // Pokud se sekce zobrazuje a není ještě žádná otázka, zobrazíme zprávu
            updateNoQuestionsMessage();
        });
    }

    // Přidání posluchače události pro tlačítko "Přidat otázku" na stránce CreateDiscussion
    if (addQuestionBtn) {
        addQuestionBtn.addEventListener('click', addNewQuestion);
    }

    // Inicializace existujících otázek (pokud existují) na stránce CreateDiscussion
    function initExistingQuestions() {
        // Tento kód by načetl existující otázky z modelu
        // Pro první verzi implementace předpokládáme, že vytváříme novou diskuzi bez otázek

        // Pokud by existovala data z modelu, zde bychom je načetli
        // const existingQuestions = JSON.parse(document.getElementById('existing-questions-data')?.value || '[]');

        // Pro každou existující otázku bychom zavolali funkci addNewQuestion s jejími daty
        // existingQuestions.forEach(question => {
        //     addNewQuestion(null, question);
        // });

        updateNoQuestionsMessage();
    }

    // Funkce pro přidání nové otázky na stránce CreateDiscussion
    function addNewQuestion(event, questionData = null) {
        // Zvýšení počítadla pro unikátní ID
        questionCounter++;

        // Skrytí zprávy "Žádné otázky"
        if (noQuestionsMessage) noQuestionsMessage.style.display = 'none';

        // Klonování šablony otázky
        if (!questionTemplate) return;

        const template = questionTemplate.content.cloneNode(true);
        const questionElement = template.querySelector('.voting-question');

        // Přidání ID pro identifikaci v DOM
        questionElement.id = `question-${questionCounter}`;

        // Nastavení čísla otázky
        template.querySelector('.question-number').textContent = `Otázka #${getQuestionCount() + 1}`;

        // Nastavení pořadí (automaticky další v pořadí)
        const orderInput = template.querySelector('.question-order');
        orderInput.value = getQuestionCount() + 1;

        // Nastavení hodnot z existujících dat (pokud existují)
        if (questionData) {
            template.querySelector('.question-text').value = questionData.text || '';
            template.querySelector('.question-order').value = questionData.displayOrder || getQuestionCount();

            // Pokud má otázka ID (při editaci), nastavíme ho do skrytého pole
            if (questionData.id) {
                template.querySelector('.question-id').value = questionData.id;
            }
        }

        // Přidání posluchačů událostí pro nové prvky
        const deleteBtn = template.querySelector('.remove-question-btn');
        deleteBtn.addEventListener('click', function () {
            removeQuestion(questionElement);
        });

        // Přidání počítadla znaků do textového pole
        const textArea = template.querySelector('.question-text');
        const charCount = template.querySelector('.char-count');

        textArea.addEventListener('input', function () {
            updateCharCount(this, charCount);
        });

        // Inicializace počítadla znaků
        updateCharCount(textArea, charCount);

        // Přidání otázky do kontejneru
        if (questionsContainer) questionsContainer.appendChild(questionElement);

        // Aktualizace číslování všech otázek
        updateQuestionNumbers();

        // Nastavení fokusu na textové pole nové otázky
        setTimeout(() => {
            textArea.focus();
        }, 0);

        // Aktualizace hodnoty pole VoteType
        if (votingTypeSelect && getQuestionCount() > 0 && hasVotingCheckbox && hasVotingCheckbox.checked) {
            votingTypeSelect.value = "1"; // Viditelné hlasování
        }

        return questionElement;
    }

    // Funkce pro odstranění otázky na stránce CreateDiscussion
    function removeQuestion(questionElement) {
        if (confirm('Opravdu chcete odstranit tuto otázku?')) {
            questionElement.remove();
            updateQuestionNumbers();
            updateNoQuestionsMessage();

            // Pokud už nejsou žádné otázky a hlasování je stále povoleno,
            // upozorníme uživatele nebo změníme hodnotu na "Bez hlasování"
            if (getQuestionCount() === 0 && hasVotingCheckbox && hasVotingCheckbox.checked) {
                if (votingTypeSelect && votingTypeSelect.value !== "0") {
                    alert('Pro aktivaci hlasování je potřeba přidat alespoň jednu otázku.');
                }
            }
        }
    }

    // Funkce pro aktualizaci počítadla znaků na stránce CreateDiscussion
    function updateCharCount(textarea, countElement) {
        if (!textarea || !countElement) return;

        const maxLength = parseInt(textarea.getAttribute('maxlength') || '400');
        const currentLength = textarea.value.length;

        countElement.textContent = currentLength;

        // Vizuální indikace blížícího se limitu
        if (currentLength > maxLength * 0.9) {
            countElement.classList.add('text-danger');
        } else {
            countElement.classList.remove('text-danger');
        }
    }

    // Funkce pro aktualizaci číslování otázek na stránce CreateDiscussion
    function updateQuestionNumbers() {
        if (!document.querySelector('.voting-question')) return;

        const questions = document.querySelectorAll('.voting-question');

        questions.forEach((question, index) => {
            const numberElement = question.querySelector('.question-number');
            if (numberElement) {
                numberElement.textContent = `Otázka #${index + 1}`;
            }

            // Aktualizace výchozích hodnot pořadí pro nové otázky
            const orderInput = question.querySelector('.question-order');
            if (orderInput && orderInput.value === '') {
                orderInput.value = index + 1;
            }
        });
    }

    // Funkce pro zjištění počtu otázek na stránce CreateDiscussion
    function getQuestionCount() {
        return document.querySelectorAll('.voting-question').length;
    }

    // Funkce pro zobrazení/skrytí zprávy "Žádné otázky" na stránce CreateDiscussion
    function updateNoQuestionsMessage() {
        if (!noQuestionsMessage) return;

        if (getQuestionCount() === 0) {
            noQuestionsMessage.style.display = 'block';
        } else {
            noQuestionsMessage.style.display = 'none';
        }
    }

    // Funkce pro přípravu dat hlasovacích otázek před odesláním formuláře na stránce CreateDiscussion
    function prepareVotingData() {
        if (!hasVotingCheckbox || !hasVotingCheckbox.checked) {
            return null;
        }

        const questions = document.querySelectorAll('.voting-question');
        const votingData = [];

        questions.forEach((question, index) => {
            const textElement = question.querySelector('.question-text');
            const orderElement = question.querySelector('.question-order');
            const idElement = question.querySelector('.question-id');

            if (textElement && orderElement) {
                const questionData = {
                    id: idElement && idElement.value ? parseInt(idElement.value) : null,
                    text: textElement.value.trim(),
                    displayOrder: parseInt(orderElement.value) || index + 1
                };

                votingData.push(questionData);
            }
        });

        return votingData;
    }

    // Funkce pro validaci dat hlasování na stránce CreateDiscussion
    function validateVotingData() {
        if (!hasVotingCheckbox || !hasVotingCheckbox.checked) {
            return true; // Hlasování není aktivní, validace není potřeba
        }

        // Pokud je v selectu hodnota "Žádné hlasování", také validace projde
        if (votingTypeSelect && votingTypeSelect.value === "0") {
            return true; // Hlasování je vypnuté
        }

        const questions = document.querySelectorAll('.voting-question');

        // Pokud je hlasování aktivní, ale nemá žádné otázky, je to chyba
        if (questions.length === 0 && votingTypeSelect && votingTypeSelect.value !== "0") {
            document.getElementById("modalMessage").textContent =
                "Pro vytvoření hlasování je potřeba přidat alespoň jednu otázku.";
            new bootstrap.Modal(document.getElementById("errorModal")).show();
            return false;
        }

        // Validace každé otázky
        let isValid = true;
        questions.forEach((question, index) => {
            const textElement = question.querySelector('.question-text');
            const orderElement = question.querySelector('.question-order');

            if (!textElement.value.trim()) {
                document.getElementById("modalMessage").textContent =
                    `Otázka #${index + 1} nemá vyplněný text.`;
                new bootstrap.Modal(document.getElementById("errorModal")).show();
                isValid = false;
                return;
            }

            const orderValue = parseInt(orderElement.value) || 0;
            if (orderValue <= 0) {
                document.getElementById("modalMessage").textContent =
                    `Otázka #${index + 1} má neplatné pořadí. Hodnota musí být kladné číslo.`;
                new bootstrap.Modal(document.getElementById("errorModal")).show();
                isValid = false;
                return;
            }
        });

        return isValid;
    }

    // Připojení validace k odesílání formuláře na stránce CreateDiscussion
    const discussionForm = document.getElementById('create-discussion-form');
    if (discussionForm) {
        discussionForm.addEventListener('submit', function (event) {
            // Validujeme pouze pokud je checkbox zaškrtnutý a není vybráno "Žádné hlasování"
            if (hasVotingCheckbox && hasVotingCheckbox.checked &&
                votingTypeSelect && votingTypeSelect.value !== "0") {
                if (!validateVotingData()) {
                    event.preventDefault();
                    return false;
                }
            }

            // Pokud je hlasování povoleno, připravíme data pro odeslání
            if (hasVotingCheckbox && hasVotingCheckbox.checked) {
                const votingQuestions = prepareVotingData();

                // Připojení dat hlasování k formuláři
                const questionsInput = document.createElement('input');
                questionsInput.type = 'hidden';
                questionsInput.name = 'VotingQuestions';
                questionsInput.value = JSON.stringify(votingQuestions);
                discussionForm.appendChild(questionsInput);

                // Připojení hodnoty VoteType k formuláři
                if (votingTypeSelect) {
                    const voteTypeInput = document.createElement('input');
                    voteTypeInput.type = 'hidden';
                    voteTypeInput.name = 'Input.VoteType';
                    voteTypeInput.value = votingTypeSelect.value;
                    discussionForm.appendChild(voteTypeInput);
                }
            }
        });
    }
    // ========================================================================
    // ČÁST 2: Funkce pro stránku Discussion - zobrazení hlasování a hlasování
    // ========================================================================

    // Zjištění, zda je na stránce sekce hlasování pro stránku Discussion
    const discussionVotingSection = document.getElementById('voting-section');
    if (discussionVotingSection) {
        // Základní elementy pro stránku Discussion
        const loadingElement = document.getElementById('voting-loading');
        const discussionQuestionsContainer = document.getElementById('voting-questions-container');
        const submitButton = document.getElementById('submit-votes-btn');

        // Získání ID diskuze z URL nebo z JavaScriptu
        const discussionId = getDiscussionIdFromPage();

        // Načtení hlasování při načtení stránky, pokud máme ID diskuze
        if (discussionId) {
            loadVoting(discussionId);
        }

        // Event listener pro odeslání hlasů
        if (submitButton) {
            submitButton.addEventListener('click', function () {
                submitVotes(discussionId);
            });
        }
    }

    /**
     * Načtení hlasování pro danou diskuzi
     * @param {number} discussionId - ID diskuze
     */
    async function loadVoting(discussionId) {
        try {
            const apiBaseUrl = document.getElementById('apiBaseUrl').value;
            const url = `${apiBaseUrl}/votings/discussion/${discussionId}`;

            const headers = {};
            const token = sessionStorage.getItem('JWTToken');
            if (token) {
                headers['Authorization'] = `Bearer ${token}`;
            }

            const response = await fetch(url, {
                method: 'GET',
                headers: headers
            });

            if (!response.ok) {
                // Pokud se nepodařilo načíst hlasování, skryjeme sekci
                const votingSection = document.getElementById('voting-section');
                if (votingSection) votingSection.style.display = 'none';
                console.error('Nepodařilo se načíst hlasování');
                return;
            }

            const votingData = await response.json();

            // Zobrazení dat hlasování
            displayVoting(votingData);

        } catch (error) {
            console.error('Chyba při načítání hlasování:', error);
            const votingSection = document.getElementById('voting-section');
            if (votingSection) votingSection.style.display = 'none';
        }
    }

    /**
     * Zobrazení hlasování v UI
     * @param {object} votingData - Data hlasování z API
     */
    function displayVoting(votingData) {
        const loadingElement = document.getElementById('voting-loading');
        const questionsContainer = document.getElementById('voting-questions-container');
        const isUserLoggedIn = document.getElementById('is-user-logged-in').value === 'true';

        if (!loadingElement || !questionsContainer) return;

        // Skrytí načítacího indikátoru
        loadingElement.classList.add('d-none');

        // Zobrazení kontejneru otázek
        questionsContainer.classList.remove('d-none');

        // Vyprázdnění kontejneru otázek
        questionsContainer.innerHTML = '';

        // Výběr šablony podle typu hlasování a přihlášení uživatele
        const canVote = votingData.voteType === 1 && isUserLoggedIn; // VoteType.Visible = 1 AND user is logged in

        // Vytvoření HTML pro každou otázku
        votingData.questions.forEach((question, index) => {
            const questionElement = document.createElement('div');
            questionElement.className = 'mb-4 pb-3 border-bottom';

            // Přidání číslování otázek
            const questionNumber = index + 1;

            // Vytvoření nadpisu otázky
            const questionTitle = document.createElement('h5');
            questionTitle.className = 'mb-3';
            questionTitle.textContent = `${questionNumber}. ${question.text}`;
            questionElement.appendChild(questionTitle);

            if (canVote) {
                // Vytvoření formuláře pro hlasování (radio buttony) - pouze pro přihlášené uživatele
                questionElement.appendChild(createVotingForm(question));
            } else {
                // Vytvoření zobrazení výsledku hlasování (progress bary) - pro všechny
                questionElement.appendChild(createVotingResults(question));
            }

            // Přidání otázky do kontejneru
            questionsContainer.appendChild(questionElement);
        });
    }

    /**
     * Vytvoření formuláře pro hlasování k otázce
     * @param {object} question - Data otázky
     * @returns {HTMLElement} - Element formuláře s radio buttony
     */
    function createVotingForm(question) {
        const formContainer = document.createElement('div');
        formContainer.className = 'voting-options';

        // Unikátní jméno skupiny radio buttonů pro tuto otázku
        const radioGroupName = `vote-question-${question.id}`;

        // Vytvoření wrapper divu pro radio buttony
        const wrapperContainer = document.createElement('div');
        wrapperContainer.className = 'vote-form-wrapper';

        // Vytvoření radio buttonů pro možnosti Ano, Ne, Nehlasuji
        const options = [
            { value: 'true', label: 'Ano', checked: question.currentUserVote === true },
            { value: 'false', label: 'Ne', checked: question.currentUserVote === false },
            { value: 'null', label: 'Nehlasuji', checked: question.currentUserVote === null }
        ];

        options.forEach(option => {
            const optionContainer = document.createElement('div');
            optionContainer.className = 'form-check mb-2';

            const radioInput = document.createElement('input');
            radioInput.className = 'form-check-input';
            radioInput.type = 'radio';
            radioInput.name = radioGroupName;
            radioInput.id = `${radioGroupName}-${option.value}`;
            radioInput.value = option.value;
            radioInput.checked = option.checked;
            radioInput.setAttribute('data-question-id', question.id);

            const radioLabel = document.createElement('label');
            radioLabel.className = 'form-check-label';
            radioLabel.htmlFor = `${radioGroupName}-${option.value}`;
            radioLabel.textContent = option.label;

            optionContainer.appendChild(radioInput);
            optionContainer.appendChild(radioLabel);

            // Přidáváme jednotlivé form-check elementy do wrapperu
            wrapperContainer.appendChild(optionContainer);

        });

        // Přidáme wrapper s form-check elementy do hlavního containeru
        formContainer.appendChild(wrapperContainer);

        // Přidání aktuálních výsledků pod radio buttony
        const resultsContainer = document.createElement('div');
        resultsContainer.className = 'voting-results mt-3';
        resultsContainer.appendChild(createVotingResults(question));
        formContainer.appendChild(resultsContainer);


        return formContainer;
    }

    /**
     * Vytvoření zobrazení výsledků hlasování k otázce
     * @param {object} question - Data otázky
     * @returns {HTMLElement} - Element s progress bary výsledků
     */
    function createVotingResults(question) {
        const resultsContainer = document.createElement('div');
        resultsContainer.className = 'voting-results';

        // Vytvoření progress barů pro zobrazení výsledků
        // Progress bar pro Ano
        const yesContainer = document.createElement('div');
        yesContainer.className = 'mb-2';

        const yesLabel = document.createElement('div');
        yesLabel.className = 'd-flex justify-content-between mb-1';

        const yesLabelText = document.createElement('span');
        yesLabelText.textContent = 'Ano';

        const yesLabelCount = document.createElement('span');
        yesLabelCount.textContent = `${question.yesVotes} (${question.yesPercentage}%)`;

        yesLabel.appendChild(yesLabelText);
        yesLabel.appendChild(yesLabelCount);
        yesContainer.appendChild(yesLabel);

        const yesProgressBarOuter = document.createElement('div');
        yesProgressBarOuter.className = 'progress';
        yesProgressBarOuter.style.height = '20px';

        const yesProgressBar = document.createElement('div');
        yesProgressBar.className = 'progress-bar bg-success';
        yesProgressBar.style.width = `${question.yesPercentage}%`;
        yesProgressBar.setAttribute('role', 'progressbar');
        yesProgressBar.setAttribute('aria-valuenow', question.yesPercentage);
        yesProgressBar.setAttribute('aria-valuemin', '0');
        yesProgressBar.setAttribute('aria-valuemax', '100');

        yesProgressBarOuter.appendChild(yesProgressBar);
        yesContainer.appendChild(yesProgressBarOuter);
        resultsContainer.appendChild(yesContainer);

        // Progress bar pro Ne
        const noContainer = document.createElement('div');
        noContainer.className = 'mb-2';

        const noLabel = document.createElement('div');
        noLabel.className = 'd-flex justify-content-between mb-1';

        const noLabelText = document.createElement('span');
        noLabelText.textContent = 'Ne';

        const noLabelCount = document.createElement('span');
        noLabelCount.textContent = `${question.noVotes} (${question.noPercentage}%)`;

        noLabel.appendChild(noLabelText);
        noLabel.appendChild(noLabelCount);
        noContainer.appendChild(noLabel);

        const noProgressBarOuter = document.createElement('div');
        noProgressBarOuter.className = 'progress';
        noProgressBarOuter.style.height = '20px';

        const noProgressBar = document.createElement('div');
        noProgressBar.className = 'progress-bar bg-danger';
        noProgressBar.style.width = `${question.noPercentage}%`;
        noProgressBar.setAttribute('role', 'progressbar');
        noProgressBar.setAttribute('aria-valuenow', question.noPercentage);
        noProgressBar.setAttribute('aria-valuemin', '0');
        noProgressBar.setAttribute('aria-valuemax', '100');

        noProgressBarOuter.appendChild(noProgressBar);
        noContainer.appendChild(noProgressBarOuter);
        resultsContainer.appendChild(noContainer);

        // Informace o celkovém počtu hlasů
        const totalVotesInfo = document.createElement('div');
        totalVotesInfo.className = 'small text-muted mt-1';
        totalVotesInfo.textContent = `Celkový počet hlasů: ${question.totalVotes}`;
        resultsContainer.appendChild(totalVotesInfo);

        return resultsContainer;
    }

    /**
     * Odeslání hlasů uživatele
     * @param {number} discussionId - ID diskuze
     */
    async function submitVotes(discussionId) {
        const submitButton = document.getElementById('submit-votes-btn');

        try {
            // Zobrazení načítacího stavu tlačítka
            if (submitButton) {
                submitButton.disabled = true;
                submitButton.innerHTML = '<span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span> Odesílání...';
            }

            // Získání všech radio buttonů
            const radioButtons = document.querySelectorAll('[data-question-id]:checked');

            // Vytvoření objektu s hlasy
            const votes = {};

            // Procházení všech zaškrtnutých radio buttonů
            radioButtons.forEach(radio => {
                const questionId = parseInt(radio.getAttribute('data-question-id'));
                const value = radio.value;

                // Přidání hlasu do objektu votes pouze pro odpovědi Ano nebo Ne
                if (value === 'true') {
                    votes[questionId] = true;
                } else if (value === 'false') {
                    votes[questionId] = false;
                }
                // Hodnotu "Nehlasuji" (null) ignorujeme, protože se nemá ukládat
            });

            // Příprava dat pro odeslání
            const votingData = {
                discussionId: discussionId,
                votes: votes
            };

            // Odeslání hlasů na server
            const apiBaseUrl = document.getElementById('apiBaseUrl').value;
            const response = await fetch(`${apiBaseUrl}/votings/submit`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Authorization': `Bearer ${sessionStorage.getItem('JWTToken')}`
                },
                body: JSON.stringify(votingData)
            });

            if (!response.ok) {
                throw new Error('Nepodařilo se odeslat hlasy');
            }

            // Zpracování odpovědi
            const result = await response.json();

            // Aktualizace UI s novými výsledky
            displayVoting(result);

            // Zobrazení úspěšné zprávy
            showSuccessMessage();

        } catch (error) {
            console.error('Chyba při odesílání hlasů:', error);
            showErrorMessage();
        } finally {
            // Obnovení stavu tlačítka
            if (submitButton) {
                submitButton.disabled = false;
                submitButton.textContent = 'Hlasovat';
            }
        }
    }

    /**
     * Zobrazení zprávy o úspěšném hlasování
     */
    function showSuccessMessage() {
        const submitButton = document.getElementById('submit-votes-btn');

        // Vytvoření zprávy
        const messageContainer = document.createElement('div');
        messageContainer.className = 'alert alert-success mt-3';
        messageContainer.setAttribute('role', 'alert');
        messageContainer.textContent = 'Váš hlas byl úspěšně zaznamenán. Děkujeme za účast v hlasování!';

        // Přidání zprávy nad tlačítko
        if (submitButton && submitButton.parentNode) {
            submitButton.parentNode.insertBefore(messageContainer, submitButton);

            // Automatické skrytí zprávy po 5 sekundách
            setTimeout(() => {
                messageContainer.remove();
            }, 5000);
        }
    }

    /**
     * Zobrazení chybové zprávy při selhání hlasování
     */
    function showErrorMessage() {
        const submitButton = document.getElementById('submit-votes-btn');

        // Vytvoření zprávy
        const messageContainer = document.createElement('div');
        messageContainer.className = 'alert alert-danger mt-3';
        messageContainer.setAttribute('role', 'alert');
        messageContainer.textContent = 'Nepodařilo se zaznamenat váš hlas. Zkuste to prosím znovu.';

        // Přidání zprávy nad tlačítko
        if (submitButton && submitButton.parentNode) {
            submitButton.parentNode.insertBefore(messageContainer, submitButton);

            // Automatické skrytí zprávy po 5 sekundách
            setTimeout(() => {
                messageContainer.remove();
            }, 5000);
        }
    }

    /**
     * Získání ID diskuze ze stránky
     * @returns {number} ID diskuze
     */
    function getDiscussionIdFromPage() {
        // Různé způsoby, jak získat ID diskuze

        // 1. Z globální proměnné window.discussionId
        if (typeof window.discussionId !== 'undefined' && window.discussionId) {
            return parseInt(window.discussionId);
        }

        // 2. Z ID v elementu
        const discussionIdInput = document.querySelector('[name="discussionId"]');
        if (discussionIdInput && discussionIdInput.value) {
            return parseInt(discussionIdInput.value);
        }

        // 3. Z URL parametru
        const urlMatch = window.location.href.match(/discussionId=(\d+)/);
        if (urlMatch && urlMatch[1]) {
            return parseInt(urlMatch[1]);
        }

        // 4. Z objektu v diskuzi
        if (typeof Discussion !== 'undefined' && Discussion.Id) {
            return parseInt(Discussion.Id);
        }

        // 5. Z URL cesty
        const pathParts = window.location.pathname.split('/');
        const lastPart = pathParts[pathParts.length - 1];
        if (/^\d+$/.test(lastPart)) {
            return parseInt(lastPart);
        }

        // Pokud nic nenajdeme, vrátíme null
        console.warn('Nepodařilo se najít ID diskuze');
        return null;
    }

    // ========================================================================
    // ČÁST 3: Funkce pro správu hlasování na stránce Discussion - vytváření a editace
    // ========================================================================

    // Získání elementů pro modální okno hlasování na stránce Discussion
    const votingModal = document.getElementById('voting-modal');
    const votingTypeSelect_modal = document.getElementById('voting-type-modal');
    const questionsContainer_modal = document.getElementById('voting-questions-modal-list');
    const noQuestionsMessage_modal = document.getElementById('no-questions-message-modal');
    const questionTemplate_modal = document.getElementById('question-template-modal');
    const addQuestionBtn_modal = document.getElementById('add-question-btn-modal');
    const saveVotingBtn = document.getElementById('save-voting-btn');
    const createVotingBtn = document.getElementById('create-voting-btn');
    const editVotingBtn = document.getElementById('edit-voting-btn');

    // Počítadlo pro unikátní ID otázek v modálním okně
    let modalQuestionCounter = 0;

    // Pokud nejsou dostupné elementy pro správu hlasování v modálu, část kódu přeskočíme
    if (votingModal) {
        // Event listener pro změnu typu hlasování v modálu
        if (votingTypeSelect_modal) {
            votingTypeSelect_modal.addEventListener('change', function () {
                const questionsContainer = document.getElementById('voting-questions-modal-container');
                if (questionsContainer) {
                    // Pokud je vybrána hodnota 0 (Žádné hlasování), skryjeme sekci otázek
                    if (this.value === "0") {
                        questionsContainer.classList.add('d-none');
                    } else {
                        questionsContainer.classList.remove('d-none');
                        updateNoQuestionsMessageModal();
                    }
                }
            });
        }

        // Event listener pro tlačítko přidání otázky v modálu
        if (addQuestionBtn_modal) {
            addQuestionBtn_modal.addEventListener('click', addNewQuestionModal);
        }

        // Event listener pro tlačítko uložení hlasování v modálu
        if (saveVotingBtn) {
            saveVotingBtn.addEventListener('click', saveVotingChanges);
        }

        // Event listener pro otevření modálu - načítá aktuální hlasování, pokud existuje
        if (votingModal) {
            votingModal.addEventListener('show.bs.modal', function () {
                // Resetujeme obsah modálu
                if (questionsContainer_modal) {
                    questionsContainer_modal.innerHTML = '';
                    if (noQuestionsMessage_modal) {
                        questionsContainer_modal.appendChild(noQuestionsMessage_modal);
                    }
                }

                // Nastavení počátečního stavu - pro nové hlasování nastavit viditelné
                if (votingTypeSelect_modal) {
                    // Pro novou diskuzi přednastavíme Viditelné
                    if (document.getElementById('create-voting-btn')) {
                        votingTypeSelect_modal.value = "1"; // Viditelné pro nové hlasování

                        // Ujistíme se, že je container pro otázky viditelný
                        const questionsContainer = document.getElementById('voting-questions-modal-container');
                        if (questionsContainer) {
                            questionsContainer.classList.remove('d-none');
                        }
                    }
                }

                // Načteme aktuální hlasování, pokud existuje
                const discussionId = getDiscussionIdFromPage();
                if (discussionId) {
                    loadVotingForEditing(discussionId);
                }
            });
        }
    }

    /**
     * Načtení hlasování pro editaci v modálním okně
     * @param {number} discussionId - ID diskuze
     */
    async function loadVotingForEditing(discussionId) {
        if (!questionsContainer_modal) return;

        try {
            const apiBaseUrl = document.getElementById('apiBaseUrl').value;
            const url = `${apiBaseUrl}/votings/discussion/${discussionId}`;

            const headers = {};
            const token = sessionStorage.getItem('JWTToken');
            if (token) {
                headers['Authorization'] = `Bearer ${token}`;
            }

            // Nejprve nastavíme stav načítání
            questionsContainer_modal.innerHTML = `
                <div class="text-center py-3">
                    <div class="spinner-border spinner-border-sm" role="status">
                        <span class="visually-hidden">Načítání...</span>
                    </div>
                    <span class="ms-2">Načítání hlasování...</span>
                </div>
            `;

            const response = await fetch(url, {
                method: 'GET',
                headers: headers
            });

            // Vyčistíme kontejner otázek
            questionsContainer_modal.innerHTML = '';
            if (noQuestionsMessage_modal) {
                questionsContainer_modal.appendChild(noQuestionsMessage_modal);
            }

            // Pokud je odpověď 404, znamená to, že hlasování neexistuje
            if (response.status === 404) {
                // Nastavíme výchozí stav pro nové hlasování
                if (votingTypeSelect_modal) {
                    votingTypeSelect_modal.value = "1"; // Výchozí hodnota je "Viditelné"
                }
                return;
            }

            // Pro jiné chyby zobrazíme upozornění
            if (!response.ok) {
                alert('Nepodařilo se načíst hlasování pro editaci.');
                return;
            }

            const votingData = await response.json();

            // Nastavíme typ hlasování v selectu
            if (votingTypeSelect_modal) {
                votingTypeSelect_modal.value = votingData.voteType.toString();
            }

            // Zobrazíme nebo skryjeme sekci otázek podle typu hlasování
            const questionsModalContainer = document.getElementById('voting-questions-modal-container');
            if (questionsModalContainer) {
                if (votingData.voteType === 0) {
                    questionsModalContainer.classList.add('d-none');
                } else {
                    questionsModalContainer.classList.remove('d-none');
                }
            }

            // Přidáme otázky do modálu
            if (votingData.questions && votingData.questions.length > 0) {
                // Odstraníme zprávu o žádných otázkách
                if (noQuestionsMessage_modal) {
                    noQuestionsMessage_modal.style.display = 'none';
                }

                // Pro každou otázku vytvoříme element v modálu
                votingData.questions.forEach(question => {
                    addNewQuestionModal(null, {
                        id: question.id,
                        text: question.text,
                        displayOrder: question.displayOrder
                    });
                });
            }

        } catch (error) {
            console.error('Chyba při načítání hlasování pro editaci:', error);
            alert('Nastala chyba při načítání hlasování.');
        }
    }

    /**
     * Přidání nové otázky do modálního okna
     * @param {Event} event - Událost kliknutí
     * @param {Object} questionData - Data otázky pro editaci
     */
    function addNewQuestionModal(event, questionData = null) {
        // Zvýšení počítadla pro unikátní ID
        modalQuestionCounter++;

        // Skrytí zprávy "Žádné otázky"
        if (noQuestionsMessage_modal) noQuestionsMessage_modal.style.display = 'none';

        // Klonování šablony otázky
        if (!questionTemplate_modal) return;

        const template = questionTemplate_modal.content.cloneNode(true);
        const questionElement = template.querySelector('.voting-question-modal');

        // Přidání ID pro identifikaci v DOM
        questionElement.id = `question-modal-${modalQuestionCounter}`;

        // Nastavení čísla otázky
        template.querySelector('.question-number-modal').textContent = `Otázka #${getQuestionCountModal() + 1}`;

        // Nastavení pořadí (automaticky další v pořadí)
        const orderInput = template.querySelector('.question-order-modal');
        orderInput.value = getQuestionCountModal() + 1;

        // Nastavení hodnot z existujících dat (pokud existují)
        if (questionData) {
            template.querySelector('.question-text-modal').value = questionData.text || '';
            template.querySelector('.question-order-modal').value = questionData.displayOrder || getQuestionCountModal() + 1;

            // Pokud má otázka ID (při editaci), nastavíme ho do skrytého pole
            if (questionData.id) {
                template.querySelector('.question-id-modal').value = questionData.id;
            }
        }

        // Přidání posluchačů událostí pro nové prvky
        const deleteBtn = template.querySelector('.remove-question-btn-modal');
        deleteBtn.addEventListener('click', function () {
            removeQuestionModal(questionElement);
        });

        // Přidání počítadla znaků do textového pole
        const textArea = template.querySelector('.question-text-modal');
        const charCount = template.querySelector('.char-count-modal');

        textArea.addEventListener('input', function () {
            updateCharCountModal(this, charCount);
        });

        // Inicializace počítadla znaků
        updateCharCountModal(textArea, charCount);

        // Přidání otázky do kontejneru
        if (questionsContainer_modal) questionsContainer_modal.appendChild(questionElement);

        // Aktualizace číslování všech otázek
        updateQuestionNumbersModal();

        // Nastavení fokusu na textové pole nové otázky
        setTimeout(() => {
            textArea.focus();
        }, 0);

        return questionElement;
    }

    /**
     * Odstranění otázky z modálního okna
     * @param {HTMLElement} questionElement - Element otázky
     */
    function removeQuestionModal(questionElement) {
        if (confirm('Opravdu chcete odstranit tuto otázku?')) {
            questionElement.remove();
            updateQuestionNumbersModal();
            updateNoQuestionsMessageModal();

            // Pokud už nejsou žádné otázky a hlasování je stále povoleno,
            // upozorníme uživatele
            if (getQuestionCountModal() === 0 && votingTypeSelect_modal && votingTypeSelect_modal.value !== "0") {
                alert('Pro aktivaci hlasování je potřeba přidat alespoň jednu otázku.');
            }
        }
    }

    /**
     * Aktualizace počítadla znaků v modálním okně
     * @param {HTMLTextAreaElement} textarea - Textové pole
     * @param {HTMLElement} countElement - Element počítadla
     */
    function updateCharCountModal(textarea, countElement) {
        if (!textarea || !countElement) return;

        const maxLength = parseInt(textarea.getAttribute('maxlength') || '400');
        const currentLength = textarea.value.length;

        countElement.textContent = currentLength;

        // Vizuální indikace blížícího se limitu
        if (currentLength > maxLength * 0.9) {
            countElement.classList.add('text-danger');
        } else {
            countElement.classList.remove('text-danger');
        }
    }

    /**
     * Aktualizace číslování otázek v modálním okně
     */
    function updateQuestionNumbersModal() {
        if (!document.querySelector('.voting-question-modal')) return;

        const questions = document.querySelectorAll('.voting-question-modal');

        questions.forEach((question, index) => {
            const numberElement = question.querySelector('.question-number-modal');
            if (numberElement) {
                numberElement.textContent = `Otázka #${index + 1}`;
            }

            // Aktualizace výchozích hodnot pořadí pro nové otázky
            const orderInput = question.querySelector('.question-order-modal');
            if (orderInput && orderInput.value === '') {
                orderInput.value = index + 1;
            }
        });
    }

    /**
     * Zjištění počtu otázek v modálním okně
     * @returns {number} Počet otázek
     */
    function getQuestionCountModal() {
        return document.querySelectorAll('.voting-question-modal').length;
    }

    /**
     * Aktualizace zprávy "Žádné otázky" v modálním okně
     */
    function updateNoQuestionsMessageModal() {
        if (!noQuestionsMessage_modal) return;

        if (getQuestionCountModal() === 0) {
            noQuestionsMessage_modal.style.display = 'block';
        } else {
            noQuestionsMessage_modal.style.display = 'none';
        }
    }

    /**
     * Příprava dat hlasovacích otázek před odesláním z modálního okna
     * @returns {Array} Otázky pro odeslání
     */
    function prepareVotingDataModal() {
        const questions = document.querySelectorAll('.voting-question-modal');
        const votingData = [];

        questions.forEach((question, index) => {
            const textElement = question.querySelector('.question-text-modal');
            const orderElement = question.querySelector('.question-order-modal');
            const idElement = question.querySelector('.question-id-modal');

            if (textElement && orderElement) {
                const questionData = {
                    id: idElement && idElement.value ? parseInt(idElement.value) : null,
                    text: textElement.value.trim(),
                    displayOrder: parseInt(orderElement.value) || index + 1
                };

                votingData.push(questionData);
            }
        });

        return votingData;
    }

    /**
     * Validace dat hlasování v modálním okně
     * @returns {boolean} True, pokud jsou data validní
     */
    function validateVotingDataModal() {
        if (!votingTypeSelect_modal) return true;

        const voteType = parseInt(votingTypeSelect_modal.value);
        const questions = document.querySelectorAll('.voting-question-modal');

        // Pokud je hlasování aktivní, ale nemá žádné otázky, je to chyba
        if (voteType !== 0 && questions.length === 0) {
            document.getElementById("modalMessage").textContent =
                "Pro vytvoření hlasování je potřeba přidat alespoň jednu otázku.";
            new bootstrap.Modal(document.getElementById("errorModal")).show();
            return false;
        }

        // Validace každé otázky
        let isValid = true;
        questions.forEach((question, index) => {
            const textElement = question.querySelector('.question-text-modal');
            const orderElement = question.querySelector('.question-order-modal');

            if (!textElement.value.trim()) {
                document.getElementById("modalMessage").textContent =
                    `Otázka #${index + 1} nemá vyplněný text.`;
                new bootstrap.Modal(document.getElementById("errorModal")).show();
                isValid = false;
                return;
            }

            const orderValue = parseInt(orderElement.value) || 0;
            if (orderValue <= 0) {
                document.getElementById("modalMessage").textContent =
                    `Otázka #${index + 1} má neplatné pořadí. Hodnota musí být kladné číslo.`;
                new bootstrap.Modal(document.getElementById("errorModal")).show();
                isValid = false;
                return;
            }
        });

        return isValid;
    }

    /**
     * Uložení změn hlasování z modálního okna
     */
    async function saveVotingChanges() {
        try {
            // Kontrola validity dat
            if (!validateVotingDataModal()) {
                return;
            }

            // Získání ID diskuze
            const discussionId = getDiscussionIdFromPage();
            if (!discussionId) {
                alert('Nepodařilo se zjistit ID diskuze.');
                return;
            }

            // Získání typu hlasování
            const voteType = parseInt(votingTypeSelect_modal.value);

            // Příprava dat pro odeslání
            const votingData = {
                discussionId: discussionId,
                voteType: voteType,
                questions: voteType !== 0 ? prepareVotingDataModal() : []
            };

            // Změníme stav tlačítka pro indikaci načítání
            const saveBtn = document.getElementById('save-voting-btn');
            if (saveBtn) {
                saveBtn.disabled = true;
                saveBtn.innerHTML = '<span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span> Ukládání...';
            }

            // Odeslání dat na server
            const apiBaseUrl = document.getElementById('apiBaseUrl').value;
            const response = await fetch(`${apiBaseUrl}/votings`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Authorization': `Bearer ${sessionStorage.getItem('JWTToken')}`
                },
                body: JSON.stringify(votingData)
            });

            // Obnovení původního stavu tlačítka
            if (saveBtn) {
                saveBtn.disabled = false;
                saveBtn.innerHTML = 'Uložit';
            }

            if (!response.ok) {
                throw new Error('Nepodařilo se uložit hlasování');
            }

            // Zavření modálu
            const modal = bootstrap.Modal.getInstance(document.getElementById('voting-modal'));
            if (modal) {
                modal.hide();
            }

            // Upozornění na úspěšné uložení
            // alert('Hlasování bylo úspěšně uloženo.');

            // Obnovení stránky pro zobrazení změn
            location.reload();

        } catch (error) {
            console.error('Chyba při ukládání hlasování:', error);
            alert('Při ukládání hlasování došlo k chybě.');
        }
    }
});