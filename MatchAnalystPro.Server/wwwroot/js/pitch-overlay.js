window.PitchOverlay = {
    draw(eventCoords, canvasId) {
        const canvas = document.getElementById(canvasId);
        if (!canvas) return;
        const ctx = canvas.getContext('2d');
        ctx.clearRect(0, 0, canvas.width, canvas.height);

        this._drawPitchBackground(ctx, canvas.width, canvas.height);
        const w = canvas.width, h = canvas.height;

        // رسم الأحداث
        (eventCoords || []).forEach(ev => {
            const px = (ev.x / 100) * w;
            const py = (ev.y / 100) * h;

            if (ev.endX != null && ev.endY != null) {
                const ex = (ev.endX / 100) * w;
                const ey = (ev.endY / 100) * h;

                // خط الاتجاه
                ctx.strokeStyle = ev.color || 'rgba(255,215,0,0.85)';
                ctx.lineWidth = 2.5;
                ctx.beginPath();
                ctx.moveTo(px, py);
                ctx.lineTo(ex, ey);
                ctx.stroke();

                // رأس السهم
                const angle = Math.atan2(ey - py, ex - px);
                ctx.fillStyle = ev.color || 'rgba(255,215,0,0.85)';
                ctx.beginPath();
                ctx.moveTo(ex, ey);
                ctx.lineTo(ex - 10 * Math.cos(angle - Math.PI / 6),
                           ey - 10 * Math.sin(angle - Math.PI / 6));
                ctx.lineTo(ex - 10 * Math.cos(angle + Math.PI / 6),
                           ey - 10 * Math.sin(angle + Math.PI / 6));
                ctx.closePath();
                ctx.fill();
            }

            // نقطة الحدث
            ctx.fillStyle = ev.color || '#e74c3c';
            ctx.beginPath();
            ctx.arc(px, py, ev.radius || 6, 0, Math.PI * 2);
            ctx.fill();
            ctx.strokeStyle = '#ffffff';
            ctx.lineWidth = 1.5;
            ctx.stroke();

            if (ev.label) {
                ctx.fillStyle = '#ffffff';
                ctx.font = 'bold 11px Cairo, sans-serif';
                ctx.fillText(ev.label, px + 8, py - 6);
            }
        });
    },

    _drawPitchBackground(ctx, w, h) {
        // خلفية الملعب (أخضر تكتيكي احترافي)
        ctx.fillStyle = '#153226';
        ctx.fillRect(0, 0, w, h);

        // تقليمات عشب الملعب
        ctx.fillStyle = 'rgba(255,255,255,0.025)';
        const stripes = 10;
        const stripeW = (w - 10) / stripes;
        for (let i = 0; i < stripes; i += 2) {
            ctx.fillRect(5 + i * stripeW, 5, stripeW, h - 10);
        }

        // خطوط الملعب
        ctx.strokeStyle = 'rgba(255,255,255,0.75)';
        ctx.lineWidth = 2;

        // الحدود الخارجية
        ctx.strokeRect(5, 5, w - 10, h - 10);

        // خط المنتصف
        ctx.beginPath();
        ctx.moveTo(w / 2, 5);
        ctx.lineTo(w / 2, h - 5);
        ctx.stroke();

        // دائرة المنتصف
        ctx.beginPath();
        ctx.arc(w / 2, h / 2, h * 0.16, 0, Math.PI * 2);
        ctx.stroke();

        // نقطة المركز
        ctx.beginPath();
        ctx.arc(w / 2, h / 2, 3, 0, Math.PI * 2);
        ctx.fillStyle = 'rgba(255,255,255,0.85)';
        ctx.fill();

        // منطقة الجزاء اليسرى
        ctx.strokeRect(5, h * 0.2, w * 0.16, h * 0.6);
        ctx.strokeRect(5, h * 0.36, w * 0.06, h * 0.28);
        ctx.beginPath();
        ctx.arc(w * 0.11, h * 0.5, h * 0.1, -Math.PI * 0.35, Math.PI * 0.35);
        ctx.stroke();

        // منطقة الجزاء اليمنى
        ctx.strokeRect(w * 0.84, h * 0.2, w * 0.16, h * 0.6);
        ctx.strokeRect(w * 0.94, h * 0.36, w * 0.06, h * 0.28);
        ctx.beginPath();
        ctx.arc(w * 0.89, h * 0.5, h * 0.1, Math.PI * 0.65, Math.PI * 1.35);
        ctx.stroke();

        // سهم اتجاه الهجوم
        ctx.save();
        ctx.fillStyle = 'rgba(255,255,255,0.22)';
        ctx.font = 'bold 11px Cairo, sans-serif';
        ctx.textAlign = 'center';
        ctx.fillText('اتجاه الهجوم ➔', w / 2, h - 12);
        ctx.restore();
    },

    // رسم شبكة التمريرات ومتوسط تمركز اللاعبين (Passing Network & Average Positions)
    drawPassNetwork(canvasId, nodes, links) {
        const canvas = document.getElementById(canvasId);
        if (!canvas) return;
        const ctx = canvas.getContext('2d');
        const w = canvas.width, h = canvas.height;
        ctx.clearRect(0, 0, w, h);
        this._drawPitchBackground(ctx, w, h);

        // 1) رسم خطوط التمرير بين اللاعبين
        (links || []).forEach(l => {
            const x1 = (l.fromX / 100) * w;
            const y1 = (l.fromY / 100) * h;
            const x2 = (l.toX / 100) * w;
            const y2 = (l.toY / 100) * h;

            ctx.save();
            ctx.strokeStyle = l.count >= 4 ? 'rgba(88, 166, 255, 0.9)' : 'rgba(88, 166, 255, 0.45)';
            ctx.lineWidth = Math.min(9, Math.max(1.5, l.count * 1.3));
            ctx.beginPath();
            ctx.moveTo(x1, y1);
            ctx.lineTo(x2, y2);
            ctx.stroke();

            // عدد التمريرات في منتصف الخط
            if (l.count >= 2) {
                const mx = (x1 + x2) / 2;
                const my = (y1 + y2) / 2;
                ctx.fillStyle = 'rgba(13, 17, 23, 0.85)';
                ctx.beginPath();
                ctx.arc(mx, my, 9, 0, Math.PI * 2);
                ctx.fill();
                ctx.fillStyle = '#58a6ff';
                ctx.font = 'bold 10px sans-serif';
                ctx.textAlign = 'center';
                ctx.textBaseline = 'middle';
                ctx.fillText(l.count.toString(), mx, my);
            }
            ctx.restore();
        });

        // 2) رسم عقد اللاعبين (متوسط التمركز)
        (nodes || []).forEach(n => {
            const px = (n.avgX / 100) * w;
            const py = (n.avgY / 100) * h;
            const radius = Math.min(22, Math.max(12, 10 + (n.passCount || 1) * 0.6));

            ctx.save();
            // هالة خارجية
            ctx.fillStyle = 'rgba(31, 111, 235, 0.28)';
            ctx.beginPath();
            ctx.arc(px, py, radius + 4, 0, Math.PI * 2);
            ctx.fill();

            // دائرة اللاعب
            ctx.fillStyle = '#1f6feb';
            ctx.beginPath();
            ctx.arc(px, py, radius, 0, Math.PI * 2);
            ctx.fill();
            ctx.strokeStyle = '#ffffff';
            ctx.lineWidth = 2;
            ctx.stroke();

            // رقم القميص داخل الدائرة
            ctx.fillStyle = '#ffffff';
            ctx.font = 'bold 12px sans-serif';
            ctx.textAlign = 'center';
            ctx.textBaseline = 'middle';
            ctx.fillText((n.shirtNumber || '').toString(), px, py);

            // اسم اللاعب أسفل الدائرة
            if (n.name) {
                ctx.font = 'bold 11px Cairo, sans-serif';
                const textW = ctx.measureText(n.name).width + 10;
                ctx.fillStyle = 'rgba(13, 17, 23, 0.82)';
                ctx.fillRect(px - textW / 2, py + radius + 3, textW, 17);
                ctx.fillStyle = '#e6edf3';
                ctx.fillText(n.name, px, py + radius + 12);
            }
            ctx.restore();
        });
    },

    // رسم الخريطة الحرارية لمناطق الملعب الـ 15 (5x3 Zones) + Zone 14
    drawHeatmap(canvasId, points) {
        const canvas = document.getElementById(canvasId);
        if (!canvas) return;
        const ctx = canvas.getContext('2d');
        const w = canvas.width, h = canvas.height;
        ctx.clearRect(0, 0, w, h);
        this._drawPitchBackground(ctx, w, h);

        const cols = 5, rows = 3;
        const counts = Array.from({ length: cols }, () => Array(rows).fill(0));
        const total = (points || []).length;

        (points || []).forEach(p => {
            const c = Math.min(cols - 1, Math.max(0, Math.floor((p.x / 100) * cols)));
            const r = Math.min(rows - 1, Math.max(0, Math.floor((p.y / 100) * rows)));
            counts[c][r]++;
        });

        let maxCount = 1;
        for (let c = 0; c < cols; c++) {
            for (let r = 0; r < rows; r++) {
                if (counts[c][r] > maxCount) maxCount = counts[c][r];
            }
        }

        const cellW = (w - 10) / cols;
        const cellH = (h - 10) / rows;

        for (let c = 0; c < cols; c++) {
            for (let r = 0; r < rows; r++) {
                const cnt = counts[c][r];
                const intensity = cnt / maxCount;
                const x = 5 + c * cellW;
                const y = 5 + r * cellH;

                if (cnt > 0) {
                    // تدرج لوني من الأصفر للبرتقالي للأحمر حسب الكثافة
                    const alpha = 0.15 + intensity * 0.50;
                    const red = 248;
                    const green = Math.round(200 * (1 - intensity * 0.8));
                    ctx.fillStyle = `rgba(${red}, ${green}, 40, ${alpha})`;
                    ctx.fillRect(x, y, cellW, cellH);
                }

                ctx.strokeStyle = 'rgba(255,255,255,0.18)';
                ctx.lineWidth = 1;
                ctx.strokeRect(x, y, cellW, cellH);

                // تمييز المنطقة 14 (Zone 14: العمود الرابع، الصف الأوسط)
                const isZone14 = (c === 3 && r === 1);
                if (isZone14) {
                    ctx.strokeStyle = '#eab308';
                    ctx.lineWidth = 2;
                    ctx.strokeRect(x + 1, y + 1, cellW - 2, cellH - 2);
                }

                const pct = total > 0 ? Math.round((cnt * 100) / total) : 0;
                ctx.fillStyle = cnt > 0 ? '#ffffff' : 'rgba(255,255,255,0.35)';
                ctx.font = 'bold 14px sans-serif';
                ctx.textAlign = 'center';
                ctx.fillText(`${pct}%`, x + cellW / 2, y + cellH / 2);
                ctx.font = '11px Cairo, sans-serif';
                ctx.fillStyle = 'rgba(255,255,255,0.75)';
                ctx.fillText(isZone14 ? `Zone 14 (${cnt})` : `${cnt} حدث`, x + cellW / 2, y + cellH / 2 + 18);
            }
        }
    },

    // رسم خريطة التسديدات والأهداف المتوقعة (Shot Map & xG)
    drawShotMap(canvasId, shots) {
        const canvas = document.getElementById(canvasId);
        if (!canvas) return;
        const ctx = canvas.getContext('2d');
        const w = canvas.width, h = canvas.height;
        ctx.clearRect(0, 0, w, h);
        this._drawPitchBackground(ctx, w, h);

        (shots || []).forEach(s => {
            const px = (s.x / 100) * w;
            const py = (s.y / 100) * h;
            const radius = Math.min(20, Math.max(6, 6 + (s.xg || 0.1) * 28));
            const color = s.isGoal ? '#3fb950' : (s.isOnTarget ? '#eab308' : '#f85149');

            if (s.endX != null && s.endY != null) {
                const ex = (s.endX / 100) * w;
                const ey = (s.endY / 100) * h;
                ctx.save();
                ctx.strokeStyle = color;
                ctx.lineWidth = 2;
                ctx.setLineDash([4, 3]);
                ctx.beginPath();
                ctx.moveTo(px, py);
                ctx.lineTo(ex, ey);
                ctx.stroke();
                ctx.restore();
            }

            ctx.save();
            ctx.fillStyle = color;
            ctx.beginPath();
            ctx.arc(px, py, radius, 0, Math.PI * 2);
            ctx.fill();
            ctx.strokeStyle = '#ffffff';
            ctx.lineWidth = 2;
            ctx.stroke();

            if (s.label) {
                ctx.fillStyle = '#ffffff';
                ctx.font = 'bold 11px Cairo, sans-serif';
                ctx.fillText(`${s.label} (${(s.xg || 0).toFixed(2)})`, px + radius + 4, py + 4);
            }
            ctx.restore();
        });
    },

    attachClick(canvasId, dotnetRef) {
        const canvas = document.getElementById(canvasId);
        if (!canvas) return;
        canvas.addEventListener('click', (e) => {
            const rect = canvas.getBoundingClientRect();
            const x = Math.max(0, Math.min(100, ((e.clientX - rect.left) / rect.width) * 100));
            const y = Math.max(0, Math.min(100, ((e.clientY - rect.top) / rect.height) * 100));
            try {
                dotnetRef.invokeMethodAsync('OnPitchClick', x, y);
            } catch(err) {}
        });
    },

    attachVideoCalibration(videoId, dotnetRef, overlayCanvasId) {
        const v = document.getElementById(videoId);
        const overlay = document.getElementById(overlayCanvasId || 'calOverlay');
        const target = overlay || v;
        if (!target) return;

        const syncSize = () => {
            if (!overlay || !v) return;
            const rect = v.getBoundingClientRect();
            const h = Math.max(0, rect.height - 38);
            overlay.width = rect.width || 640;
            overlay.height = h || 360;
            overlay.style.width = rect.width + 'px';
            overlay.style.height = h + 'px';
        };

        if (v) {
            v.addEventListener('loadedmetadata', syncSize);
            setTimeout(syncSize, 100);
        }

        if (target._calHandler) {
            target.removeEventListener('click', target._calHandler);
        }
        target._calHandler = function(e) {
            e.preventDefault();
            e.stopPropagation();
            const baseEl = v || target;
            const rect = baseEl.getBoundingClientRect();
            const vidW = (v && v.videoWidth) ? v.videoWidth : 1280;
            const vidH = (v && v.videoHeight) ? v.videoHeight : 720;
            const scaleX = vidW / (rect.width || 1);
            const scaleY = vidH / (rect.height || 1);
            const x = Math.round((e.clientX - rect.left) * scaleX);
            const y = Math.round((e.clientY - rect.top) * scaleY);
            try {
                dotnetRef.invokeMethodAsync('OnVideoClick', x, y);
            } catch(err) {}
        };
        target.addEventListener('click', target._calHandler);
    },

    drawCalibrationPoints(videoId, overlayCanvasId, corners) {
        const v = document.getElementById(videoId);
        const overlay = document.getElementById(overlayCanvasId || 'calOverlay');
        if (!overlay) return;

        const rect = v ? v.getBoundingClientRect() : overlay.getBoundingClientRect();
        const vidW = (v && v.videoWidth) ? v.videoWidth : 1280;
        const vidH = (v && v.videoHeight) ? v.videoHeight : 720;
        const h = Math.max(0, rect.height - (v ? 38 : 0));
        overlay.width = rect.width || 640;
        overlay.height = h || 360;

        const ctx = overlay.getContext('2d');
        ctx.clearRect(0, 0, overlay.width, overlay.height);
        if (!corners || corners.length === 0) return;

        const pts = corners.map(c => ({
            px: (c.x / vidW) * (rect.width || overlay.width),
            py: (c.y / vidH) * (rect.height || overlay.height)
        }));

        if (pts.length > 1) {
            ctx.save();
            ctx.strokeStyle = '#58a6ff';
            ctx.fillStyle = 'rgba(88, 166, 255, 0.18)';
            ctx.lineWidth = 2.5;
            ctx.setLineDash([6, 4]);
            ctx.beginPath();
            ctx.moveTo(pts[0].px, pts[0].py);
            for (let i = 1; i < pts.length; i++) {
                ctx.lineTo(pts[i].px, pts[i].py);
            }
            if (pts.length === 4) {
                ctx.closePath();
                ctx.fill();
            }
            ctx.stroke();
            ctx.restore();
        }

        const labels = ['1: أعلى-يسار', '2: أعلى-يمين', '3: أسفل-يمين', '4: أسفل-يسار'];
        pts.forEach((p, idx) => {
            ctx.fillStyle = '#3fb950';
            ctx.beginPath();
            ctx.arc(p.px, p.py, 7, 0, Math.PI * 2);
            ctx.fill();
            ctx.strokeStyle = '#ffffff';
            ctx.lineWidth = 2;
            ctx.stroke();

            ctx.fillStyle = 'rgba(13, 17, 23, 0.85)';
            ctx.fillRect(p.px + 10, p.py - 18, 90, 20);
            ctx.fillStyle = '#58a6ff';
            ctx.font = 'bold 11px Cairo, sans-serif';
            ctx.fillText(labels[idx] || `#${idx + 1}`, p.px + 14, p.py - 4);
        });
    },

    downloadDataUri(fileName, dataUri) {
        const a = document.createElement('a');
        a.download = fileName;
        a.href = dataUri;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
    },

    openUrl(url) {
        window.open(url, '_blank');
    }
};
