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
* Převádí textové URL adresy na HTML odkazy
*
* Funkce prochází text a hledá řetězce, které vypadají jako URL adresy
* (např. začínající http://, https://, www. nebo končící známými doménami).
* Nalezené URL adresy převádí na HTML odkazy s atributem target="_blank",
* aby se otevíraly v nové záložce.
*
* @param {string} text - Text, ve kterém chceme detekovat URL adresy
* @returns {string} Text s URL adresami převedenými na HTML odkazy
*/
function linkifyText(text) {
    // Regulární výraz pro detekci URL adres
    // Podporuje adresy začínající http://, https://, www. nebo končící známými doménami
    const urlRegex = /(https?:\/\/[^\s<]+)|(\bwww\.[^\s<]+)|(\b[^\s<]+\.(com|org|edu|gov|net|sk|cz|eu|io|co|me|info|it|biz|xyz|dev)\b)/gi;

    // Nahrazení URL adres HTML odkazy
    return text.replace(urlRegex, function (url) {
        // Pokud URL nezačíná protokolem, přidáme http://
        let href = url;
        if (!url.match(/^https?:\/\//i)) {
            href = 'http://' + url;
        }

        // Vytvoření HTML odkazu
        return `<a href="${href}" target="_blank" rel="noopener noreferrer">${url}</a>`;
    });
}
