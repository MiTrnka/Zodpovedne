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