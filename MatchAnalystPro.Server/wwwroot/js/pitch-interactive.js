window.InteractivePitch = {
    _dotnetRefs: new Map(),

    init(canvasId, dotnetRef, mode) {
        const canvas = document.getElementById(canvasId);
        if (!canvas) return;
        
        this._dotnetRefs.set(canvasId, { dotnetRef, mode });
        this._drawBase(canvasId);
        this._drawMarkers(canvasId, []);

        canvas.removeEventListener('click', canvas._handler);
        canvas._handler = (e) => this._onClick(canvasId, e);
        canvas.addEventListener('click', canvas._handler);
    },

    _drawBase(canvasId) {
        const canvas = document.getElementById(canvasId);
        if (!canvas) return;
        const ctx = canvas.getContext('2d');
        const w = canvas.width, h = canvas.height;

        ctx.fillStyle = '#2d7a2d';
        ctx.fillRect(0, 0, w, h);
        
        ctx.strokeStyle = 'rgba(255,255,255,0.85)';
        ctx.lineWidth = 2;
        
        // الحدود
        ctx.strokeRect(4, 4, w - 8, h - 8);
        
        // خط المنتصف
        ctx.beginPath(); ctx.moveTo(w/2, 4); ctx.lineTo(w/2, h-4); ctx.stroke();
        
        // دائرة المنتصف
        ctx.beginPath(); ctx.arc(w/2, h/2, h*0.15, 0, Math.PI*2); ctx.stroke();
        
        // مناطق الجزاء
        ctx.strokeRect(4, h*0.2, w*0.16, h*0.6);
        ctx.strokeRect(w*0.84, h*0.2, w*0.16, h*0.6);
        
        // مناطق الست ياردات
        ctx.strokeRect(4, h*0.37, w*0.06, h*0.26);
        ctx.strokeRect(w*0.94, h*0.37, w*0.06, h*0.26);
        
        // نقاط الجزاء
        ctx.fillStyle = 'rgba(255,255,255,0.85)';
        ctx.beginPath(); ctx.arc(w*0.12, h/2, 3, 0, Math.PI*2); ctx.fill();
        ctx.beginPath(); ctx.arc(w*0.88, h/2, 3, 0, Math.PI*2); ctx.fill();

        // ترقيم المناطق (15 منطقة تحليلية 5x3)
        ctx.fillStyle = 'rgba(255,255,255,0.25)';
        ctx.font = 'bold 16px sans-serif';
        ctx.textAlign = 'center';
        for (let i = 0; i < 5; i++) {
            for (let j = 0; j < 3; j++) {
                const zoneNum = i * 3 + j + 1;
                const x = (i + 0.5) * (w / 5);
                const y = (j + 0.5) * (h / 3);
                ctx.fillText(zoneNum, x, y);
            }
        }
    },

    _onClick(canvasId, e) {
        const canvas = document.getElementById(canvasId);
        if (!canvas) return;
        const rect = canvas.getBoundingClientRect();
        const x = Math.max(0, Math.min(100, ((e.clientX - rect.left) / rect.width) * 100));
        const y = Math.max(0, Math.min(100, ((e.clientY - rect.top) / rect.height) * 100));
        
        const info = this._dotnetRefs.get(canvasId);
        if (info?.dotnetRef) {
            info.dotnetRef.invokeMethodAsync('OnPitchClick', x, y);
        }
    },

    _drawMarkers(canvasId, markers) {
        const canvas = document.getElementById(canvasId);
        if (!canvas) return;
        const ctx = canvas.getContext('2d');
        const w = canvas.width, h = canvas.height;
        
        (markers || []).forEach(m => {
            const px = (m.x / 100) * w;
            const py = (m.y / 100) * h;
            
            if (m.endX != null && m.endY != null) {
                const ex = (m.endX / 100) * w;
                const ey = (m.endY / 100) * h;
                ctx.strokeStyle = m.color || 'rgba(255,255,0,0.9)';
                ctx.lineWidth = 2.5;
                ctx.beginPath(); ctx.moveTo(px, py); ctx.lineTo(ex, ey); ctx.stroke();
                
                const angle = Math.atan2(ey - py, ex - px);
                ctx.fillStyle = m.color || 'rgba(255,255,0,0.9)';
                ctx.beginPath();
                ctx.moveTo(ex, ey);
                ctx.lineTo(ex - 10*Math.cos(angle - Math.PI/6), ey - 10*Math.sin(angle - Math.PI/6));
                ctx.lineTo(ex - 10*Math.cos(angle + Math.PI/6), ey - 10*Math.sin(angle + Math.PI/6));
                ctx.closePath();
                ctx.fill();
            }
            
            ctx.fillStyle = m.color || '#ff0000';
            ctx.beginPath(); ctx.arc(px, py, m.radius || 6, 0, Math.PI*2); ctx.fill();
            ctx.strokeStyle = '#fff'; ctx.lineWidth = 1.5; ctx.stroke();

            if (m.label) {
                ctx.fillStyle = '#fff';
                ctx.font = '11px sans-serif';
                ctx.fillText(m.label, px + 8, py - 4);
            }
        });
    },

    redraw(canvasId, markers) {
        this._drawBase(canvasId);
        this._drawMarkers(canvasId, markers);
    }
};
