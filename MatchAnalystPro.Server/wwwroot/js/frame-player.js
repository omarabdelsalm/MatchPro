window.FramePlayer = {
    _instances: new Map(),

    create(videoId, fps, dotnetRef) {
        const video = document.getElementById(videoId);
        if (!video) return -1;

        // إذا كان هناك مثيل سابق لنفس عنصر الفيديو، نظفه أولاً
        for (const [oldId, oldInst] of this._instances.entries()) {
            if (oldInst.videoId === videoId) {
                this.dispose(oldId);
            }
        }

        const id = Date.now() + Math.floor(Math.random() * 1000);
        const overlay = document.getElementById('videoOverlay-' + videoId);
        const inst = {
            id,
            videoId,
            video,
            overlay,
            fps: fps || 25,
            frameMs: 1000 / (fps || 25),
            dotnetRef: dotnetRef || null,
            lastReportedFrame: -1,
            lastReportTime: 0,
            interactionMode: 'tag', // 'tag' (تحديد إحداثيات الحدث) | 'draw' (رسم تكتيكي) | 'none'
            drawTool: 'arrow',      // 'arrow' | 'circle' | 'freehand'
            drawColor: '#eab308',
            drawings: [],
            currentMarker: null,    // { xPct, yPct, endXPct, endYPct }
            isDrawing: false,
            dragStart: null
        };

        this._instances.set(id, inst);
        this._bindVideoEvents(inst);
        this._bindOverlayEvents(inst);
        return id;
    },

    _bindVideoEvents(inst) {
        const v = inst.video;
        const notifyFrame = (force = false) => {
            if (!inst.dotnetRef) return;
            const frame = Math.round((v.currentTime || 0) * inst.fps);
            const now = performance.now();
            if (!force && frame === inst.lastReportedFrame) return;
            if (!force && (now - inst.lastReportTime) < 120) return; // Throttling لمنع إغراق SignalR
            inst.lastReportedFrame = frame;
            inst.lastReportTime = now;
            try {
                inst.dotnetRef.invokeMethodAsync('OnFrameUpdate', frame);
            } catch (e) { }
        };

        inst._onTimeUpdate = () => notifyFrame(false);
        inst._onSeeked = () => notifyFrame(true);
        inst._onPause = () => notifyFrame(true);
        inst._onLoadedMetadata = () => {
            this._syncOverlaySize(inst);
            notifyFrame(true);
        };

        v.addEventListener('timeupdate', inst._onTimeUpdate);
        v.addEventListener('seeked', inst._onSeeked);
        v.addEventListener('pause', inst._onPause);
        v.addEventListener('loadedmetadata', inst._onLoadedMetadata);

        // مزامنة حجم طبقة الرسم مع حجم الفيديو عند تغيير حجم النافذة
        inst._resizeObserver = new ResizeObserver(() => this._syncOverlaySize(inst));
        inst._resizeObserver.observe(v);
        setTimeout(() => this._syncOverlaySize(inst), 100);
    },

    _syncOverlaySize(inst) {
        if (!inst.overlay || !inst.video) return;
        const rect = inst.video.getBoundingClientRect();
        // نترك شريط التحكم السفلي (حوالي 38 بكسل) متاحاً للنقر المباشر على الفيديو
        const controlsHeight = 38;
        const h = Math.max(0, rect.height - controlsHeight);
        inst.overlay.width = rect.width || 640;
        inst.overlay.height = h || 360;
        inst.overlay.style.width = rect.width + 'px';
        inst.overlay.style.height = h + 'px';
        this._renderOverlay(inst);
    },

    _bindOverlayEvents(inst) {
        if (!inst.overlay) return;
        const canvas = inst.overlay;

        const getCoords = (e) => {
            const rect = canvas.getBoundingClientRect();
            const vRect = inst.video.getBoundingClientRect();
            const clientX = e.clientX - rect.left;
            const clientY = e.clientY - rect.top;

            // إحداثيات البكسل الحقيقية داخل إطار الفيديو الأصلي (للمعايرة Homography)
            const vidW = inst.video.videoWidth || vRect.width || 1280;
            const vidH = inst.video.videoHeight || vRect.height || 720;
            const videoX = (clientX / (vRect.width || 1)) * vidW;
            const videoY = (clientY / (vRect.height || 1)) * vidH;

            // النسب المئوية داخل لوحة الفيديو (0 - 100)
            const xPct = Math.max(0, Math.min(100, (clientX / (vRect.width || 1)) * 100));
            const yPct = Math.max(0, Math.min(100, (clientY / (vRect.height || 1)) * 100));

            return { clientX, clientY, videoX, videoY, xPct, yPct };
        };

        inst._onMouseDown = (e) => {
            if (inst.interactionMode === 'none') return;
            const pt = getCoords(e);

            if (inst.interactionMode === 'tag') {
                // إيقاف الفيديو تلقائياً عند النقر لتصنيف حدث بدقة الفريم
                if (!inst.video.paused) {
                    inst.video.pause();
                }
                if (inst.dotnetRef) {
                    const frame = this.getCurrentFrame(inst.id);
                    inst.dotnetRef.invokeMethodAsync('OnVideoClicked', pt.videoX, pt.videoY, pt.xPct, pt.yPct, frame);
                }
                return;
            }

            if (inst.interactionMode === 'draw') {
                if (!inst.video.paused) inst.video.pause();
                inst.isDrawing = true;
                inst.dragStart = pt;
                if (inst.drawTool === 'freehand') {
                    inst.currentFreehand = [{ x: pt.clientX, y: pt.clientY }];
                }
            }
        };

        inst._onMouseMove = (e) => {
            if (!inst.isDrawing || inst.interactionMode !== 'draw') return;
            const pt = getCoords(e);
            this._renderOverlay(inst);
            const ctx = canvas.getContext('2d');

            if (inst.drawTool === 'arrow' && inst.dragStart) {
                this._drawArrow(ctx, inst.dragStart.clientX, inst.dragStart.clientY, pt.clientX, pt.clientY, inst.drawColor);
            } else if (inst.drawTool === 'circle' && inst.dragStart) {
                const r = Math.hypot(pt.clientX - inst.dragStart.clientX, pt.clientY - inst.dragStart.clientY);
                this._drawCircle(ctx, inst.dragStart.clientX, inst.dragStart.clientY, r, inst.drawColor);
            } else if (inst.drawTool === 'freehand' && inst.currentFreehand) {
                inst.currentFreehand.push({ x: pt.clientX, y: pt.clientY });
                this._drawFreehand(ctx, inst.currentFreehand, inst.drawColor);
            }
        };

        inst._onMouseUp = (e) => {
            if (!inst.isDrawing || inst.interactionMode !== 'draw') return;
            inst.isDrawing = false;
            const pt = getCoords(e);

            if (inst.drawTool === 'arrow' && inst.dragStart) {
                inst.drawings.push({
                    type: 'arrow',
                    x1: inst.dragStart.clientX,
                    y1: inst.dragStart.clientY,
                    x2: pt.clientX,
                    y2: pt.clientY,
                    color: inst.drawColor
                });
            } else if (inst.drawTool === 'circle' && inst.dragStart) {
                const r = Math.max(12, Math.hypot(pt.clientX - inst.dragStart.clientX, pt.clientY - inst.dragStart.clientY));
                inst.drawings.push({
                    type: 'circle',
                    x: inst.dragStart.clientX,
                    y: inst.dragStart.clientY,
                    r,
                    color: inst.drawColor
                });
            } else if (inst.drawTool === 'freehand' && inst.currentFreehand) {
                inst.drawings.push({
                    type: 'freehand',
                    points: inst.currentFreehand,
                    color: inst.drawColor
                });
                inst.currentFreehand = null;
            }
            inst.dragStart = null;
            this._renderOverlay(inst);
        };

        canvas.addEventListener('mousedown', inst._onMouseDown);
        canvas.addEventListener('mousemove', inst._onMouseMove);
        canvas.addEventListener('mouseup', inst._onMouseUp);
    },

    setInteractionMode(id, mode, tool, color) {
        const inst = this._instances.get(id);
        if (!inst) return;
        inst.interactionMode = mode || 'tag';
        if (tool) inst.drawTool = tool;
        if (color) inst.drawColor = color;
        if (inst.overlay) {
            inst.overlay.style.pointerEvents = (inst.interactionMode === 'none') ? 'none' : 'auto';
            inst.overlay.style.cursor = (inst.interactionMode === 'none') ? 'default' : 'crosshair';
        }
    },

    setVideoMarker(id, xPct, yPct, endXPct, endYPct, color, label) {
        const inst = this._instances.get(id);
        if (!inst) return;
        if (xPct == null || yPct == null) {
            inst.currentMarker = null;
        } else {
            inst.currentMarker = { xPct, yPct, endXPct, endYPct, color: color || '#3fb950', label: label || '' };
        }
        this._renderOverlay(inst);
    },

    clearDrawings(id) {
        const inst = this._instances.get(id);
        if (!inst) return;
        inst.drawings = [];
        inst.currentMarker = null;
        this._renderOverlay(inst);
    },

    _renderOverlay(inst) {
        if (!inst.overlay) return;
        const canvas = inst.overlay;
        const ctx = canvas.getContext('2d');
        ctx.clearRect(0, 0, canvas.width, canvas.height);

        // رسم الأشكال التكتيكية المرسومة يدوياً (Telestration)
        for (const d of inst.drawings) {
            if (d.type === 'arrow') {
                this._drawArrow(ctx, d.x1, d.y1, d.x2, d.y2, d.color);
            } else if (d.type === 'circle') {
                this._drawCircle(ctx, d.x, d.y, d.r, d.color);
            } else if (d.type === 'freehand') {
                this._drawFreehand(ctx, d.points, d.color);
            }
        }

        // رسم نقطة الحدث المحددة على الفيديو
        if (inst.currentMarker) {
            const m = inst.currentMarker;
            const vRect = inst.video.getBoundingClientRect();
            const px = (m.xPct / 100) * (vRect.width || canvas.width);
            const py = (m.yPct / 100) * (vRect.height || canvas.height);

            if (m.endXPct != null && m.endYPct != null) {
                const ex = (m.endXPct / 100) * (vRect.width || canvas.width);
                const ey = (m.endYPct / 100) * (vRect.height || canvas.height);
                this._drawArrow(ctx, px, py, ex, ey, m.color || '#3fb950');
            }

            ctx.fillStyle = m.color || '#3fb950';
            ctx.beginPath();
            ctx.arc(px, py, 8, 0, Math.PI * 2);
            ctx.fill();
            ctx.strokeStyle = '#ffffff';
            ctx.lineWidth = 2;
            ctx.stroke();

            if (m.label) {
                ctx.fillStyle = 'rgba(0,0,0,0.75)';
                ctx.fillRect(px + 10, py - 20, m.label.length * 7 + 16, 20);
                ctx.fillStyle = '#ffffff';
                ctx.font = 'bold 12px Cairo, sans-serif';
                ctx.fillText(m.label, px + 16, py - 6);
            }
        }
    },

    _drawArrow(ctx, x1, y1, x2, y2, color) {
        ctx.save();
        ctx.strokeStyle = color || '#eab308';
        ctx.fillStyle = color || '#eab308';
        ctx.lineWidth = 3;
        ctx.beginPath();
        ctx.moveTo(x1, y1);
        ctx.lineTo(x2, y2);
        ctx.stroke();

        const angle = Math.atan2(y2 - y1, x2 - x1);
        ctx.beginPath();
        ctx.moveTo(x2, y2);
        ctx.lineTo(x2 - 14 * Math.cos(angle - Math.PI / 6), y2 - 14 * Math.sin(angle - Math.PI / 6));
        ctx.lineTo(x2 - 14 * Math.cos(angle + Math.PI / 6), y2 - 14 * Math.sin(angle + Math.PI / 6));
        ctx.closePath();
        ctx.fill();
        ctx.restore();
    },

    _drawCircle(ctx, x, y, r, color) {
        ctx.save();
        ctx.strokeStyle = color || '#eab308';
        ctx.fillStyle = 'rgba(234, 179, 8, 0.15)';
        ctx.lineWidth = 3;
        ctx.beginPath();
        ctx.arc(x, y, r, 0, Math.PI * 2);
        ctx.fill();
        ctx.stroke();
        ctx.restore();
    },

    _drawFreehand(ctx, points, color) {
        if (!points || points.length < 2) return;
        ctx.save();
        ctx.strokeStyle = color || '#eab308';
        ctx.lineWidth = 3;
        ctx.lineCap = 'round';
        ctx.lineJoin = 'round';
        ctx.beginPath();
        ctx.moveTo(points[0].x, points[0].y);
        for (let i = 1; i < points.length; i++) {
            ctx.lineTo(points[i].x, points[i].y);
        }
        ctx.stroke();
        ctx.restore();
    },

    attachTimeListener(id, dotnetRef) {
        const inst = this._instances.get(id);
        if (!inst) return;
        inst.dotnetRef = dotnetRef;
    },

    getCurrentFrame(id) {
        const inst = this._instances.get(id);
        if (!inst || !inst.video) return 0;
        return Math.round((inst.video.currentTime || 0) * inst.fps);
    },

    getCurrentTimeMs(id) {
        const inst = this._instances.get(id);
        if (!inst || !inst.video) return 0;
        return Math.round((inst.video.currentTime || 0) * 1000);
    },

    togglePlay(id) {
        const inst = this._instances.get(id);
        if (!inst || !inst.video) return false;
        if (inst.video.paused) {
            inst.video.play().catch(() => {});
            return true;
        } else {
            inst.video.pause();
            return false;
        }
    },

    seekToFrame(id, frame) {
        const inst = this._instances.get(id);
        if (!inst || !inst.video) return Promise.resolve();
        const t = Math.max(0, frame / inst.fps);
        return new Promise(resolve => {
            const onSeeked = () => {
                inst.video.removeEventListener('seeked', onSeeked);
                resolve();
            };
            inst.video.addEventListener('seeked', onSeeked);
            inst.video.currentTime = t;
            setTimeout(() => {
                inst.video.removeEventListener('seeked', onSeeked);
                resolve();
            }, 250);
        });
    },

    async captureFrameBase64(id, frame) {
        const inst = this._instances.get(id);
        if (!inst || !inst.video) return null;
        if (frame != null && Math.abs(this.getCurrentFrame(id) - frame) > 1) {
            await this.seekToFrame(id, frame);
        }
        const canvas = document.createElement('canvas');
        canvas.width = inst.video.videoWidth || 640;
        canvas.height = inst.video.videoHeight || 360;
        const ctx = canvas.getContext('2d');
        if (inst.video.readyState >= 2) {
            ctx.drawImage(inst.video, 0, 0, canvas.width, canvas.height);
            // دمج الرسومات التكتيكية المرسومة فوق الفيديو داخل الـ Freeze Frame
            if (inst.overlay && inst.overlay.width > 0 && inst.overlay.height > 0) {
                ctx.drawImage(inst.overlay, 0, 0, canvas.width, canvas.height);
            }
        } else {
            ctx.fillStyle = "#1e293b";
            ctx.fillRect(0, 0, canvas.width, canvas.height);
            ctx.fillStyle = "#ffffff";
            ctx.font = "20px sans-serif";
            ctx.fillText("Frame " + frame, 40, 60);
        }
        return canvas.toDataURL('image/jpeg', 0.85).split(',')[1];
    },

    async playRange(id, startFrame, endFrame) {
        const inst = this._instances.get(id);
        if (!inst || !inst.video) return;
        await this.seekToFrame(id, startFrame);
        try {
            await inst.video.play();
        } catch (e) { }
        return new Promise(resolve => {
            const check = () => {
                if (this.getCurrentFrame(id) >= endFrame || inst.video.paused) {
                    inst.video.pause();
                    inst.video.removeEventListener('timeupdate', check);
                    resolve();
                }
            };
            inst.video.addEventListener('timeupdate', check);
        });
    },

    setPlaybackRate(id, rate) {
        const inst = this._instances.get(id);
        if (inst && inst.video) inst.video.playbackRate = rate;
    },

    loadLocalBlobFile(videoId, inputElementId) {
        const input = document.getElementById(inputElementId);
        const video = document.getElementById(videoId);
        if (!input || !input.files || input.files.length === 0 || !video) return null;
        const file = input.files[0];
        const blobUrl = URL.createObjectURL(file);
        video.src = blobUrl;
        video.load();
        return { name: file.name, sizeMb: Math.round((file.size / (1024 * 1024)) * 100) / 100, blobUrl };
    },

    setVideoBlobUrl(videoId, blobUrl) {
        const video = document.getElementById(videoId);
        if (!video || !blobUrl) return false;
        video.src = blobUrl;
        video.load();
        return true;
    },

    dispose(id) {
        const inst = this._instances.get(id);
        if (!inst) return;
        if (inst.video) {
            inst.video.removeEventListener('timeupdate', inst._onTimeUpdate);
            inst.video.removeEventListener('seeked', inst._onSeeked);
            inst.video.removeEventListener('pause', inst._onPause);
            inst.video.removeEventListener('loadedmetadata', inst._onLoadedMetadata);
        }
        if (inst._resizeObserver) {
            inst._resizeObserver.disconnect();
        }
        if (inst.overlay) {
            inst.overlay.removeEventListener('mousedown', inst._onMouseDown);
            inst.overlay.removeEventListener('mousemove', inst._onMouseMove);
            inst.overlay.removeEventListener('mouseup', inst._onMouseUp);
        }
        this._instances.delete(id);
    }
};
