window.MultiCamera = {
    _groups: new Map(),

    create(groupId, videoIds, masterId) {
        const videos = videoIds.map(id => document.getElementById(id)).filter(v => v);
        if (videos.length === 0) return false;

        const master = document.getElementById(masterId) || videos[0];
        const group = { videos, master, isSyncing: false, lastReportedFrame: -1, lastReportTime: 0 };
        this._groups.set(groupId, group);

        // عندما يتحرك الماستر → حرك الباقي
        master.addEventListener('seeked', () => {
            if (group.isSyncing) return;
            group.isSyncing = true;
            const t = master.currentTime;
            videos.forEach(v => {
                if (v !== master && Math.abs(v.currentTime - t) > 0.05) {
                    v.currentTime = t;
                }
            });
            setTimeout(() => group.isSyncing = false, 100);
        });

        // عند التشغيل/الإيقاف
        master.addEventListener('play', () => {
            videos.forEach(v => { if (v !== master) v.play().catch(() => {}); });
        });
        master.addEventListener('pause', () => {
            videos.forEach(v => { if (v !== master) v.pause(); });
        });

        return true;
    },

    attachTimeListener(groupId, fps, dotnetRef) {
        const group = this._groups.get(groupId);
        if (!group || !group.master) return;
        const notify = (force = false) => {
            const frame = Math.round((group.master.currentTime || 0) * (fps || 25));
            const now = performance.now();
            if (!force && frame === group.lastReportedFrame) return;
            if (!force && (now - group.lastReportTime) < 120) return;
            group.lastReportedFrame = frame;
            group.lastReportTime = now;
            try {
                dotnetRef.invokeMethodAsync('OnFrame', frame);
            } catch (e) {}
        };
        group.master.addEventListener('timeupdate', () => notify(false));
        group.master.addEventListener('seeked', () => notify(true));
        group.master.addEventListener('pause', () => notify(true));
    },

    togglePlay(groupId) {
        const group = this._groups.get(groupId);
        if (!group || !group.master) return false;
        if (group.master.paused) {
            group.master.play().catch(() => {});
            return true;
        } else {
            group.master.pause();
            return false;
        }
    },

    seekAll(groupId, frame, fps) {
        const group = this._groups.get(groupId);
        if (!group) return;
        const t = Math.max(0, frame / (fps || 25));
        group.isSyncing = true;
        group.videos.forEach(v => { v.currentTime = t; });
        setTimeout(() => group.isSyncing = false, 150);
    },

    async captureFromCamera(groupId, videoIndex, frame, fps) {
        const group = this._groups.get(groupId);
        if (!group || !group.videos[videoIndex]) return null;
        const v = group.videos[videoIndex];
        
        const targetTime = frame / (fps || 25);
        if (Math.abs(v.currentTime - targetTime) > 0.02) {
            v.currentTime = targetTime;
            await new Promise(r => {
                const h = () => { v.removeEventListener('seeked', h); r(); };
                v.addEventListener('seeked', h);
                setTimeout(() => { v.removeEventListener('seeked', h); r(); }, 250);
            });
        }

        const canvas = document.createElement('canvas');
        canvas.width = v.videoWidth || 640;
        canvas.height = v.videoHeight || 360;
        canvas.getContext('2d').drawImage(v, 0, 0);
        return canvas.toDataURL('image/jpeg', 0.85).split(',')[1];
    },

    dispose(groupId) {
        this._groups.delete(groupId);
    }
};
