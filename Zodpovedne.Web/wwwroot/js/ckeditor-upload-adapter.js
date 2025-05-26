class MyUploadAdapter {
    constructor(loader) {
        // Načítač souborů z CKEditoru
        this.loader = loader;
    }

    // Začne proces nahrávání
    upload() {
        return this.loader.file
            .then(file => new Promise((resolve, reject) => {
                // Kontrola velikosti souboru na straně klienta
                const maxFileSize = 10 * 1024 * 1024; // 10MB
                if (file.size > maxFileSize) {
                    reject(`Soubor je příliš velký. Maximální velikost je 10MB.`);
                    return;
                }

                this._initRequest();
                this._initListeners(resolve, reject, file);
                this._sendRequest(file);
            }));
    }

    // Zruší nahrávání
    abort() {
        if (this.xhr) {
            this.xhr.abort();
        }
    }

    // Inicializace XHR requestu
    _initRequest() {
        // Nejprve zkusíme získat kód diskuze z URL
        let discussionCode = window.location.pathname.split('/').pop();

        // Kontrola, zda jsme na stránce pro vytvoření nové diskuze
        if (window.location.pathname.includes('/discussion/create/')) {
            // Jsme na stránce vytváření diskuze, použijeme dočasný kód
            discussionCode = document.getElementById('temp-discussion-code').value;
        }

        const xhr = this.xhr = new XMLHttpRequest();

        // Nastavení URL endpoint pro nahrávání
        xhr.open('POST', `/upload/file?discussionCode=${discussionCode}`, true);

        // Nastavení JWT tokenu pro autorizaci, pokud je k dispozici
        const token = sessionStorage.getItem('JWTToken');
        if (token) {
            xhr.setRequestHeader('Authorization', `Bearer ${token}`);
        }

        // XMLHttpRequest nepotřebuje Content-Type, ten se nastaví automaticky
        xhr.responseType = 'json';
    }

    // Inicializace posluchačů událostí pro XHR
    _initListeners(resolve, reject, file) {
        const xhr = this.xhr;
        const loader = this.loader;
        const genericErrorText = `Nepodařilo se nahrát soubor: ${file.name}.`;

        xhr.addEventListener('error', () => {
            reject(genericErrorText);
        });

        xhr.addEventListener('abort', () => {
            reject('Nahrávání bylo přerušeno.');
        });

        xhr.addEventListener('load', () => {
            const response = xhr.response;

            // Kontrola, zda server vrátil chybu
            if (!response || response.error) {
                return reject(response && response.error && response.error.message ?
                    response.error.message : genericErrorText);
            }

            // Kontrola, zda server vrátil očekávanou strukturu
            if (!response.uploaded || !response.url) {
                return reject('Neplatná odpověď serveru.');
            }

            // Pokud je vše v pořádku, vrátíme data podle očekávaného formátu CKEditoru
            resolve({
                default: response.url
            });
        });

        // Pokud CKEditor podporuje progress event, můžeme ho použít
        if (xhr.upload) {
            xhr.upload.addEventListener('progress', evt => {
                if (evt.lengthComputable) {
                    // Aktualizace progress baru v editoru
                    loader.uploadTotal = evt.total;
                    loader.uploaded = evt.loaded;
                }
            });
        }
    }

    // Odeslání requestu s nahrávaným souborem
    _sendRequest(file) {
        // Validace typu souboru na straně klienta
        const allowedTypes = ['image/jpeg', 'image/png', 'image/gif', 'image/webp'];
        if (!allowedTypes.includes(file.type)) {
            this.xhr.abort();
            throw new Error('Nepodporovaný typ souboru. Povolené jsou pouze JPG, PNG, GIF a WEBP.');
        }

        // Vytvoření FormData objektu a přidání souboru
        const data = new FormData();
        data.append('upload', file);

        // Odeslání requestu
        this.xhr.send(data);
    }
}

// Funkce, která vytvoří adaptér pro nahrávání
function MyCustomUploadAdapterPlugin(editor) {
    editor.plugins.get('FileRepository').createUploadAdapter = (loader) => {
        // Vytvoření nové instance našeho adaptéru
        return new MyUploadAdapter(loader);
    };
}