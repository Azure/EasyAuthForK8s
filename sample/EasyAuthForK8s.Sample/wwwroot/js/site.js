// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
function reload() {
    var encoding = "UrlEncode";
    var ele = document.getElementsByName('header-encoding');
    for (i = 0; i < ele.length; i++) {
        if (ele[i].checked) {
            encoding = ele[i].id;
            break;
        }
    }

    var format = "Separate";
    var ele = document.getElementsByName('header-format');
    for (i = 0; i < ele.length; i++) {
        if (ele[i].checked) {
            format = ele[i].id;
            break;
        }
    }

    var prefix = document.getElementById('header-prefix').value;

    document.location.search = `encoding=${encoding}&format=${format}&prefix=${encodeURIComponent(prefix)}`;
}
