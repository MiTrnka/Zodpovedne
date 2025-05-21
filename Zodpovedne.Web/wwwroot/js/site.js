// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

// Event listener pro kliknutí na křížek maintance notifikace
$('.close-notification').on('click', function () {
    // Skrytí celého notifikačního prvku
    $('.maintenanceNotification').hide();
});

$(document).ready(function () {
    $('.alert.alert-danger, .alert.alert-info').on('click', function (e) {
        const $alert = $(this);
        const alertRect = this.getBoundingClientRect();

        // Definujeme oblast křížku jako pravý horní roh (čtverec 30x30px)
        const crossAreaSize = 30;

        // Kontrolujeme, zda klik byl v pravém horním rohu
        if (e.clientX >= alertRect.right - crossAreaSize && e.clientX <= alertRect.right &&
            e.clientY >= alertRect.top && e.clientY <= alertRect.top + crossAreaSize) {
            // Skryjeme alert info box
            $alert.fadeOut(300, function () {
                $(this).remove();
            });
        }
    });
});

/**
 * Převádí textové URL adresy na HTML odkazy nebo obrázky/GIFy
 *
 * Funkce prochází text a hledá řetězce, které vypadají jako URL adresy.
 * Pro odkazy na obrázky/GIFy (končící na .gif, .jpg, .jpeg, .png) je zobrazí jako obrázky.
 * Pro odkazy z populárních stránek s meme a GIFy (giphy.com, tenor.com, imgur.com apod.)
 * provede kontrolu, zda jde skutečně o odkaz na konkrétní GIF/obrázek, a pak jej zobrazí.
 * Ostatní URL adresy převádí na HTML odkazy s atributem target="_blank",
 * aby se otevíraly v nové záložce.
 *
 * @param {string} text - Text, ve kterém chceme detekovat URL adresy
 * @returns {string} Text s URL adresami převedenými na HTML odkazy nebo obrázky
 */
function linkifyText(text) {
    // Regulární výraz pro detekci URL, který ignoruje URL v HTML atributech
    const urlRegex = /(https?:\/\/[^\s<>"]+)|(www\.[^\s<>"]+)|([^\s<>"]+\.(com|org|edu|gov|net|sk|cz|eu|io|co|me|info|it|biz|xyz|dev))/gi;

    // Funkce pro náhradu URL adresou, která kontroluje, jestli URL není již součástí odkazu
    return text.replace(urlRegex, function (match, url1, url2, url3) {
        // Zjistíme, které URL bylo nalezeno (url1, url2 nebo url3)
        const url = url1 || url2 || url3;

        // Kontrola, zda URL není již součástí HTML tagu
        // Pokud před URL je znak "<", může být součástí HTML tagu
        const positionInText = text.indexOf(url);
        if (positionInText > 0 && text.charAt(positionInText - 1) === '<') {
            return url; // Vrátíme URL beze změny
        }

        // Pokud URL nezačíná protokolem, přidáme http://
        let href = url;
        if (!url.match(/^https?:\/\//i)) {
            href = 'http://' + url;
        }

        // Kontrola, zda jde přímo o obrázek podle přípony souboru
        const isDirectImageUrl = /\.(gif|jpe?g|png)$/i.test(url);

        if (isDirectImageUrl) {
            // Přímý odkaz na obrázek - zobrazíme jako obrázek
            return `<a href="${href}" target="_blank"><img src="${href}" alt="Obrázek" style="max-width: 200px; max-height: 200px; display: block; margin: 5px 0;" loading="lazy"></a>`;
        }

        // Kontrola odkazů na GIFy z populárních služeb - musíme kontrolovat specifické vzory URL,
        // které odpovídají konkrétním GIFům, ne hlavním stránkám

        // Giphy.com - má např. formát /gifs/..., /embed/..., /media/...
        if (/giphy\.com\/(gifs|embed|media|clips)\/[a-zA-Z0-9-]+$/i.test(url)) {
            return `<a href="${href}" target="_blank"><img src="${href}" alt="GIF" style="max-width: 200px; max-height: 200px; display: block; margin: 5px 0;" loading="lazy"></a>`;
        }

        // Tenor.com - má např. formát /view/..., /8474832...
        if (/tenor\.com\/view\/[a-zA-Z0-9-]+$/i.test(url) || /tenor\.com\/[a-zA-Z0-9]{7,}$/i.test(url)) {
            return `<a href="${href}" target="_blank"><img src="${href}" alt="GIF" style="max-width: 200px; max-height: 200px; display: block; margin: 5px 0;" loading="lazy"></a>`;
        }

        // Imgur.com - má např. formát /gallery/..., /a/..., přímé ID obrázku
        if (/imgur\.com\/(gallery\/|a\/)?[a-zA-Z0-9]{5,}$/i.test(url)) {
            return `<a href="${href}" target="_blank"><img src="${href}" alt="Obrázek" style="max-width: 200px; max-height: 200px; display: block; margin: 5px 0;" loading="lazy"></a>`;
        }

        // Discord a další CDN odkazy na média
        if (/media\.discordapp\.net\/attachments\/[0-9]+\/[0-9]+\/[^\/]+$/i.test(url) ||
            /cdn\.discordapp\.com\/attachments\/[0-9]+\/[0-9]+\/[^\/]+$/i.test(url) ||
            /media\.tenor\.com\/[a-zA-Z0-9_-]+\/[^\/]+$/i.test(url)) {
            return `<a href="${href}" target="_blank"><img src="${href}" alt="Obrázek" style="max-width: 200px; max-height: 200px; display: block; margin: 5px 0;" loading="lazy"></a>`;
        }

        // Pro ostatní odkazy vytvoříme běžný odkaz
        return '<a href="' + href + '" target="_blank" rel="noopener noreferrer">' + url + '</a>';
    });
}
