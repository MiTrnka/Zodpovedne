/**
 * Sdílené funkce pro práci s CKEditorem
 * Pro použití v Discussion.js a CreateDiscussion.cshtml
 */

// Konfigurace CKEditoru
function createEditorConfig(canUploadFiles = false) {
    const editorConfig = {
        // Základní konfigurace nástrojové lišty bez problematického alignment
        toolbar: [
            'heading',
            '|',
            'bold',
            'italic',
            'link',
            'bulletedList',
            'numberedList',
            '|',
            // Odstraněno alignment
            ...(canUploadFiles ? ['imageUpload', '|'] : []), // Podmíněné přidání tlačítka pro nahrávání obrázků
            'mediaEmbed', // Nástroj pro vkládání médií
            '|',
            'undo',
            'redo'
        ],
        language: 'cs',
        // Odstraněna konfigurace alignment
        // Konfigurace pro vkládání médií
        mediaEmbed: {
            previewsInData: true, // Ukládat iframe v HTML
            toolbar: ['mediaEmbed:inline', 'mediaEmbed:center'],
            styles: {
                options: [
                    { name: 'inline', title: 'Umístit kdekoliv', className: '' },
                    { name: 'center', title: 'Zarovnat na střed', className: 'image-style-align-center' }
                ]
            },
            providers: [
                {
                    name: 'youtube',
                    url: [
                        /^(?:m\.)?youtube\.com\/watch\?v=([\w-]+)(?:&t=(\d+))?/,
                        /^(?:m\.)?youtube\.com\/v\/([\w-]+)(?:\?t=(\d+))?/,
                        /^youtube\.com\/embed\/([\w-]+)(?:\?start=(\d+))?/,
                        /^youtu\.be\/([\w-]+)(?:\?t=(\d+))?/
                    ],
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

    // Přidáme konfiguraci pro obrázky pouze pokud uživatel může nahrávat soubory
    if (canUploadFiles) {
        editorConfig.image = {
            insert: {
                type: 'inline' // Změna výchozího stylu na inline
            },
            toolbar: [
                'imageTextAlternative',
                '|',
                'imageStyle:inline',        // Plovoucí obrázek
                'imageStyle:alignCenter'  // Zarovnání na střed
            ],
            // Definice stylů zarovnání - použití standardních CKEditor stylů
            styles: {
                options: [
                    { name: 'inline', title: 'Umístit kdekoliv', icon: 'inline' },
                    { name: 'alignCenter', title: 'Zarovnat na střed', icon: 'center' }
                ]
            },
            // Nastavení upload URL
            upload: {
                types: ['jpeg', 'png', 'gif', 'jpg', 'webp']
            }
        };

        // Přidáme plugin pro upload
        editorConfig.extraPlugins = [MyCustomUploadAdapterPlugin];
    }

    return editorConfig;
}

// Funkce pro úpravu HTML před odesláním na server
function processEditorContentBeforeSave(content) {
    // Vytvořte dočasný div pro manipulaci s obsahem
    const tempDiv = document.createElement('div');
    tempDiv.innerHTML = content;

    // Upravit všechny img elementy s třídou pro zarovnání
    tempDiv.querySelectorAll('img.image-style-align-center').forEach(img => {
        // Pokud img není v divu, obalíme ho
        const parent = img.parentElement;
        if (parent.tagName !== 'DIV') {
            const centerDiv = document.createElement('div');
            centerDiv.classList.add('text-center'); // Přidáme Bootstrap třídu text-center
            centerDiv.style.textAlign = 'center'; // Explicitní CSS zarovnání

            // Nahradíme obrázek divem obsahujícím obrázek
            parent.replaceChild(centerDiv, img);
            centerDiv.appendChild(img);
        }
    });

    // Upravit všechny figure elementy s třídou pro zarovnání
    tempDiv.querySelectorAll('figure.image-style-align-center').forEach(figure => {
        figure.style.textAlign = 'center';
        figure.style.margin = '0 auto';

        // Ujistíme se, že img uvnitř figure má správný styl
        const img = figure.querySelector('img');
        if (img) {
            img.style.display = 'block';
            img.style.margin = '0 auto';
        }
    });

    return tempDiv.innerHTML;
}