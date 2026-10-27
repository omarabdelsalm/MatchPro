window.Shortcuts = {
    _dotnetRef: null,
    _registered: false,

    register(dotnetRef) {
        this._dotnetRef = dotnetRef;
        if (this._registered) return;
        document.addEventListener('keydown', (e) => {
            if (!this._dotnetRef) return;
            const tag = e.target && e.target.tagName ? e.target.tagName.toUpperCase() : '';
            if (tag === 'TEXTAREA' || tag === 'INPUT' || tag === 'SELECT') return;

            // حفظ الحدث فوراً (Enter)
            if (e.key === 'Enter') {
                e.preventDefault();
                this._dotnetRef.invokeMethodAsync('SaveEventShortcut').catch(() => {});
                return;
            }
            // تشغيل / إيقاف الفيديو (Space)
            if (e.key === ' ' || e.code === 'Space') {
                e.preventDefault();
                this._dotnetRef.invokeMethodAsync('TogglePlay').catch(() => {});
                return;
            }
            // تقديم / ترجيع إطار واحد
            if (e.key === 'ArrowRight') {
                e.preventDefault();
                this._dotnetRef.invokeMethodAsync('StepFrame', 1).catch(() => {});
                return;
            }
            if (e.key === 'ArrowLeft') {
                e.preventDefault();
                this._dotnetRef.invokeMethodAsync('StepFrame', -1).catch(() => {});
                return;
            }
            // تقديم / ترجيع ثانية كاملة (25 إطار)
            if (e.key === 'ArrowUp') {
                e.preventDefault();
                this._dotnetRef.invokeMethodAsync('StepFrame', 25).catch(() => {});
                return;
            }
            if (e.key === 'ArrowDown') {
                e.preventDefault();
                this._dotnetRef.invokeMethodAsync('StepFrame', -25).catch(() => {});
                return;
            }
            // حرف [ لتحديد بداية المقطع وحرف ] لنهاية المقطع
            if (e.key === '[') {
                e.preventDefault();
                this._dotnetRef.invokeMethodAsync('SetClipStartShortcut').catch(() => {});
                return;
            }
            if (e.key === ']') {
                e.preventDefault();
                this._dotnetRef.invokeMethodAsync('SetClipEndShortcut').catch(() => {});
                return;
            }
            // أرقام 1-9 لاختيار فئات سريعة
            if (/^[1-9]$/.test(e.key)) {
                e.preventDefault();
                this._dotnetRef.invokeMethodAsync('QuickCategory', parseInt(e.key, 10)).catch(() => {});
            }
        });
        this._registered = true;
    },

    unregister() {
        this._dotnetRef = null;
    }
};
