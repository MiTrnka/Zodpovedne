/**
 * Skript pro správu hlasování v diskuzích
 *
 * Tento soubor obsluhuje funkcionalitu přidávání, editace a mazání
 * hlasovacích otázek při vytváření nebo úpravě diskuze.
 *
 * Funkce:
 * - Přidání nové otázky
 * - Mazání otázek
 * - Aktualizace číslování otázek
 * - Počítání znaků v textu otázky
 * - Validace hlasovacích formulářů
 */

document.addEventListener('DOMContentLoaded', function () {
    // Základní prvky pro práci s hlasováním
    const hasVotingCheckbox = document.getElementById('hasVoting');
    const votingSection = document.getElementById('voting-section');
    const addQuestionBtn = document.getElementById('add-question-btn');
    const questionsContainer = document.getElementById('voting-questions-container');
    const noQuestionsMessage = document.getElementById('no-questions-message');
    const questionTemplate = document.getElementById('question-template');
    const votingTypeSelect = document.getElementById('voting-type');

    // Sledování globálního stavu otázek
    let questionCounter = 0;

    // Inicializace - zobrazení/skrytí sekce hlasování podle výchozího stavu checkboxu
    if (hasVotingCheckbox) {
        votingSection.style.display = hasVotingCheckbox.checked ? 'block' : 'none';
        if (hasVotingCheckbox.checked) {
            // Pokud je výchozí stav zapnutý, inicializujeme existující otázky
            initExistingQuestions();
        }
    }

    // Přidání posluchače událostí pro přepínač hlasování
    if (hasVotingCheckbox) {
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

    // Přidání posluchače události pro tlačítko "Přidat otázku"
    if (addQuestionBtn) {
        addQuestionBtn.addEventListener('click', addNewQuestion);
    }

    // Inicializace existujících otázek (pokud existují)
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

    // Funkce pro přidání nové otázky
    function addNewQuestion(event, questionData = null) {
        // Zvýšení počítadla pro unikátní ID
        questionCounter++;

        // Skrytí zprávy "Žádné otázky"
        noQuestionsMessage.style.display = 'none';

        // Klonování šablony otázky
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
        questionsContainer.appendChild(questionElement);

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

    // Funkce pro odstranění otázky
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

    // Funkce pro aktualizaci počítadla znaků
    function updateCharCount(textarea, countElement) {
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

    // Funkce pro aktualizaci číslování otázek
    function updateQuestionNumbers() {
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

    // Funkce pro zjištění počtu otázek
    function getQuestionCount() {
        return document.querySelectorAll('.voting-question').length;
    }

    // Funkce pro zobrazení/skrytí zprávy "Žádné otázky"
    function updateNoQuestionsMessage() {
        if (getQuestionCount() === 0) {
            noQuestionsMessage.style.display = 'block';
        } else {
            noQuestionsMessage.style.display = 'none';
        }
    }

    // Funkce pro přípravu dat hlasovacích otázek před odesláním formuláře
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

    // Funkce pro validaci dat hlasování
    function validateVotingData() {
        if (!hasVotingCheckbox || !hasVotingCheckbox.checked) {
            return true; // Hlasování není aktivní, validace není potřeba
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

    // Připojení validace k odesílání formuláře
    const discussionForm = document.getElementById('create-discussion-form');
    if (discussionForm) {
        discussionForm.addEventListener('submit', function (event) {
            if (!validateVotingData()) {
                event.preventDefault();
                return false;
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
});