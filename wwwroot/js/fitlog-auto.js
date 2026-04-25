/**
 * Auto-submit forms via fetch when form has data-fitlog-ajax-url (full URL from Url.Action).
 * Expects JSON: { success: false, error? | message? } on failure; { success: true, message?, redirectUrl? } on success.
 * Toast UI: #fitlog-toast-container (fixed top-right, defined in _Layout).
 */
(function () {
    window.fitlogToast = function (message, type) {
        var container = document.getElementById('fitlog-toast-container');
        if (!container || message == null || message === '') return;
        type = (type || 'info').toLowerCase();
        var colors = {
            success: { bg: '#1e2d1e', border: '#28a745', icon: '\u2713' },
            danger: { bg: '#2d1e1e', border: '#dc3545', icon: '\u2715' },
            error: { bg: '#2d1e1e', border: '#dc3545', icon: '\u2715' },
            warning: { bg: '#2d2718', border: '#ffc107', icon: '!' },
            info: { bg: '#1e2333', border: '#17a2b8', icon: '\u2139' }
        };
        var c = colors[type] || colors.info;
        var toast = document.createElement('div');
        toast.setAttribute('role', 'alert');
        toast.style.cssText =
            'background:' + c.bg + ';border-left:3px solid ' + c.border + ';border-radius:8px;padding:12px 16px;' +
            'display:flex;align-items:flex-start;gap:10px;box-shadow:0 4px 12px rgba(0,0,0,0.4);opacity:0;' +
            'transform:translateX(20px);transition:opacity 0.2s ease, transform 0.2s ease;cursor:pointer;pointer-events:auto;';
        var icon = document.createElement('span');
        icon.style.cssText = 'color:' + c.border + ';font-size:1rem;font-weight:bold;flex-shrink:0;line-height:1.35;';
        icon.textContent = c.icon;
        var text = document.createElement('span');
        text.style.cssText = 'color:#e0e0e0;font-size:0.875rem;flex:1;min-width:0;line-height:1.35;word-break:break-word;';
        text.textContent = String(message);
        var close = document.createElement('span');
        close.style.cssText = 'color:#6c757d;font-size:0.75rem;cursor:pointer;flex-shrink:0;line-height:1.35;';
        close.textContent = '\u2715';
        close.setAttribute('aria-label', 'Dismiss');
        function removeToast() {
            if (!toast.parentNode) return;
            toast.style.opacity = '0';
            toast.style.transform = 'translateX(20px)';
            setTimeout(function () {
                try {
                    toast.remove();
                } catch (_) {}
            }, 200);
        }
        close.addEventListener('click', function (e) {
            e.stopPropagation();
            removeToast();
        });
        toast.appendChild(icon);
        toast.appendChild(text);
        toast.appendChild(close);
        toast.addEventListener('click', removeToast);
        container.appendChild(toast);
        requestAnimationFrame(function () {
            toast.style.opacity = '1';
            toast.style.transform = 'translateX(0)';
        });
        var autoDismiss = type === 'success' ? 3000 : 6000;
        setTimeout(removeToast, autoDismiss);
    };
    window.fitlogAlert = window.fitlogToast;

    document.addEventListener('submit', async function (e) {
        var form = e.target;
        if (!form || form.tagName !== 'FORM') return;
        var ajaxUrl = form.getAttribute('data-fitlog-ajax-url');
        if (!ajaxUrl) return;
        e.preventDefault();
        var submitters = form.querySelectorAll('[type="submit"]');
        submitters.forEach(function (s) {
            s.disabled = true;
        });
        try {
            var res = await fetch(ajaxUrl, { method: 'POST', body: new FormData(form), credentials: 'same-origin' });
            var ct = (res.headers.get('content-type') || '').toLowerCase();
            var data = {};
            if (ct.indexOf('application/json') >= 0) {
                try {
                    data = await res.json();
                } catch (_) {}
            }
            if (!res.ok || (data && data.success === false)) {
                window.fitlogToast((data && (data.error || data.message)) || 'Request failed.', 'danger');
                return;
            }
            if (data.redirectUrl) {
                window.dispatchEvent(
                    new CustomEvent('fitlog:redirect-requested', {
                        detail: { url: data.redirectUrl, form: form, data: data },
                        bubbles: true
                    })
                );
            }
            var evName = form.getAttribute('data-fitlog-success-event');
            if (evName) {
                window.dispatchEvent(new CustomEvent(evName, { detail: { form: form, data: data }, bubbles: true }));
            }
            var clearSel = form.getAttribute('data-fitlog-clear-selector');
            if (clearSel) {
                form.querySelectorAll(clearSel).forEach(function (inp) {
                    if (inp.type === 'checkbox' || inp.type === 'radio') inp.checked = false;
                    else if ('value' in inp) inp.value = '';
                });
            }
            var closeModal = form.getAttribute('data-fitlog-close-modal');
            if (closeModal) {
                var modalEl = document.getElementById(closeModal);
                if (modalEl && typeof bootstrap !== 'undefined') {
                    var inst = bootstrap.Modal.getInstance(modalEl);
                    if (inst) inst.hide();
                }
            }
            if (!evName || (data && data.message)) {
                window.fitlogToast((data && data.message) || 'Saved successfully.', 'success');
            }
        } catch (_) {
            window.fitlogToast('Network error.', 'danger');
        } finally {
            submitters.forEach(function (s) {
                s.disabled = false;
            });
        }
    });
})();
