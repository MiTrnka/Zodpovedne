/**
 * Sdílené funkce pro práci s CKEditorem
 * Pro použití v Discussion.js a CreateDiscussion.cshtml
 *
 * Tento modul obsahuje centralizované funkce pro konfiguraci CKEditoru a zpracování obsahu.
 * Zajišťuje konzistentní chování editoru napříč celou aplikací a správné zachování
 * zarovnání obrázků a videí při ukládání a načítání obsahu.
 */

/**
 * Vytváří konfigurační objekt pro CKEditor s podporou obrázků, videí a zarovnání.
 *
 * Funkce vytváří kompletní konfiguraci pro CKEditor, která zahrnuje:
 * - Nástroje v toolbaru (formátování, seznamy, odkazy, média)
 * - Podporu pro nahrávání obrázků (podmíněná podle oprávnění uživatele)
 * - Konfiguraci pro vkládání YouTube videí s responsive designem
 * - Nástroje pro zarovnání obrázků a videí
 * - Českou lokalizaci
 *
 * @param {boolean} canUploadFiles - Určuje, zda má uživatel oprávnění nahrávat soubory
 * @returns {Object} Konfigurace pro CKEditor s všemi potřebnými nastaveními
 */
function createEditorConfig(canUploadFiles = false) {
    const editorConfig = {
        // Konfigurace nástrojové lišty s podmíněným přidáním uploadu obrázků
        toolbar: [
            'heading',
            '|',
            'bold',
            'italic',
            'link',
            'bulletedList',
            'numberedList',
            '|',
            // Tlačítko pro nahrávání obrázků se zobrazí pouze pokud má uživatel oprávnění
            ...(canUploadFiles ? ['imageUpload', '|'] : []),
            'mediaEmbed', // Nástroj pro vkládání YouTube videí a dalších médií
            '|',
            'undo',
            'redo'
        ],

        // Nastavení českého jazyka pro UI editoru
        language: 'cs',

        // Konfigurace pro vkládání médií (především YouTube videa)
        mediaEmbed: {
            // Ukládání iframe přímo do HTML obsahu místo placeholder
            previewsInData: true,

            // Toolbar pro média s možnostmi zarovnání
            toolbar: ['mediaEmbed:inline', 'mediaEmbed:center'],

            // Definice stylů pro zarovnání médií
            styles: {
                options: [
                    {
                        name: 'inline',
                        title: 'Umístit kdekoliv',
                        className: ''
                    },
                    {
                        name: 'center',
                        title: 'Zarovnat na střed',
                        className: 'image-style-align-center'
                    }
                ]
            },

            // Provider pro YouTube s podporou různých formátů URL
            providers: [
                {
                    name: 'youtube',
                    // Regex patterns pro rozpoznání různých formátů YouTube URL
                    url: [
                        /^(?:m\.)?youtube\.com\/watch\?v=([\w-]+)(?:&t=(\d+))?/,
                        /^(?:m\.)?youtube\.com\/v\/([\w-]+)(?:\?t=(\d+))?/,
                        /^youtube\.com\/embed\/([\w-]+)(?:\?start=(\d+))?/,
                        /^youtu\.be\/([\w-]+)(?:\?t=(\d+))?/
                    ],
                    // Funkce pro generování HTML výstupu YouTube videa
                    html: match => {
                        const id = match[1];
                        const time = match[2];

                        return (
                            '<div class="embed-responsive embed-responsive-16by9">' +
                            '<iframe class="embed-responsive-item" ' +
                            'src="https://www.youtube.com/embed/' + id + (time ? '?start=' + time : '') + '" ' +
                            'allowfullscreen>' +
                            '</iframe>' +
                            '</div>'
                        );
                    }
                }
            ]
        }
    };

    // Přidání konfigurace pro obrázky pouze pokud má uživatel oprávnění k uploadu
    if (canUploadFiles) {
        editorConfig.image = {
            // Nastavení výchozího typu vkládání obrázků
            insert: {
                type: 'inline'
            },

            // Toolbar specifický pro obrázky s nástroji pro alternativní text a zarovnání
            toolbar: [
                'imageTextAlternative',
                '|',
                'imageStyle:inline',
                'imageStyle:alignCenter'
            ],

            // Definice stylů zarovnání obrázků
            styles: {
                options: [
                    {
                        name: 'inline',
                        title: 'Umístit kdekoliv',
                        icon: 'inline'
                    },
                    {
                        name: 'alignCenter',
                        title: 'Zarovnat na střed',
                        icon: 'center'
                    }
                ]
            },

            // Konfigurace uploadu s povolenými typy souborů
            upload: {
                types: ['jpeg', 'png', 'gif', 'jpg', 'webp']
            }
        };

        // Přidání custom upload adaptéru pro zpracování nahraných souborů
        editorConfig.extraPlugins = [MyCustomUploadAdapterPlugin];
    }

    return editorConfig;
}

/**
 * Zpracovává HTML obsah z CKEditoru před jeho uložením na server.
 *
 * Tato funkce řeší problém se ztrátou zarovnání obrázků a videí tím, že:
 * - Analyzuje DOM strukturu obsahu a identifikuje elementy s CSS třídami pro zarovnání
 * - Aplikuje dodatečné CSS styly a HTML atributy pro zajištění kompatibility
 * - Obaluje samostatné obrázky do div kontejnerů s Bootstrap třídami
 * - Zajišťuje, že zarovnání zůstane zachováno i po reload stránky
 * - Ošetřuje edge-case scénáře jako kombinace obrázků a videí
 *
 * Funkce vytváří dočasný DOM element pro bezpečnou manipulaci s HTML obsahem
 * a vrací upravený HTML řetězec připravený pro uložení do databáze.
 *
 * @param {string} content - HTML obsah z CKEditoru
 * @returns {string} Zpracovaný HTML obsah s preserved zarovnáním
 */
function processEditorContentBeforeSave(content) {
    // Vytvoření dočasného DOM elementu pro bezpečnou manipulaci s HTML obsahem
    const tempDiv = document.createElement('div');
    tempDiv.innerHTML = content;

    // Zpracování všech obrázků s třídou pro centrální zarovnání
    tempDiv.querySelectorAll('img.image-style-align-center').forEach(img => {
        const parent = img.parentElement;

        // Kontrola, zda obrázek není již v správně nakonfigurovaném kontejneru
        if (!parent.classList.contains('text-center') &&
            !parent.classList.contains('image-style-align-center')) {

            // Vytvoření nového div kontejneru pro zarovnání
            const centerDiv = document.createElement('div');
            centerDiv.classList.add('text-center', 'image-style-align-center');

            // Aplikace inline CSS stylů pro zajištění kompatibility napříč prohlížeči
            centerDiv.style.textAlign = 'center';
            centerDiv.style.display = 'block';
            centerDiv.style.width = '100%';
            centerDiv.style.margin = '1rem auto';

            // Vložení div kontejneru před původní obrázek a přesunutí obrázku do něj
            parent.insertBefore(centerDiv, img);
            centerDiv.appendChild(img);

            // Aplikace stylů přímo na obrázek pro dodatečnou jistotu
            img.style.display = 'block';
            img.style.margin = '0 auto';
        }
    });

    // Zpracování figure elementů (kontejnery pro obrázky s popisky)
    tempDiv.querySelectorAll('figure.image-style-align-center').forEach(figure => {
        // Aplikace stylů na figure element
        figure.style.textAlign = 'center';
        figure.style.margin = '1rem auto';
        figure.style.display = 'block';
        figure.style.width = '100%';

        // Přidání Bootstrap třídy pro konzistenci se zbytkem aplikace
        if (!figure.classList.contains('text-center')) {
            figure.classList.add('text-center');
        }

        // Aplikace stylů na obrázek uvnitř figure elementu
        const img = figure.querySelector('img');
        if (img) {
            img.style.display = 'block';
            img.style.margin = '0 auto';
            img.style.maxWidth = '100%';
        }

        // Aplikace stylů na figcaption (popisek obrázku)
        const figcaption = figure.querySelector('figcaption');
        if (figcaption) {
            figcaption.style.textAlign = 'center';
            figcaption.style.marginTop = '0.5rem';
        }
    });

    // Zpracování media elementů (YouTube videa a další vložená média)
    tempDiv.querySelectorAll('figure.media.image-style-align-center').forEach(mediaFigure => {
        // Aplikace stylů na figure element obsahující média
        mediaFigure.style.textAlign = 'center';
        mediaFigure.style.margin = '1rem auto';
        mediaFigure.style.display = 'block';
        mediaFigure.style.maxWidth = '560px';

        // Přidání Bootstrap třídy
        if (!mediaFigure.classList.contains('text-center')) {
            mediaFigure.classList.add('text-center');
        }

        // Aplikace stylů na oembed element (YouTube video)
        const oembed = mediaFigure.querySelector('oembed');
        if (oembed) {
            oembed.style.display = 'block';
            oembed.style.margin = '0 auto';
            oembed.style.width = '100%';
        }
    });

    // Zpracování div kontejnerů s embedded videi (už uložená videa)
    tempDiv.querySelectorAll('div.embed-responsive').forEach(embedDiv => {
        // Kontrola, zda má div třídu pro centrální zarovnání
        if (embedDiv.classList.contains('text-center') ||
            embedDiv.classList.contains('image-style-align-center')) {

            // Aplikace stylů pro centrální zarovnání
            embedDiv.style.margin = '1rem auto';
            embedDiv.style.display = 'block';
            embedDiv.style.maxWidth = '560px';
        }
    });

    // Dodatečné zpracování všech elementů s třídou text-center
    tempDiv.querySelectorAll('.text-center').forEach(element => {
        // Zajištění, že všechny elementy s text-center mají správné CSS vlastnosti
        element.style.textAlign = 'center';

        // Pro block elementy (div, figure) přidáme margin auto
        if (['DIV', 'FIGURE'].includes(element.tagName)) {
            element.style.margin = '1rem auto';
            element.style.display = 'block';
        }
    });

    // Vrácení upraveného HTML obsahu
    return tempDiv.innerHTML;
}

/**
 * Konvertuje HTML obsah pro načtení do CKEditoru.
 *
 * Funkce transformuje uložený HTML obsah do formátu, který CKEditor očekává:
 * - Převádí iframe YouTube videa na oembed elementy
 * - Zachovává informace o zarovnání pomocí CSS tříd
 * - Zajišťuje kompatibilitu mezi databázovým uložením a editorem
 *
 * Tato konverze je nezbytná, protože CKEditor používá vlastní interní reprezentaci
 * pro některé typy obsahu (zejména vložená média).
 *
 * @param {string} html - HTML obsah z databáze
 * @returns {string} HTML obsah připravený pro CKEditor
 */
function convertHtmlForCKEditor(html) {
    // Regex pro nalezení YouTube videí v div.embed-responsive kontejnerech
    return html.replace(
        /<div class="embed-responsive embed-responsive-16by9(?: text-center| image-style-align-center)?">\s*<iframe.*?src="https:\/\/www\.youtube\.com\/embed\/([a-zA-Z0-9_-]+)".*?><\/iframe>\s*<\/div>/g,
        function (match, videoId) {
            // Detekce zarovnání na střed z původního HTML
            const isCenter = match.includes('text-center') || match.includes('image-style-align-center');
            const styleClass = isCenter ? ' class="image-style-align-center"' : '';

            // Převod na CKEditor formát s oembed elementem
            return `<figure${styleClass} class="media"><oembed url="https://www.youtube.com/watch?v=${videoId}"></oembed></figure>`;
        }
    );
}

/**
 * Konvertuje HTML obsah z CKEditoru zpět do standardního formátu.
 *
 * Funkce provádí opačný proces než convertHtmlForCKEditor:
 * - Převádí oembed elementy zpět na iframe YouTube videa
 * - Obnovuje div.embed-responsive kontejnery s Bootstrap třídami
 * - Zachovává informace o zarovnání
 *
 * Výsledný HTML je kompatibilní s Bootstrap CSS frameworkem a správně
 * se zobrazuje v prohlížeči bez potřeby CKEditoru.
 *
 * @param {string} html - HTML obsah z CKEditoru
 * @returns {string} Standardní HTML obsah pro zobrazení v prohlížeči
 */
function convertHtmlFromCKEditor(html) {
    // Regex pro nalezení oembed YouTube elementů v CKEditor formátu
    return html.replace(
        /<figure(?:\s+class="([^"]*)")?\s*class="media">\s*<oembed url="https:\/\/www\.youtube\.com\/watch\?v=([a-zA-Z0-9_-]+)".*?><\/oembed>\s*<\/figure>/g,
        function (match, styleClass, videoId) {
            // Detekce zarovnání na střed z CKEditor tříd
            const alignCenterClass = styleClass && styleClass.includes('image-style-align-center') ? ' text-center' : '';

            // Převod zpět na standardní iframe s Bootstrap responsive třídami
            return `<div class="embed-responsive embed-responsive-16by9${alignCenterClass}"><iframe class="embed-responsive-item" src="https://www.youtube.com/embed/${videoId}" allowfullscreen></iframe></div>`;
        }
    );
}