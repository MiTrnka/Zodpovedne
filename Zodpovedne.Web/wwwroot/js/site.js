// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

// Event listener pro kliknutí na křížek maintance notifikace
$('.close-notification').on('click', function () {
    // Skrytí celého notifikačního prvku
    $('.maintenanceNotification').hide();
});