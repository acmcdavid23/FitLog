// Shared image cropper for circular profile/group photos.
(function () {
    const modalEl = document.getElementById("imageCropModal");
    if (!modalEl || typeof bootstrap === "undefined") return;

    const modal = new bootstrap.Modal(modalEl);
    const canvas = document.getElementById("imageCropCanvas");
    const ctx = canvas.getContext("2d");
    const zoomInput = document.getElementById("imageCropZoom");
    const applyBtn = document.getElementById("imageCropApplyBtn");

    const state = {
        input: null,
        fileName: "cropped.jpg",
        image: null,
        zoom: 1,
        minZoom: 1,
        x: 0,
        y: 0,
        dragging: false,
        dragStartX: 0,
        dragStartY: 0,
        originX: 0,
        originY: 0
    };

    function clampPosition() {
        if (!state.image) return;
        const drawnW = state.image.width * state.zoom;
        const drawnH = state.image.height * state.zoom;
        const minX = canvas.width - drawnW;
        const minY = canvas.height - drawnH;
        state.x = Math.min(0, Math.max(minX, state.x));
        state.y = Math.min(0, Math.max(minY, state.y));
    }

    function render() {
        ctx.clearRect(0, 0, canvas.width, canvas.height);
        if (!state.image) return;
        const drawnW = state.image.width * state.zoom;
        const drawnH = state.image.height * state.zoom;
        ctx.drawImage(state.image, state.x, state.y, drawnW, drawnH);
    }

    function loadImage(file) {
        return new Promise((resolve, reject) => {
            const reader = new FileReader();
            reader.onload = () => {
                const img = new Image();
                img.onload = () => resolve(img);
                img.onerror = reject;
                img.src = reader.result;
            };
            reader.onerror = reject;
            reader.readAsDataURL(file);
        });
    }

    async function openCropper(input, file) {
        try {
            const img = await loadImage(file);
            state.input = input;
            state.image = img;
            state.fileName = (file.name || "cropped").replace(/\.[^.]+$/, "") + ".jpg";
            state.minZoom = Math.max(canvas.width / img.width, canvas.height / img.height);
            state.zoom = state.minZoom;
            zoomInput.min = String(state.minZoom);
            zoomInput.max = String(Math.max(state.minZoom, 3));
            zoomInput.value = String(state.zoom);
            state.x = (canvas.width - img.width * state.zoom) / 2;
            state.y = (canvas.height - img.height * state.zoom) / 2;
            clampPosition();
            render();
            modal.show();
        } catch (_) {
            // If cropper fails, keep original file selected.
        }
    }

    function outputCroppedFile() {
        const out = document.createElement("canvas");
        out.width = 512;
        out.height = 512;
        const octx = out.getContext("2d");
        const scale = out.width / canvas.width;
        octx.drawImage(
            canvas,
            0, 0, canvas.width, canvas.height,
            0, 0, out.width, out.height
        );
        out.toBlob((blob) => {
            if (!blob || !state.input) return;
            const file = new File([blob], state.fileName, { type: "image/jpeg" });
            const dt = new DataTransfer();
            dt.items.add(file);
            state.input.files = dt.files;
            modal.hide();
        }, "image/jpeg", 0.92);
    }

    canvas.addEventListener("pointerdown", (e) => {
        if (!state.image) return;
        state.dragging = true;
        state.dragStartX = e.clientX;
        state.dragStartY = e.clientY;
        state.originX = state.x;
        state.originY = state.y;
        canvas.setPointerCapture(e.pointerId);
    });

    canvas.addEventListener("pointermove", (e) => {
        if (!state.dragging) return;
        const dx = e.clientX - state.dragStartX;
        const dy = e.clientY - state.dragStartY;
        state.x = state.originX + dx;
        state.y = state.originY + dy;
        clampPosition();
        render();
    });

    function endDrag(e) {
        if (!state.dragging) return;
        state.dragging = false;
        try { canvas.releasePointerCapture(e.pointerId); } catch (_) { }
    }

    canvas.addEventListener("pointerup", endDrag);
    canvas.addEventListener("pointercancel", endDrag);

    zoomInput.addEventListener("input", () => {
        if (!state.image) return;
        const prevZoom = state.zoom;
        state.zoom = parseFloat(zoomInput.value);
        const cx = canvas.width / 2;
        const cy = canvas.height / 2;
        const relX = (cx - state.x) / prevZoom;
        const relY = (cy - state.y) / prevZoom;
        state.x = cx - relX * state.zoom;
        state.y = cy - relY * state.zoom;
        clampPosition();
        render();
    });

    applyBtn.addEventListener("click", outputCroppedFile);

    document.addEventListener("change", (e) => {
        const input = e.target;
        if (!(input instanceof HTMLInputElement)) return;
        if (input.type !== "file") return;
        if (!input.hasAttribute("data-image-crop-circle")) return;
        const file = input.files && input.files[0];
        if (!file || !file.type.startsWith("image/")) return;
        openCropper(input, file);
    });
})();
