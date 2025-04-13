class MyUploadAdapter {
    constructor(loader) {
        // Načítač souborů z CKEditoru
        this.loader = loader;
    }

    // Začne proces nahrávání
    upload() {
        return this.loader.file
            .then(file => new Promise((resolve, reject) => {
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
        // Získáme kód diskuze z URL
        const discussionCode = window.location.pathname.split('/').pop();

        const xhr = this.xhr = new XMLHttpRequest();

        // Nastavení URL endpoint pro nahrávání
        xhr.open('POST', `/api/FileUpload/upload?discussionCode=${discussionCode}`, true);

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

        xhr.addEventListener('error', () => reject(genericErrorText));
        xhr.addEventListener('abort', () => reject());
        xhr.addEventListener('load', () => {
            const response = xhr.response;

            // Kontrola, zda server vrátil chybu
            if (!response || response.error) {
                return reject(response && response.error ? response.error.message : genericErrorText);
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