(function ($) {
    $.blockUI = function () {
        if ($('#_blockUiOverlay').length) return;

        $('body').append(`
            <div id="_blockUiOverlay" style="
                position: fixed;
                top: 0; left: 0;
                width: 100vw; height: 100vh;
                background: rgba(0,0,0,0.5);
                z-index: 9999;">
                <div class="spinner"></div>
            </div>
        `);
    };

    $.unblockUI = function () {
        $('#_blockUiOverlay').remove();
    };
})(jQuery);
