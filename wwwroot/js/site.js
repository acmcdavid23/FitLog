// Shared image cropper for circular profile/group photos.
(function () {
    const modalEl = document.getElementById("imageCropModal");
    if (!modalEl || typeof bootstrap === "undefined") return;

    const modal = new bootstrap.Modal(modalEl);
    const canvas = document.getElementById("imageCropCanvas");
    const ctx = canvas.getContext("2d");
    const zoomInput = document.getElementById("imageCropZoom");
    const applyBtn = document.getElementById("imageCropApplyBtn");
    const stage = modalEl.querySelector(".image-crop-stage") || canvas;

    const pointers = new Map();

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
        originY: 0,
        applied: false,
        pinchActive: false,
        pinchLastDist: 0
    };

    function resetPointerGestureState() {
        pointers.clear();
        state.dragging = false;
        state.pinchActive = false;
        state.pinchLastDist = 0;
    }

    // Allow re-picking the same file from disk (clears value right before the picker opens).
    document.addEventListener("click", (e) => {
        const t = e.target;
        if (!(t instanceof HTMLInputElement)) return;
        if (t.type !== "file" || !t.hasAttribute("data-image-crop-circle")) return;
        t.value = "";
    }, true);

    function clientToCanvas(clientX, clientY) {
        const rect = canvas.getBoundingClientRect();
        const sx = canvas.width / rect.width;
        const sy = canvas.height / rect.height;
        return {
            x: (clientX - rect.left) * sx,
            y: (clientY - rect.top) * sy
        };
    }

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

    /** Zoom toward a fixed point on the canvas (canvas pixel coords). Keeps slider in sync. */
    function setZoomAt(newZoom, anchorX, anchorY) {
        if (!state.image) return;
        const minZ = parseFloat(zoomInput.min);
        const maxZ = parseFloat(zoomInput.max);
        const z = Math.min(maxZ, Math.max(minZ, newZoom));
        const prevZoom = state.zoom;
        if (Math.abs(z - prevZoom) < 1e-9) {
            zoomInput.value = String(z);
            return;
        }
        const relImgX = (anchorX - state.x) / prevZoom;
        const relImgY = (anchorY - state.y) / prevZoom;
        state.x = anchorX - relImgX * z;
        state.y = anchorY - relImgY * z;
        state.zoom = z;
        zoomInput.value = String(z);
        clampPosition();
        render();
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
            state.applied = false;
            resetPointerGestureState();
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
        out.getContext("2d").drawImage(
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
            state.applied = true;
            const inputEl = state.input;
            const form = inputEl.form;
            const autoSubmit = inputEl.getAttribute("data-crop-auto-submit") === "true";
            modal.hide();
            if (autoSubmit && form) {
                setTimeout(() => {
                    try {
                        form.requestSubmit();
                    } catch (_) {
                        form.submit();
                    }
                }, 400);
            }
        }, "image/jpeg", 0.92);
    }

    modalEl.addEventListener("hidden.bs.modal", () => {
        if (!state.applied && state.input) {
            state.input.value = "";
        }
        state.input = null;
        state.image = null;
        state.applied = false;
        resetPointerGestureState();
    });

    canvas.addEventListener("pointerdown", (e) => {
        if (!state.image) return;
        if (e.button !== undefined && e.button !== 0) return;
        pointers.set(e.pointerId, { clientX: e.clientX, clientY: e.clientY });
        if (pointers.size === 1) {
            state.dragging = true;
            state.dragStartX = e.clientX;
            state.dragStartY = e.clientY;
            state.originX = state.x;
            state.originY = state.y;
            canvas.setPointerCapture(e.pointerId);
        } else if (pointers.size === 2) {
            state.dragging = false;
            state.pinchActive = true;
            const arr = Array.from(pointers.values());
            state.pinchLastDist = Math.max(
                Math.hypot(arr[0].clientX - arr[1].clientX, arr[0].clientY - arr[1].clientY),
                1
            );
            canvas.setPointerCapture(e.pointerId);
        }
    });

    canvas.addEventListener("pointermove", (e) => {
        if (!state.image) return;
        if (pointers.has(e.pointerId)) {
            const p = pointers.get(e.pointerId);
            p.clientX = e.clientX;
            p.clientY = e.clientY;
        }
        if (pointers.size >= 2 && state.pinchActive) {
            const vals = Array.from(pointers.values());
            const a = vals[0];
            const b = vals[1];
            const d = Math.max(Math.hypot(a.clientX - b.clientX, a.clientY - b.clientY), 1);
            const scale = d / state.pinchLastDist;
            state.pinchLastDist = d;
            const midX = (a.clientX + b.clientX) / 2;
            const midY = (a.clientY + b.clientY) / 2;
            const anchor = clientToCanvas(midX, midY);
            setZoomAt(state.zoom * scale, anchor.x, anchor.y);
            return;
        }
        if (state.dragging && pointers.size === 1) {
            const dx = e.clientX - state.dragStartX;
            const dy = e.clientY - state.dragStartY;
            state.x = state.originX + dx;
            state.y = state.originY + dy;
            clampPosition();
            render();
        }
    });

    function endPointer(e) {
        pointers.delete(e.pointerId);
        if (pointers.size < 2) {
            state.pinchActive = false;
            state.pinchLastDist = 0;
        }
        if (pointers.size === 0) {
            state.dragging = false;
        }
        try {
            canvas.releasePointerCapture(e.pointerId);
        } catch (_) { }
    }

    canvas.addEventListener("pointerup", endPointer);
    canvas.addEventListener("pointercancel", endPointer);

    /** Trackpad / mouse wheel: scroll up zooms in, down zooms out (toward cursor). */
    stage.addEventListener(
        "wheel",
        (e) => {
            if (!state.image) return;
            e.preventDefault();
            const anchor = clientToCanvas(e.clientX, e.clientY);
            const dy = e.deltaY;
            const dx = e.deltaX;
            const combined = Math.abs(dy) >= Math.abs(dx) ? dy : dx;
            const sensitivity = e.deltaMode === 1 ? 0.12 : e.deltaMode === 2 ? 0.85 : 0.0022;
            const factor = Math.exp(-combined * sensitivity);
            setZoomAt(state.zoom * factor, anchor.x, anchor.y);
        },
        { passive: false }
    );

    zoomInput.addEventListener("input", () => {
        if (!state.image) return;
        const cx = canvas.width / 2;
        const cy = canvas.height / 2;
        setZoomAt(parseFloat(zoomInput.value), cx, cy);
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

// AI assistant actions (site-wide): parse, prompt, apply.
(function () {
    const TARGET_SELECTORS = {
        calorieGoal: ["input[name='CalorieGoal']", "#CalorieGoal", "input[name='calories']", "#calories", "#estCalories"],
        proteinGoal: ["input[name='ProteinGoal']", "#ProteinGoal", "input[name='protein']", "#protein", "#estProtein"],
        carbGoal: ["input[name='CarbGoal']", "#CarbGoal", "input[name='carbs']", "#carbs", "#estCarbs"],
        fatGoal: ["input[name='FatGoal']", "#FatGoal", "input[name='fat']", "#fat", "#estFat"],
        currentWeight: ["input[name='CurrentWeight']", "#CurrentWeight"],
        goalWeight: ["input[name='GoalWeight']", "#GoalWeight"],
        waterOz: ["#customOz"],
        customOz: ["#customOz"],
        exerciseName: ["#customExerciseName"],
        reps: ["#newReps"],
        weight: ["#newWeight"],
        sets: ["#newSets"]
    };

    function parseActionEnvelope(rawText) {
        const text = String(rawText || "");
        const blockRegex = /```fitlog-actions\s*([\s\S]*?)```/i;
        const m = text.match(blockRegex);
        if (!m) {
            return { displayText: text.trim(), envelope: null };
        }
        const jsonText = (m[1] || "").trim();
        let envelope = null;
        try {
            envelope = JSON.parse(jsonText);
        } catch (_) {
            envelope = null;
        }
        const displayText = text.replace(blockRegex, "").trim();
        if (!envelope || !Array.isArray(envelope.actions) || !envelope.actions.length) {
            return { displayText, envelope: null };
        }
        return { displayText, envelope };
    }

    function resolveElement(action) {
        if (!action) return null;
        if (action.selector) return document.querySelector(action.selector);
        const key = String(action.target || "").trim();
        const selectors = TARGET_SELECTORS[key] || [];
        for (const s of selectors) {
            const el = document.querySelector(s);
            if (el) return el;
        }
        return null;
    }

    function markAndFocus(el) {
        if (!el || typeof el.focus !== "function") return;
        el.scrollIntoView({ behavior: "smooth", block: "center" });
        try { el.focus({ preventScroll: true }); } catch (_) { el.focus(); }
        el.classList.add("ai-field-highlight");
        setTimeout(() => el.classList.remove("ai-field-highlight"), 1600);
    }

    function setFieldValue(el, value) {
        if (!el) return;
        if (el instanceof HTMLInputElement || el instanceof HTMLTextAreaElement || el instanceof HTMLSelectElement) {
            el.value = value == null ? "" : String(value);
            el.dispatchEvent(new Event("input", { bubbles: true }));
            el.dispatchEvent(new Event("change", { bubbles: true }));
        } else {
            el.textContent = value == null ? "" : String(value);
        }
    }

    async function applyActions(envelope) {
        if (!envelope || !Array.isArray(envelope.actions)) return false;
        let firstChanged = null;
        let changedCount = 0;

        for (const action of envelope.actions) {
            const type = String(action?.type || "").trim();
            if (type === "setField") {
                const el = resolveElement(action);
                if (!el) continue;
                setFieldValue(el, action.value);
                if (!firstChanged) firstChanged = el;
                changedCount++;
                continue;
            }

            if (type === "addExercise" && window.fitlogAiHandlers?.addExercise) {
                const el = await window.fitlogAiHandlers.addExercise(action);
                if (!firstChanged && el) firstChanged = el;
                changedCount++;
                continue;
            }

            if (type === "call" && window.fitlogAiHandlers) {
                const fnName = String(action.name || "");
                const fn = window.fitlogAiHandlers[fnName];
                if (typeof fn === "function") {
                    const result = await fn(action.args || {});
                    if (!firstChanged && result && result.nodeType === 1) firstChanged = result;
                    changedCount++;
                }
            }
        }

        if (firstChanged) markAndFocus(firstChanged);
        return changedCount > 0;
    }

    function createActionsPrompt(envelope, onDone) {
        const wrap = document.createElement("div");
        wrap.className = "mt-2 p-2 rounded";
        wrap.style.background = "rgba(255,255,255,0.04)";
        wrap.style.border = "1px solid #3a3a5c";

        const t = document.createElement("div");
        t.className = "small text-muted mb-2";
        t.textContent = envelope.prompt || "Apply these suggested values automatically?";
        wrap.appendChild(t);

        const actionsRow = document.createElement("div");
        actionsRow.className = "d-flex gap-2";
        const acceptBtn = document.createElement("button");
        acceptBtn.type = "button";
        acceptBtn.className = "btn btn-success btn-sm";
        acceptBtn.textContent = "Accept";
        const rejectBtn = document.createElement("button");
        rejectBtn.type = "button";
        rejectBtn.className = "btn btn-outline-secondary btn-sm";
        rejectBtn.textContent = "Reject";
        actionsRow.appendChild(acceptBtn);
        actionsRow.appendChild(rejectBtn);
        wrap.appendChild(actionsRow);

        function finish(msg) {
            actionsRow.remove();
            const done = document.createElement("div");
            done.className = "small text-muted";
            done.textContent = msg;
            wrap.appendChild(done);
            if (typeof onDone === "function") onDone();
        }

        rejectBtn.addEventListener("click", () => {
            wrap.remove();
            if (typeof onDone === "function") onDone();
        });

        acceptBtn.addEventListener("click", async () => {
            acceptBtn.disabled = true;
            rejectBtn.disabled = true;
            acceptBtn.textContent = "...";
            const ok = await applyActions(envelope);
            finish(ok ? "Applied." : "No matching fields found on this page.");
        });

        return wrap;
    }

    function appendAiMessage(container, text, envelope) {
        const d = document.createElement("div");
        d.className = "chat-bubble-ai";
        d.textContent = text || "";
        if (envelope?.actions?.length) {
            d.appendChild(createActionsPrompt(envelope));
        }
        container.appendChild(d);
        container.scrollTop = container.scrollHeight;
    }

    window.fitlogAI = {
        parseActionEnvelope,
        appendAiMessage,
        applyActions
    };
})();
